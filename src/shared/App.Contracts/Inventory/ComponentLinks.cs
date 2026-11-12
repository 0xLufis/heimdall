namespace App.Contracts.Inventory;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using App.Contracts.Machine;

/// <summary>
/// Where a controller's remote desktop can be reached (noVNC / websockify endpoint).
/// Stored per controller under <c>system_metadata.RemoteAccess.Vnc</c>.
/// </summary>
public class RemoteAccessEndpointDto
{
    /// <summary>
    /// Host of the websocket endpoint. Null means "the host the Heimdall web UI is served from"
    /// (a gateway / dev container published on the same machine).
    /// </summary>
    public string? Host { get; set; }
    public int Port { get; set; }
    /// <summary>Websocket path without a leading slash, e.g. "websockify".</summary>
    public string Path { get; set; } = ComponentLinkResolver.DefaultVncPath;
    /// <summary>Use wss:// regardless of the page protocol.</summary>
    public bool Secure { get; set; }
}

/// <summary>Remote access links resolved for one controller.</summary>
public class ControllerRemoteAccessDto
{
    public Guid ControllerId { get; set; }
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    /// <summary>Null when no VNC endpoint is linked to this controller.</summary>
    public RemoteAccessEndpointDto? Vnc { get; set; }
}

/// <summary>
/// Resolves per-component links (remote desktop endpoint of a controller, 3D model of a station)
/// from the component's own persisted data. There is deliberately no global default: a component
/// without a link resolves to null and the UI says so. Several components may of course be linked
/// to the same target (e.g. every dev controller to one container, every station to one model).
/// </summary>
public static class ComponentLinkResolver
{
    public const string RemoteAccessKey = "RemoteAccess";
    public const string VncKey = "Vnc";
    public const string DefaultVncPath = "websockify";

    /// <summary>
    /// <c>system_metadata</c> keys owned by the server/operators rather than the reporting agent.
    /// They survive an agent heartbeat that replaces the rest of the document.
    /// "AgentLifecycle" is written by the server from lifecycle events (start / stop / last will), not by the system_info report.
    /// </summary>
    public static readonly IReadOnlyList<string> ServerManagedMetadataKeys = new[] { RemoteAccessKey, "AdsPlcMemory", "AgentLifecycle" };

    private static readonly string[] ModelUrlKeys = { "model3dUrl", "Model3dUrl", "model3DUrl", "Model3DUrl" };
    private static readonly Regex HostPattern = new(@"^(\[[0-9A-Fa-f:.]{2,45}\]|[A-Za-z0-9]([A-Za-z0-9.\-]{0,251}[A-Za-z0-9])?)$", RegexOptions.Compiled);
    private static readonly Regex PathPattern = new(@"^[A-Za-z0-9._~\-/?=&%]{0,256}$", RegexOptions.Compiled);

