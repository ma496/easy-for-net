namespace EasyForNetTool.Tests.Generator;

using System.Net;
using System.Net.Sockets;
using EasyForNetTool.Generator;

/// <summary>
/// Unit tests for <see cref="DevPorts"/>.
/// </summary>
public class DevPortsTests
{
    /// <summary>
    /// Tests that the defaults are kept when nothing holds them.
    /// </summary>
    [Fact]
    public void Should_Keep_Defaults_When_Free()
    {
        // Act
        var ports = DevPorts.Find(_ => true);

        // Assert
        Assert.Equal(DevPorts.Default, ports);
    }

    /// <summary>
    /// Tests that a held default moves to the next free port above it, independently for each service.
    /// </summary>
    [Fact]
    public void Should_Move_Past_Held_Ports()
    {
        // Arrange
        var held = new HashSet<int> { 5432, 5433, 6379 };

        // Act
        var ports = DevPorts.Find(port => !held.Contains(port));

        // Assert
        Assert.Equal(new DevPorts(5434, 6380), ports);
    }

    /// <summary>
    /// Tests that the default is kept when no port in the search range is free, so generation still finishes.
    /// </summary>
    [Fact]
    public void Should_Fall_Back_To_Default_When_Nothing_Is_Free()
    {
        // Act
        var ports = DevPorts.Find(_ => false);

        // Assert
        Assert.Equal(DevPorts.Default, ports);
    }

    /// <summary>
    /// Tests that a port something listens on is reported as held.
    /// </summary>
    [Fact]
    public void Should_Detect_A_Listening_Port()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            // Act
            var free = DevPorts.IsFree(port);

            // Assert
            Assert.False(free);
        }
        finally
        {
            listener.Stop();
        }
    }
}
