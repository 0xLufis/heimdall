using System.Text.Json;
using App.Backend.Api.Controllers.V1;
using App.Contracts.Inventory;
using App.Contracts.Machine;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

/// <summary>
/// Remote desktop endpoints and 3D models are resolved from the component's own data, never from a
/// global default: two components only show the same target when both are linked to it.
/// </summary>
public class ComponentLinkResolverTests
{
    private static JsonDocument Json(string json) => JsonDocument.Parse(json);

    [Fact]
    public void ResolveVnc_ReturnsNull_WhenControllerHasNoLink()
    {
        Assert.Null(ComponentLinkResolver.ResolveVnc(null));
        Assert.Null(ComponentLinkResolver.ResolveVnc(Json("""{"OperatingSystem":"Windows 10 IoT"}""")));
        Assert.Null(ComponentLinkResolver.ResolveVnc(Json("""{"RemoteAccess":{}}""")));
    }

    [Fact]
    public void ResolveVnc_ReadsEndpoint_PerController()
    {
        var a = ComponentLinkResolver.ResolveVnc(Json("""{"RemoteAccess":{"Vnc":{"Host":"10.42.1.11","Port":6080,"Path":"/websockify","Secure":true}}}"""));
        var b = ComponentLinkResolver.ResolveVnc(Json("""{"RemoteAccess":{"Vnc":{"Port":8006}}}"""));

        Assert.NotNull(a);
        Assert.Equal("10.42.1.11", a!.Host);
        Assert.Equal(6080, a.Port);
        Assert.Equal("websockify", a.Path);
        Assert.True(a.Secure);

        Assert.NotNull(b);
        Assert.Null(b!.Host);
        Assert.Equal(8006, b.Port);
        Assert.Equal("websockify", b.Path);
        Assert.False(b.Secure);
    }

    [Theory]
    [InlineData("""{"RemoteAccess":{"Vnc":{"Host":"evil.example/x?","Port":8006}}}""")]
    [InlineData("""{"RemoteAccess":{"Vnc":{"Host":"ok.example","Port":0}}}""")]
    [InlineData("""{"RemoteAccess":{"Vnc":{"Host":"ok.example","Port":70000}}}""")]
    [InlineData("""{"RemoteAccess":{"Vnc":{"Host":"ok.example","Port":8006,"Path":"../admin"}}}""")]
    [InlineData("""{"RemoteAccess":{"Vnc":{"Host":"user@ok.example","Port":8006}}}""")]
    public void ResolveVnc_RejectsInvalidEndpoints(string metadata)
    {
        Assert.Null(ComponentLinkResolver.ResolveVnc(Json(metadata)));
    }

    [Fact]
    public void WithVnc_SetsAndClearsLink_WithoutTouchingOtherKeys()
    {
        var original = Json("""{"OperatingSystem":"Windows 10 IoT","RemoteAccess":{"Rdp":{"Port":3389}}}""");

        var linked = ComponentLinkResolver.WithVnc(original, new RemoteAccessEndpointDto { Host = "ipc-07", Port = 5901, Path = "websockify" });
        Assert.Equal("Windows 10 IoT", linked.RootElement.GetProperty("OperatingSystem").GetString());
        Assert.Equal(3389, linked.RootElement.GetProperty("RemoteAccess").GetProperty("Rdp").GetProperty("Port").GetInt32());
        Assert.Equal("ipc-07", ComponentLinkResolver.ResolveVnc(linked)!.Host);

        var cleared = ComponentLinkResolver.WithVnc(linked, null);
        Assert.Null(ComponentLinkResolver.ResolveVnc(cleared));
        Assert.True(cleared.RootElement.GetProperty("RemoteAccess").TryGetProperty("Rdp", out _));
    }