    /// <summary>The VNC endpoint linked to a controller, or null when none (or an invalid one) is stored.</summary>
    public static RemoteAccessEndpointDto? ResolveVnc(JsonDocument? systemMetadata)
    {
        if (systemMetadata == null || systemMetadata.RootElement.ValueKind != JsonValueKind.Object) return null;
        if (!TryGetProperty(systemMetadata.RootElement, RemoteAccessKey, out var remote) || remote.ValueKind != JsonValueKind.Object) return null;
        if (!TryGetProperty(remote, VncKey, out var vnc) || vnc.ValueKind != JsonValueKind.Object) return null;

        var endpoint = new RemoteAccessEndpointDto
        {
            Host = TryGetProperty(vnc, "Host", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null,
            Port = TryGetProperty(vnc, "Port", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var port) ? port : 0,
            Path = TryGetProperty(vnc, "Path", out var pa) && pa.ValueKind == JsonValueKind.String ? pa.GetString() ?? DefaultVncPath : DefaultVncPath,
            Secure = TryGetProperty(vnc, "Secure", out var s) && s.ValueKind == JsonValueKind.True
        };
        return TryNormalize(endpoint, out var normalized, out _) ? normalized : null;
    }

    /// <summary>Validates and normalizes an endpoint (trimmed host, path without leading slash).</summary>
    public static bool TryNormalize(RemoteAccessEndpointDto? input, out RemoteAccessEndpointDto normalized, out string? error)
    {
        normalized = new RemoteAccessEndpointDto();
        error = null;
        if (input == null) { error = "Endpoint is required."; return false; }

        var host = string.IsNullOrWhiteSpace(input.Host) ? null : input.Host.Trim();
        if (host != null && !HostPattern.IsMatch(host)) { error = "Host must be a hostname or IP address."; return false; }
        if (input.Port is < 1 or > 65535) { error = "Port must be between 1 and 65535."; return false; }

        var path = (input.Path ?? string.Empty).Trim().TrimStart('/');
        if (!PathPattern.IsMatch(path) || path.Contains("..", StringComparison.Ordinal) || path.Contains("//", StringComparison.Ordinal))
        {
            error = "Path contains unsupported characters.";
            return false;
        }

        normalized = new RemoteAccessEndpointDto { Host = host, Port = input.Port, Path = path, Secure = input.Secure };
        return true;
    }

    /// <summary>Returns a copy of <paramref name="systemMetadata"/> with the VNC link set (or removed when null).</summary>
    public static JsonDocument WithVnc(JsonDocument? systemMetadata, RemoteAccessEndpointDto? vnc)
    {
        var root = ToObject(systemMetadata);
        var remote = root[RemoteAccessKey] as JsonObject ?? new JsonObject();
        if (vnc == null)
        {
            remote.Remove(VncKey);
        }
        else
        {
            var node = new JsonObject { ["Port"] = vnc.Port, ["Path"] = vnc.Path, ["Secure"] = vnc.Secure };
            if (!string.IsNullOrWhiteSpace(vnc.Host)) node["Host"] = vnc.Host;
            remote[VncKey] = node;
        }

        root.Remove(RemoteAccessKey);
        if (remote.Count > 0) root[RemoteAccessKey] = remote;
        return JsonDocument.Parse(root.ToJsonString());
    }

    /// <summary>
    /// Agent-reported metadata replaces the stored document, except for
    /// <see cref="ServerManagedMetadataKeys"/>, which are carried over when the report lacks them.
    /// </summary>
    public static JsonDocument MergeAgentMetadata(JsonDocument? existing, JsonDocument incoming)
    {
        if (existing == null || existing.RootElement.ValueKind != JsonValueKind.Object) return incoming;
        if (incoming.RootElement.ValueKind != JsonValueKind.Object) return incoming;

        JsonObject? merged = null;
        foreach (var key in ServerManagedMetadataKeys)
        {
            if (incoming.RootElement.TryGetProperty(key, out _)) continue;
            if (!existing.RootElement.TryGetProperty(key, out var kept)) continue;
            merged ??= ToObject(incoming);
            merged[key] = JsonNode.Parse(kept.GetRawText());
        }
        return merged == null ? incoming : JsonDocument.Parse(merged.ToJsonString());
    }

    /// <summary>
    /// The 3D model linked to a station: the station's own <c>metadata.model3dUrl</c>, else the first
    /// model referenced by one of its components' CAD references. Null when nothing is linked.
    /// </summary>
    public static string? ResolveModel3dUrl(JsonDocument? stationMetadata, IEnumerable<EplanCadReference?>? componentCadReferences = null)
    {
        if (stationMetadata != null && stationMetadata.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in ModelUrlKeys)
            {
                if (stationMetadata.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var url = value.GetString();
                    if (!string.IsNullOrWhiteSpace(url)) return url.Trim();
                }
            }
        }

        if (componentCadReferences != null)
        {
            foreach (var cad in componentCadReferences)
            {
                if (!string.IsNullOrWhiteSpace(cad?.Model3dUrl)) return cad!.Model3dUrl!.Trim();
            }
        }
        return null;
    }

    private static JsonObject ToObject(JsonDocument? document) =>
        document != null && document.RootElement.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(document.RootElement.GetRawText())?.AsObject() ?? new JsonObject()
            : new JsonObject();

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        // Tolerate camelCase documents written by hand or by the BFF.
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        return element.TryGetProperty(camel, out value);
    }
}
