namespace EasyForNetTool.Generator;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// The host ports a generated project's development PostgreSQL and Redis are published on.
/// </summary>
/// <param name="Postgres">The host port <c>docker-compose.yml</c> publishes PostgreSQL on.</param>
/// <param name="Redis">The host port <c>docker-compose.yml</c> publishes Redis on.</param>
public sealed record DevPorts(int Postgres, int Redis)
{
    /// <summary>The ports the development services use when nothing else holds them.</summary>
    public static readonly DevPorts Default = new(5432, 6379);

    /// <summary>How many ports above the default are tried before giving up.</summary>
    internal const int SearchRange = 100;

    /// <summary>
    /// Picks the ports for a new project: each default when it is free, otherwise the next free port
    /// above it, so a PostgreSQL or Redis already installed on the machine keeps its port and the new
    /// project's containers never collide with it.
    /// </summary>
    /// <param name="isFree">Whether a port is free; <see cref="IsFree"/> outside tests.</param>
    public static DevPorts Find(Func<int, bool> isFree)
    {
        var postgres = FindFree(Default.Postgres, isFree, []);
        var redis = FindFree(Default.Redis, isFree, [postgres]);
        return new DevPorts(postgres, redis);
    }

    /// <summary>
    /// The first port from <paramref name="preferred"/> upwards that is free and not already taken
    /// by <paramref name="taken"/>; <paramref name="preferred"/> itself when none is found, since a
    /// project generated with a colliding port can still be repointed by hand.
    /// </summary>
    internal static int FindFree(int preferred, Func<int, bool> isFree, IReadOnlyCollection<int> taken)
    {
        for (var port = preferred; port < preferred + SearchRange && port <= IPEndPoint.MaxPort; port++)
        {
            if (!taken.Contains(port) && isFree(port))
                return port;
        }

        return preferred;
    }

    /// <summary>
    /// Whether nothing on this machine holds <paramref name="port"/>. A port is held when something
    /// answers on loopback, or when it cannot be bound exclusively on every interface - the second
    /// catches a server bound to another address and, on Windows, the ranges Hyper-V reserves.
    /// </summary>
    public static bool IsFree(int port)
    {
        try
        {
            using var client = new TcpClient();
            if (client.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromMilliseconds(500)) && client.Connected)
                return false;
        }
        catch
        {
            // refused: nothing is listening on loopback
        }

        try
        {
            var listener = new TcpListener(IPAddress.Any, port);
            // Windows lets a socket bind a port another one holds on a different address unless the bind is exclusive
            if (OperatingSystem.IsWindows())
                listener.ExclusiveAddressUse = true;
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
