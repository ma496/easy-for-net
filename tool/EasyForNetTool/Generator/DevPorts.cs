using System.Net;
using System.Net.Sockets;

namespace EasyForNetTool.Generator;

/// <summary>
/// The host ports a generated project develops on: the PostgreSQL and Redis containers, and the
/// three processes <c>pnpm dev</c> starts.
/// </summary>
/// <param name="Postgres">The host port <c>docker-compose.yml</c> publishes PostgreSQL on.</param>
/// <param name="Redis">The host port <c>docker-compose.yml</c> publishes Redis on.</param>
/// <param name="Proxy">The dev proxy, the address the browser opens (<c>DEV_PROXY_PORT</c>).</param>
/// <param name="Web"><c>next dev</c>, behind the proxy (<c>DEV_WEB_PORT</c>).</param>
/// <param name="Api">The API (<c>DEV_API_PORT</c>).</param>
public sealed record DevPorts(int Postgres, int Redis, int Proxy = 3000, int Web = 3001, int Api = 5000)
{
    /// <summary>The ports a project develops on when nothing else holds them.</summary>
    public static readonly DevPorts Default = new(5432, 6379, 3000, 3001, 5000);

    /// <summary>How many ports above the default are tried before giving up.</summary>
    internal const int SearchRange = 100;

    /// <summary>
    /// Picks the ports for a new project: each default when it is free, otherwise the next free port
    /// above it, so a PostgreSQL, Redis or web app already running on the machine keeps its port and
    /// the new project never collides with it - nor hides it, which a loopback bind beside another
    /// app's wildcard listener does on Windows and macOS. That is also what moves the API off 5000
    /// on a Mac, where the AirPlay Receiver holds it.
    /// </summary>
    /// <param name="isFree">Whether a port is free; <see cref="IsFree"/> outside tests.</param>
    public static DevPorts Find(Func<int, bool> isFree)
    {
        var postgres = FindFree(Default.Postgres, isFree, []);
        var redis = FindFree(Default.Redis, isFree, [postgres]);
        var proxy = FindFree(Default.Proxy, isFree, [postgres, redis]);
        var web = FindFree(Default.Web, isFree, [postgres, redis, proxy]);
        var api = FindFree(Default.Api, isFree, [postgres, redis, proxy, web]);
        return new DevPorts(postgres, redis, proxy, web, api);
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
    /// answers on either loopback address, or when it cannot be bound exclusively on every interface -
    /// the second catches a server bound to another address and, on Windows, the ranges Hyper-V reserves.
    /// </summary>
    public static bool IsFree(int port)
    {
        if (Answers(IPAddress.Loopback, port) || (Socket.OSSupportsIPv6 && Answers(IPAddress.IPv6Loopback, port)))
            return false;

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

    /// <summary>Whether something accepts a TCP connection on <paramref name="address"/>:<paramref name="port"/>.</summary>
    private static bool Answers(IPAddress address, int port)
    {
        try
        {
            using var client = new TcpClient(address.AddressFamily);
            return client.ConnectAsync(address, port).Wait(TimeSpan.FromMilliseconds(500)) && client.Connected;
        }
        catch
        {
            // refused, or the address does not exist here: nothing is listening on it
            return false;
        }
    }
}