    [Fact]
    public void MergeAgentMetadata_KeepsOperatorLinks_WhenHeartbeatReplacesMetadata()
    {
        var stored = Json("""{"OperatingSystem":"old","Stale":"x","RemoteAccess":{"Vnc":{"Port":8006}},"AdsPlcMemory":{"SymbolName":"MAIN.x"}}""");
        var heartbeat = Json("""{"OperatingSystem":"new","IPAddress":"10.0.0.5"}""");

        var merged = ComponentLinkResolver.MergeAgentMetadata(stored, heartbeat);

        Assert.Equal("new", merged.RootElement.GetProperty("OperatingSystem").GetString());
        Assert.False(merged.RootElement.TryGetProperty("Stale", out _));
        Assert.Equal(8006, ComponentLinkResolver.ResolveVnc(merged)!.Port);
        Assert.Equal("MAIN.x", merged.RootElement.GetProperty("AdsPlcMemory").GetProperty("SymbolName").GetString());
    }

    [Fact]
    public void MergeAgentMetadata_LetsTheReportOverrideManagedKeys()
    {
        var stored = Json("""{"AdsPlcMemory":{"SymbolName":"MAIN.old"}}""");
        var heartbeat = Json("""{"AdsPlcMemory":{"SymbolName":"MAIN.new"}}""");

        var merged = ComponentLinkResolver.MergeAgentMetadata(stored, heartbeat);

        Assert.Equal("MAIN.new", merged.RootElement.GetProperty("AdsPlcMemory").GetProperty("SymbolName").GetString());
    }

    [Fact]
    public void ResolveModel3dUrl_PrefersStationMetadata_ThenComponentReference_ElseNull()
    {
        var cadRefs = new[] { null, new EplanCadReference { Cad3dNodePath = "FRAME" }, new EplanCadReference { Model3dUrl = " /models/from-component.glb " } };

        Assert.Equal("/models/station-a.glb", ComponentLinkResolver.ResolveModel3dUrl(Json("""{"model3dUrl":"/models/station-a.glb"}"""), cadRefs));
        Assert.Equal("/models/from-component.glb", ComponentLinkResolver.ResolveModel3dUrl(Json("""{"line":"L01"}"""), cadRefs));
        Assert.Null(ComponentLinkResolver.ResolveModel3dUrl(Json("""{"model3dUrl":"  "}"""), new EplanCadReference?[] { null }));
        Assert.Null(ComponentLinkResolver.ResolveModel3dUrl(null));
    }

    [Fact]
    public async Task RemoteAccessEndpoints_ResolvePerController()
    {
        var repo = new FakeControllerRepository();
        var linked = new ClientPc { Id = Guid.NewGuid(), Name = "IPC-A", Hostname = "ipc-a", MacAddress = "02:00:00:00:00:01" };
        var unlinked = new ClientPc { Id = Guid.NewGuid(), Name = "IPC-B", Hostname = "ipc-b", MacAddress = "02:00:00:00:00:02" };
        repo.Items.AddRange(new[] { linked, unlinked });
        var controller = new ClientPcController(repo, NullLogger<ClientPcController>.Instance);

        var set = await controller.SetVncEndpoint(linked.Id, new RemoteAccessEndpointDto { Host = "ipc-a.plant", Port = 6080, Path = "/websockify" });
        var setDto = Assert.IsType<ControllerRemoteAccessDto>(Assert.IsType<OkObjectResult>(set.Result).Value);
        Assert.Equal("ipc-a.plant", setDto.Vnc!.Host);

        var a = Assert.IsType<ControllerRemoteAccessDto>(Assert.IsType<OkObjectResult>((await controller.GetRemoteAccess(linked.Id)).Result).Value);
        var b = Assert.IsType<ControllerRemoteAccessDto>(Assert.IsType<OkObjectResult>((await controller.GetRemoteAccess(unlinked.Id)).Result).Value);
        Assert.Equal(6080, a.Vnc!.Port);
        Assert.Equal("websockify", a.Vnc.Path);
        Assert.Null(b.Vnc);

        Assert.IsType<BadRequestObjectResult>((await controller.SetVncEndpoint(linked.Id, new RemoteAccessEndpointDto { Host = "bad host", Port = 6080 })).Result);
        Assert.IsType<NotFoundResult>((await controller.GetRemoteAccess(Guid.NewGuid())).Result);

        Assert.IsType<NoContentResult>(await controller.ClearVncEndpoint(linked.Id));
        var afterClear = Assert.IsType<ControllerRemoteAccessDto>(Assert.IsType<OkObjectResult>((await controller.GetRemoteAccess(linked.Id)).Result).Value);
        Assert.Null(afterClear.Vnc);
    }
}
