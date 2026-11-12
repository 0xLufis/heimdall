using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using App.Backend.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>Development startup wait for ports still held by the previous instance (dotnet watch restarts).</summary>
public sealed class DevelopmentPortWaitTests
{
    private static TcpListener Listen()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        return listener;
    }

    private static int PortOf(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    [Fact]
    public void IsPortFree_IsFalseWhileSomethingListens_AndTrueAfterwards()
    {
        var listener = Listen();
        var port = PortOf(listener);
        Assert.False(DevelopmentPortWait.IsPortFree(port));
        listener.Stop();
        Assert.True(DevelopmentPortWait.IsPortFree(port));
    }

    [Fact]
    public async Task WaitForFreePorts_ReturnsAsSoonAsThePreviousOwnerReleasesThePort()
    {
        var listener = Listen();
        var port = PortOf(listener);
        var release = Task.Delay(300).ContinueWith(_ => listener.Stop());

        var watch = Stopwatch.StartNew();
        var busy = DevelopmentPortWait.WaitForFreePorts(new[] { port }, TimeSpan.FromSeconds(10), NullLogger.Instance, TimeSpan.FromMilliseconds(25));
        watch.Stop();
        await release;

        Assert.Empty(busy);
        Assert.InRange(watch.ElapsedMilliseconds, 250, 5000);
    }

    [Fact]
    public void WaitForFreePorts_GivesUpAfterTheTimeout_AndReportsThePortsStillInUse()
    {
        var listener = Listen();
        try
        {
            var port = PortOf(listener);
            var busy = DevelopmentPortWait.WaitForFreePorts(new[] { port }, TimeSpan.FromMilliseconds(200), NullLogger.Instance, TimeSpan.FromMilliseconds(25));
            Assert.Equal(new[] { port }, busy);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void WaitForFreePorts_DoesNotWaitWhenEverythingIsFree()
    {
        var calls = 0;
        var watch = Stopwatch.StartNew();
        var busy = DevelopmentPortWait.WaitForFreePorts(new[] { 5099, 5001, 1883 }, TimeSpan.FromSeconds(10), NullLogger.Instance,
            isPortFree: _ => { calls++; return true; });
        Assert.Empty(busy);
        Assert.Equal(3, calls);
        Assert.True(watch.ElapsedMilliseconds < 1000);
    }

    [Theory]
    [InlineData(null, null, new[] { 5099, 5001, 1883 })]
    [InlineData("false", null, new[] { 5099, 5001 })]
    [InlineData(null, "1999", new[] { 5099, 5001, 1999 })]
    public void BackendPorts_FollowTheEmbeddedBrokerSettings(string? embedded, string? port, int[] expected)
    {
        var values = new Dictionary<string, string?>();
        if (embedded != null) values["MQTT_EMBEDDED_BROKER"] = embedded;
        if (port != null) values["MQTT_EMBEDDED_PORT"] = port;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Equal(expected, DevelopmentPortWait.BackendPorts(configuration));
    }
}
