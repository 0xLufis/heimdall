namespace App.Agent.Daemon.Infrastructure.Identity;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using App.Agent.Daemon.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

/// <summary>The identity this agent reports under, as persisted in <c>state/identity.json</c>.</summary>
public sealed class IdentityRecord
{
    /// <summary>The machine identifier used in MQTT topics and reports.</summary>
    public string MachineIdentifier { get; set; } = string.Empty;
    /// <summary>smbios_uuid | machine_guid | machine_id | dbus_machine_id | generated.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Last MAC address that passed validation; reused when no adapter yields a valid one.</summary>
    public string? MacAddress { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A place a hardware / OS bound machine identifier can be read from.</summary>
/// <param name="Source">Name recorded with the identity ("smbios_uuid", "machine_guid", "machine_id", ...).</param>
/// <param name="Read">Returns the raw identifier or null; may throw.</param>
public sealed record HardwareIdProbe(string Source, Func<string?> Read);

/// <summary>The machine identity this agent reports under.</summary>
public interface IMachineIdentity
{
    /// <summary>Stable machine identifier. Never empty, never a placeholder shared with other machines.</summary>
    string MachineIdentifier { get; }

    /// <summary>Where <see cref="MachineIdentifier"/> came from.</summary>
    string Source { get; }

    /// <summary>
    /// Picks the MAC address to report: the previously reported one while that adapter still exists,
    /// otherwise the fastest connected adapter with a real hardware address. Returns an empty string
    /// when no adapter has a usable address and none was ever recorded.
    /// </summary>
    string ResolveMacAddress(IEnumerable<NetworkAdapterInfo>? adapters = null);
}

/// <summary>What the identity needs to know about a network adapter.</summary>
public sealed record NetworkAdapterInfo(string MacAddress, bool IsUp, long Speed, bool IsLoopbackOrTunnel);

/// <summary>
/// Resolves the machine identifier once per process and keeps it stable across restarts.
/// <para>
/// Earlier versions derived the identifier on every start and silently fell back to
/// <c>"{MachineName}-{OSVersion}"</c> whenever the hardware query failed (WMI not yet started after a
/// reboot, unreadable <c>/etc/machine-id</c>) - the machine then reported under a different identity,
/// subscribed to a different command topic and appeared as a new device. They also accepted
/// placeholder SMBIOS UUIDs (all zeros, all F, "03000200-0400-...") that many boards and every
/// unconfigured VM share, so several machines collided on one identity.
/// </para>
/// Rules:
/// <list type="number">
/// <item>The first probe that returns a <see cref="IsPlausibleMachineId">plausible</see> identifier wins and is persisted.</item>
/// <item>If no probe succeeds, the persisted identity is used (hardware temporarily unreadable).</item>
/// <item>Only when nothing was ever persisted a random identifier is generated and persisted.</item>
/// <item>A damaged identity file falls back to its backup; any change of identity relative to what
/// was persisted is recorded in the recovery journal, so it is reported instead of happening silently.</item>
/// </list>
/// </summary>
public sealed class MachineIdentityResolver : IMachineIdentity
{
    private static readonly string[] PlaceholderTexts =
    {
        "unknown", "unknown uuid", "none", "n/a", "na", "null", "default string", "not settable", "not present",
        "not specified", "not applicable", "to be filled by o.e.m.", "system serial number", "uninitialized", "invalid"
    };

    // SMBIOS UUIDs shipped unprogrammed by board vendors (shared by thousands of machines).
    private static readonly string[] PlaceholderUuids =
    {
        "03000200040005000006000700080009",
        "00020003000400050006000700080009",
        "0123456789abcdef0123456789abcdef",
        "12345678123412341234123456789abc"
    };

    private readonly DurableJsonFile<IdentityRecord>? _store;
    private readonly IRecoveryJournal? _journal;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private IdentityRecord _record;

    /// <param name="identityFile">Where the identity is persisted; null keeps it in memory (tests, read-only installs).</param>
    /// <param name="probes">Hardware identifier sources in priority order.</param>
    public MachineIdentityResolver(string? identityFile, IEnumerable<HardwareIdProbe> probes,
        IRecoveryJournal? journal = null, ILogger? logger = null)
    {
        _journal = journal;
        _logger = logger;
        _store = string.IsNullOrWhiteSpace(identityFile)
            ? null
            : new DurableJsonFile<IdentityRecord>(identityFile, r => IsPlausibleMachineId(r.MachineIdentifier) ? null : "machine identifier is empty or a placeholder");
        _record = Resolve(probes?.ToList() ?? new List<HardwareIdProbe>());
    }

    public string MachineIdentifier
    {
        get { lock (_lock) return _record.MachineIdentifier; }
    }

    public string Source
    {
        get { lock (_lock) return _record.Source; }
    }

    /// <summary>Creates the resolver with the hardware sources of the current operating system.</summary>
    public static MachineIdentityResolver CreateDefault(AgentPaths paths, IRecoveryJournal? journal = null, ILogger? logger = null)
    {
        return new MachineIdentityResolver(paths.IdentityFile, DefaultProbes(), journal, logger);
    }

    /// <summary>Hardware identifier sources in the order earlier agent versions consulted them, plus fallbacks.</summary>
    public static IReadOnlyList<HardwareIdProbe> DefaultProbes()
    {
        var probes = new List<HardwareIdProbe>();
        if (OperatingSystem.IsWindows())
        {
            probes.Add(new HardwareIdProbe("smbios_uuid", () => ReadSmbiosUuidWindows()));
            probes.Add(new HardwareIdProbe("machine_guid", () => ReadMachineGuidWindows()));
        }
        else
        {
            probes.Add(new HardwareIdProbe("machine_id", () => ReadFirstLine("/etc/machine-id")));
            probes.Add(new HardwareIdProbe("dbus_machine_id", () => ReadFirstLine("/var/lib/dbus/machine-id")));
            probes.Add(new HardwareIdProbe("smbios_uuid", () => ReadFirstLine("/sys/class/dmi/id/product_uuid")));
        }
        return probes;
    }

    private IdentityRecord Resolve(List<HardwareIdProbe> probes)
    {
        var persisted = _store?.Load();
        var known = persisted?.Value;

        string? hardwareId = null;
        string? hardwareSource = null;
        var rejected = new List<string>();

        foreach (var probe in probes)
        {
            string? candidate;
            try
            {
                candidate = probe.Read()?.Trim();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Machine identifier source {Source} is not readable", probe.Source);
                continue;
            }

            if (string.IsNullOrEmpty(candidate)) continue;

            if (IsPlausibleMachineId(candidate))
            {
                hardwareId = candidate;
                hardwareSource = probe.Source;
                break;
            }

            rejected.Add($"{probe.Source}={Shorten(candidate)}");
        }

        if (persisted is { Recovered: true })
        {
            _journal?.Record(new RecoveryNotice
            {
                Component = "identity",
                File = _store!.Path,
                Defect = RecoveryNotice.DefectName(persisted.PrimaryDefect),
                RecoveredFrom = known != null ? "backup" : hardwareId != null ? "hardware" : "regenerated",
                Severity = known != null || hardwareId != null ? "Warning" : "Critical",
                Detail = persisted.Detail ?? "identity file could not be read"
            });
        }

        IdentityRecord record;
        if (hardwareId != null)
        {
            if (known == null)
            {
                record = new IdentityRecord { MachineIdentifier = hardwareId, Source = hardwareSource! };
                if (rejected.Count > 0)
                {
                    // Explains why this machine no longer reports under the placeholder it used before.
                    _logger?.LogWarning("Ignored placeholder machine identifier(s) {Rejected}; using {Source}.", string.Join(", ", rejected), hardwareSource);
                    _journal?.Record(new RecoveryNotice
                    {
                        Component = "identity",
                        File = _store?.Path ?? string.Empty,
                        Defect = "invalid",
                        RecoveredFrom = "hardware",
                        Severity = "Warning",
                        Detail = $"placeholder identifier(s) rejected: {string.Join(", ", rejected)}; machine now reports as {hardwareId} (source {hardwareSource})"
                    });
                }
                Save(record);
            }
            else if (string.Equals(known.MachineIdentifier, hardwareId, StringComparison.OrdinalIgnoreCase))
            {
                record = known;
                if (persisted!.Origin == DurableOrigin.Backup) Save(record);
            }
            else
            {
                // The hardware says otherwise than the file: board replaced, or this disk image was
                // cloned from another machine. The hardware wins, and the change is reported.
                _logger?.LogWarning("Machine identifier changed from {Old} ({OldSource}) to {New} ({NewSource}).",
                    known.MachineIdentifier, known.Source, hardwareId, hardwareSource);
                _journal?.Record(new RecoveryNotice
                {
                    Component = "identity",
                    File = _store?.Path ?? string.Empty,
                    Defect = "changed",
                    RecoveredFrom = "hardware",
                    Severity = "Warning",
                    Detail = $"machine identifier changed from {known.MachineIdentifier} ({known.Source}) to {hardwareId} ({hardwareSource}): hardware replaced or disk image cloned"
                });
                record = new IdentityRecord
                {
                    MachineIdentifier = hardwareId,
                    Source = hardwareSource!,
                    MacAddress = known.MacAddress,
                    CreatedAtUtc = known.CreatedAtUtc
                };
                Save(record);
            }
        }
        else if (known != null)
        {
            // Hardware identifier not readable right now (WMI not up yet, permissions): stay who we were.
            _logger?.LogWarning("No hardware machine identifier is readable; keeping the persisted identity {Id} ({Source}).",
                known.MachineIdentifier, known.Source);
            record = known;
            if (persisted!.Origin == DurableOrigin.Backup) Save(record);
        }
        else
        {
            record = new IdentityRecord { MachineIdentifier = "HMD-" + Guid.NewGuid().ToString("D").ToUpperInvariant(), Source = "generated" };
            _logger?.LogWarning("No hardware machine identifier is available on this system; generated {Id}.", record.MachineIdentifier);
            if (rejected.Count > 0)
            {
                _journal?.Record(new RecoveryNotice
                {
                    Component = "identity",
                    File = _store?.Path ?? string.Empty,
                    Defect = "invalid",
                    RecoveredFrom = "regenerated",
                    Severity = "Warning",
                    Detail = $"placeholder identifier(s) rejected: {string.Join(", ", rejected)}; generated {record.MachineIdentifier}"
                });
            }
            Save(record);
        }

        return record;
    }

    public string ResolveMacAddress(IEnumerable<NetworkAdapterInfo>? adapters = null)
    {
        List<NetworkAdapterInfo> all;
        try
        {
            all = (adapters ?? EnumerateAdapters()).Where(a => IsPlausibleMac(a.MacAddress) && !a.IsLoopbackOrTunnel).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Network adapters could not be enumerated");
            all = new List<NetworkAdapterInfo>();
        }

        lock (_lock)
        {
            string? remembered = _record.MacAddress;

            // Stay on the adapter reported before for as long as it exists: the server keys the
            // device by MAC, and "fastest adapter" changes with docking stations, VPNs and link speed.
            if (remembered != null && all.Any(a => string.Equals(a.MacAddress, remembered, StringComparison.OrdinalIgnoreCase)))
            {
                return remembered;
            }

            var best = all.Where(a => a.IsUp).OrderByDescending(a => a.Speed).FirstOrDefault() ?? all.FirstOrDefault();
            if (best == null)
            {
                return remembered ?? string.Empty;
            }

            string mac = best.MacAddress.ToUpperInvariant();
            if (!string.Equals(remembered, mac, StringComparison.OrdinalIgnoreCase))
            {
                _record.MacAddress = mac;
                Save(_record);
            }
            return mac;
        }
    }

    private void Save(IdentityRecord record)
    {
        if (_store == null) return;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (!_store.TrySave(record, out var error))
        {
            _logger?.LogWarning("Could not persist machine identity to {Path}: {Error}", _store.Path, error);
        }
    }

    /// <summary>
    /// True when the text can serve as a machine identifier: 8 to 128 printable characters and not one
    /// of the placeholders firmware and uninitialized OS images are known to hand out.
    /// </summary>
    public static bool IsPlausibleMachineId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        string trimmed = value.Trim();
        if (trimmed.Length < 8 || trimmed.Length > 128) return false;

        foreach (char c in trimmed)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c) && c != ' ') return false;
        }

        string lower = trimmed.ToLowerInvariant();
        if (PlaceholderTexts.Contains(lower)) return false;

        string compact = new string(lower.Where(char.IsLetterOrDigit).ToArray());
        if (compact.Length == 0) return false;
        if (compact.Distinct().Count() == 1) return false; // 0000..., ffff..., xxxx...
        if (PlaceholderUuids.Contains(compact)) return false;

        return true;
    }

    /// <summary>True for a 48-bit hardware address in <c>AA:BB:CC:DD:EE:FF</c> form that is neither all zeros nor broadcast.</summary>
    public static bool IsPlausibleMac(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 17) return false;

        bool allZero = true;
        bool allOnes = true;
        for (int i = 0; i < 17; i++)
        {
            char c = value[i];
            if (i % 3 == 2)
            {
                if (c != ':') return false;
                continue;
            }
            if (!Uri.IsHexDigit(c)) return false;
            if (c != '0') allZero = false;
            if (c is not ('F' or 'f')) allOnes = false;
        }

        return !allZero && !allOnes;
    }

    private static IEnumerable<NetworkAdapterInfo> EnumerateAdapters()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            string mac;
            try
            {
                mac = string.Join(":", nic.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
            }
            catch
            {
                continue;
            }

            long speed = 0;
            try
            {
                speed = nic.Speed;
            }
            catch
            {
                // Not reported for this adapter type; it simply ranks last.
            }

            bool virtualOnly = nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel;
            yield return new NetworkAdapterInfo(mac, nic.OperationalStatus == OperationalStatus.Up, speed, virtualOnly);
        }
    }

    private static string? ReadFirstLine(string path)
    {
        if (!File.Exists(path)) return null;
        using var reader = new StreamReader(path);
        return reader.ReadLine()?.Trim();
    }

    private static string? ReadSmbiosUuidWindows()
    {
        if (!OperatingSystem.IsWindows()) return null;

        using var searcher = new System.Management.ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
        searcher.Options.Timeout = TimeSpan.FromSeconds(10);
        foreach (var obj in searcher.Get())
        {
            return obj["UUID"]?.ToString();
        }
        return null;
    }

    private static string? ReadMachineGuidWindows()
    {
        if (!OperatingSystem.IsWindows()) return null;

        using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return key?.GetValue("MachineGuid")?.ToString();
    }

    private static string Shorten(string value) => value.Length <= 48 ? value : value[..48] + "...";
}
