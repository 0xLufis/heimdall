using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Shared.Data;
using App.Shared.Entities;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "SystemAdministration")]
public class ActiveDirectoryController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ILogger<ActiveDirectoryController> _logger;

    public ActiveDirectoryController(IDbContextFactory<AppDbContext> dbContextFactory, ILogger<ActiveDirectoryController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    [HttpGet("ous")]
    public async Task<IActionResult> GetOrganizationalUnits()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var existingHostnames = await db.ClientPcs
            .Where(p => p.Hostname != null)
            .Select(p => p.Hostname!.ToLower())
            .ToListAsync();

        var ous = GetFactoryActiveDirectoryOUs();

        // Mark existing status on candidate hosts
        foreach (var ou in ous)
        {
            foreach (var h in ou.CandidateHosts)
            {
                h.AlreadyImported = existingHostnames.Contains(h.Hostname.ToLower());
            }
            ou.HostCount = ou.CandidateHosts.Count;
        }

        return Ok(ous);
    }

    [HttpPost("preview-import")]
    public IActionResult PreviewImport([FromBody] AdImportPreviewRequest request)
    {
        var allOus = GetFactoryActiveDirectoryOUs();
        var selectedOus = (request.SelectedOuPaths == null || request.SelectedOuPaths.Count == 0)
            ? allOus
            : allOus.Where(ou => request.SelectedOuPaths.Contains(ou.OuPath)).ToList();

        var tagTemplates = request.TagTemplates ?? new Dictionary<string, string>
        {
            { "location", "{LOCATION}" },
            { "purpose", "{PURPOSE}" },
            { "machine_type", "{MACHINE_TYPE}" },
            { "vlan", "VLAN-{VLAN_ID}" }
        };

        var previewItems = new List<AdHostPreviewItem>();

        foreach (var ou in selectedOus)
        {
            var ouTokens = ExtractOuTokens(ou.OuPath);

            foreach (var host in ou.CandidateHosts)
            {
                var resolvedTags = new Dictionary<string, string>();

                if (request.TagRules != null && request.TagRules.Count > 0)
                {
                    foreach (var rule in request.TagRules)
                    {
                        if (string.IsNullOrWhiteSpace(rule.KeyTemplate)) continue;
                        var resolvedKey = ResolvePattern(rule.KeyTemplate, host, ou, ouTokens);
                        var resolvedValue = ResolvePattern(rule.ValueTemplate ?? string.Empty, host, ou, ouTokens);
                        if (!string.IsNullOrWhiteSpace(resolvedKey))
                        {
                            resolvedTags[resolvedKey] = resolvedValue;
                        }
                    }
                }
                else
                {
                    foreach (var (tagKey, pattern) in tagTemplates)
                    {
                        resolvedTags[tagKey] = ResolvePattern(pattern, host, ou, ouTokens);
                    }
                }

                previewItems.Add(new AdHostPreviewItem
                {
                    Hostname = host.Hostname,
                    Name = ResolvePattern(request.NamingPattern ?? "{NAME}", host, ou, ouTokens),
                    MacAddress = host.MacAddress,
                    IpAddress = host.IpAddress,
                    MachineIdentifier = host.MachineIdentifier,
                    OsVersion = host.OsVersion,
                    VlanId = ou.VlanId,
                    VlanName = ou.VlanName,
                    Subnet = ou.Subnet,
                    AdOuPath = ou.OuPath,
                    OuTags = resolvedTags
                });
            }
        }

        return Ok(new AdImportPreviewResponse
        {
            TotalFound = previewItems.Count,
            SelectedOusCount = selectedOus.Count,
            Preview = previewItems
        });
    }

    [HttpPost("import-hosts")]
    public async Task<IActionResult> ImportHosts([FromBody] AdHostImportRequest request)
    {
        if (request.Hosts == null || request.Hosts.Count == 0)
        {
            return BadRequest(new { Message = "No hosts provided for import." });
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var ouRules = await db.OuCertificateRules.Where(r => r.AutoEnroll).ToListAsync();

        int importedCount = 0;
        int updatedCount = 0;

        foreach (var hostItem in request.Hosts)
        {
            var existing = await db.ClientPcs
                .FirstOrDefaultAsync(p => p.MacAddress == hostItem.MacAddress || (p.Hostname != null && p.Hostname == hostItem.Hostname));

            var tagsJson = hostItem.OuTags != null ? JsonSerializer.Serialize(hostItem.OuTags) : "{}";
            var tagsDoc = JsonDocument.Parse(tagsJson);

            // Determine if OU rule matches for auto-enrollment
            string? assignedThumbprint = null;
            string? assignedProfile = null;

            if (!string.IsNullOrEmpty(hostItem.AdOuPath))
            {
                var matchingRule = ouRules.FirstOrDefault(r =>
                    hostItem.AdOuPath.Equals(r.OuPath, StringComparison.OrdinalIgnoreCase) ||
                    hostItem.AdOuPath.Contains(r.OuPath, StringComparison.OrdinalIgnoreCase));

                if (matchingRule != null)
                {
                    assignedProfile = matchingRule.ProfileName;
                    assignedThumbprint = Guid.NewGuid().ToString("N").ToUpper();
                }
            }

            if (existing == null)
            {
                var newPc = new ClientPc
                {
                    Id = Guid.NewGuid(),
                    Name = hostItem.Name,
                    MacAddress = hostItem.MacAddress,
                    IpAddress = hostItem.IpAddress,
                    Hostname = hostItem.Hostname,
                    MachineIdentifier = hostItem.MachineIdentifier,
                    LastOnline = DateTimeOffset.UtcNow,
                    VlanId = hostItem.VlanId,
                    VlanName = hostItem.VlanName,
                    Subnet = hostItem.Subnet,
                    AdOuPath = hostItem.AdOuPath,
                    OuTags = tagsDoc,
                    CertificateThumbprint = assignedThumbprint,
                    CertificateProfileName = assignedProfile
                };
                db.ClientPcs.Add(newPc);
                importedCount++;
            }
            else
            {
                existing.Name = hostItem.Name;
                existing.IpAddress = hostItem.IpAddress;
                existing.VlanId = hostItem.VlanId;
                existing.VlanName = hostItem.VlanName;
                existing.Subnet = hostItem.Subnet;
                existing.AdOuPath = hostItem.AdOuPath;
                existing.OuTags = tagsDoc;
                if (!string.IsNullOrEmpty(assignedThumbprint))
                {
                    existing.CertificateThumbprint = assignedThumbprint;
                    existing.CertificateProfileName = assignedProfile;
                }
                updatedCount++;
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Imported {Imported} new and updated {Updated} client PCs via Active Directory VLAN OU discovery.", importedCount, updatedCount);

        return Ok(new
        {
            Message = $"Successfully processed {request.Hosts.Count} hosts: {importedCount} imported, {updatedCount} updated.",
            ImportedCount = importedCount,
            UpdatedCount = updatedCount,
            TotalProcessed = request.Hosts.Count
        });
    }

    private static string ResolvePattern(string pattern, AdCandidateHost host, AdOrganizationalUnit ou, List<string> ouTokens)
    {
        var result = pattern
            .Replace("{HOSTNAME}", host.Hostname)
            .Replace("{NAME}", host.Name)
            .Replace("{IP}", host.IpAddress)
            .Replace("{MAC}", host.MacAddress)
            .Replace("{VLAN_ID}", ou.VlanId.ToString())
            .Replace("{VLAN_NAME}", ou.VlanName)
            .Replace("{SUBNET}", ou.Subnet)
            .Replace("{LOCATION}", ou.Location)
            .Replace("{PURPOSE}", ou.Purpose)
            .Replace("{MACHINE_TYPE}", ou.MachineType);

        for (int i = 0; i < ouTokens.Count; i++)
        {
            result = result.Replace($"{{OU[{i}]}}", ouTokens[i]);
        }

        return result;
    }

    private static List<string> ExtractOuTokens(string ouPath)
    {
        var matches = Regex.Matches(ouPath, @"OU=([^,]+)");
        var list = new List<string>();
        foreach (Match m in matches)
        {
            list.Add(m.Groups[1].Value);
        }
        return list;
    }

    private static List<AdOrganizationalUnit> GetFactoryActiveDirectoryOUs()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "fixtures", "enterprise_plant_dataset.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "fixtures", "enterprise_plant_dataset.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "fixtures", "enterprise_plant_dataset.json"),
            "/app/fixtures/enterprise_plant_dataset.json"
        };

        foreach (var path in candidatePaths)
        {
            if (System.IO.File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("activeDirectoryOUs", out var ousProp) &&
                        doc.RootElement.TryGetProperty("clientPcs", out var pcsProp))
                    {
                        var pcsByHost = new Dictionary<string, (string Name, string Ip, string Mac, string Os, string Mid)>(StringComparer.OrdinalIgnoreCase);
                        foreach (var pc in pcsProp.EnumerateArray())
                        {
                            var h = pc.GetProperty("hostname").GetString() ?? "";
                            var n = pc.GetProperty("name").GetString() ?? h;
                            var ip = pc.GetProperty("ipAddress").GetString() ?? "";
                            var mac = pc.GetProperty("macAddress").GetString() ?? "";
                            var os = pc.GetProperty("osVersion").GetString() ?? "";
                            var mid = pc.GetProperty("machineIdentifier").GetString() ?? "";
                            pcsByHost[h] = (n, ip, mac, os, mid);
                        }

                        var result = new List<AdOrganizationalUnit>();
                        foreach (var ou in ousProp.EnumerateArray())
                        {
                            var adOu = new AdOrganizationalUnit
                            {
                                OuPath = ou.GetProperty("ouPath").GetString() ?? "",
                                Name = ou.GetProperty("name").GetString() ?? "",
                                VlanId = ou.GetProperty("vlanId").GetInt32(),
                                VlanName = ou.GetProperty("vlanName").GetString() ?? "",
                                Subnet = ou.GetProperty("subnet").GetString() ?? "",
                                Location = ou.GetProperty("location").GetString() ?? "",
                                Purpose = ou.GetProperty("purpose").GetString() ?? "",
                                MachineType = ou.GetProperty("machineType").GetString() ?? "",
                                CandidateHosts = new List<AdCandidateHost>()
                            };

                            if (ou.TryGetProperty("candidateHostnames", out var hostnamesProp))
                            {
                                foreach (var hn in hostnamesProp.EnumerateArray())
                                {
                                    var hStr = hn.GetString() ?? "";
                                    if (pcsByHost.TryGetValue(hStr, out var info))
                                    {
                                        adOu.CandidateHosts.Add(new AdCandidateHost
                                        {
                                            Hostname = hStr,
                                            Name = info.Name,
                                            IpAddress = info.Ip,
                                            MacAddress = info.Mac,
                                            OsVersion = info.Os,
                                            MachineIdentifier = info.Mid
                                        });
                                    }
                                }
                            }
                            adOu.HostCount = adOu.CandidateHosts.Count;
                            result.Add(adOu);
                        }
                        if (result.Count > 0) return result;
                    }
                }
                catch
                {
                    // Fall back to built-in list
                }
            }
        }

        return new List<AdOrganizationalUnit>
        {
            new()
            {
                OuPath = "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp",
                Name = "Robotics",
                VlanId = 10,
                VlanName = "VLAN 10 - Production Line",
                Subnet = "10.10.10.0/24",
                Location = "Line 06 - Hall A",
                Purpose = "Robotic Pick & Place / Handling",
                MachineType = "Manipulator",
                CandidateHosts = new List<AdCandidateHost>
                {
                    new() { Hostname = "CPC-L06-ROB-01", Name = "Kuka Robot Controller 01", IpAddress = "10.10.10.11", MacAddress = "00:1A:2B:3C:4D:11", OsVersion = "Windows 10 IoT Enterprise LTSC", MachineIdentifier = "HW-ROB-901" },
                    new() { Hostname = "CPC-L06-ROB-02", Name = "Fanuc Handling Robot 02", IpAddress = "10.10.10.12", MacAddress = "00:1A:2B:3C:4D:12", OsVersion = "Windows 10 IoT Enterprise LTSC", MachineIdentifier = "HW-ROB-902" }
                }
            },
            new()
            {
                OuPath = "OU=Fastening,OU=VLAN50-Joining,DC=factory,DC=corp",
                Name = "Fastening",
                VlanId = 50,
                VlanName = "VLAN 50 - Joining & Fastening",
                Subnet = "10.10.50.0/24",
                Location = "Line 06 - Cell A",
                Purpose = "Automated Nutrunning & Screwing",
                MachineType = "Screwing Station",
                CandidateHosts = new List<AdCandidateHost>
                {
                    new() { Hostname = "CPC-L06-SCR-01", Name = "Atlas Copco Fastening Controller 01", IpAddress = "10.10.50.21", MacAddress = "00:1A:2B:3C:4D:21", OsVersion = "Windows 11 IoT Enterprise", MachineIdentifier = "HW-SCR-501" },
                    new() { Hostname = "CPC-L06-SCR-02", Name = "Atlas Copco Fastening Controller 02", IpAddress = "10.10.50.22", MacAddress = "00:1A:2B:3C:4D:22", OsVersion = "Windows 11 IoT Enterprise", MachineIdentifier = "HW-SCR-502" }
                }
            },
            new()
            {
                OuPath = "OU=AOI-Vision,OU=VLAN20-Inspection,DC=factory,DC=corp",
                Name = "AOI-Vision",
                VlanId = 20,
                VlanName = "VLAN 20 - Optical Quality Inspection",
                Subnet = "10.10.20.0/24",
                Location = "Line 06 - End of Line",
                Purpose = "Cognex AOI Defect Inspection",
                MachineType = "Automatic Optical Inspection",
                CandidateHosts = new List<AdCandidateHost>
                {
                    new() { Hostname = "CPC-L06-AOI-01", Name = "Cognex VisionPro IPC 01", IpAddress = "10.10.20.31", MacAddress = "00:1A:2B:3C:4D:31", OsVersion = "Ubuntu 24.04 LTS", MachineIdentifier = "HW-AOI-201" },
                    new() { Hostname = "CPC-L06-AOI-02", Name = "Cognex VisionPro IPC 02", IpAddress = "10.10.20.32", MacAddress = "00:1A:2B:3C:4D:32", OsVersion = "Ubuntu 24.04 LTS", MachineIdentifier = "HW-AOI-202" }
                }
            },
            new()
            {
                OuPath = "OU=Milling,OU=VLAN30-Machining,DC=factory,DC=corp",
                Name = "Milling",
                VlanId = 30,
                VlanName = "VLAN 30 - CNC Machining & Milling",
                Subnet = "10.10.30.0/24",
                Location = "Line 09 - Machining Bay",
                Purpose = "5-Axis CNC Milling",
                MachineType = "Milling",
                CandidateHosts = new List<AdCandidateHost>
                {
                    new() { Hostname = "CPC-L09-CNC-01", Name = "Siemens Sinumerik IPC 01", IpAddress = "10.10.30.41", MacAddress = "00:1A:2B:3C:4D:41", OsVersion = "Windows 10 IoT Enterprise LTSC", MachineIdentifier = "HW-CNC-301" }
                }
            },
            new()
            {
                OuPath = "OU=Dispensing,OU=VLAN40-Chemical,DC=factory,DC=corp",
                Name = "Dispensing",
                VlanId = 40,
                VlanName = "VLAN 40 - Fluid Dispensing",
                Subnet = "10.10.40.0/24",
                Location = "Line 06 - Cell A",
                Purpose = "Polyurethane Gap Filler Dispensing",
                MachineType = "Gap Filler",
                CandidateHosts = new List<AdCandidateHost>
                {
                    new() { Hostname = "CPC-L06-DISP-01", Name = "Scheugenpflug Dispenser Controller", IpAddress = "10.10.40.51", MacAddress = "00:1A:2B:3C:4D:51", OsVersion = "Windows 10 IoT Enterprise LTSC", MachineIdentifier = "HW-DISP-401" }
                }
            }
        };
    }
}

public class AdOrganizationalUnit
{
    public string OuPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int VlanId { get; set; }
    public string VlanName { get; set; } = string.Empty;
    public string Subnet { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string MachineType { get; set; } = string.Empty;
    public int HostCount { get; set; }
    public List<AdCandidateHost> CandidateHosts { get; set; } = new();
}

public class AdCandidateHost
{
    public string Hostname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string MachineIdentifier { get; set; } = string.Empty;
    public bool AlreadyImported { get; set; }
}

public class TagTemplateRule
{
    public string KeyTemplate { get; set; } = string.Empty;
    public string ValueTemplate { get; set; } = string.Empty;
}

public class AdImportPreviewRequest
{
    public List<string>? SelectedOuPaths { get; set; }
    public string? NamingPattern { get; set; }
    public Dictionary<string, string>? TagTemplates { get; set; }
    public List<TagTemplateRule>? TagRules { get; set; }
}

public class AdImportPreviewResponse
{
    public int TotalFound { get; set; }
    public int SelectedOusCount { get; set; }
    public List<AdHostPreviewItem> Preview { get; set; } = new();
}

public class AdHostPreviewItem
{
    public string Hostname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MachineIdentifier { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public int VlanId { get; set; }
    public string VlanName { get; set; } = string.Empty;
    public string Subnet { get; set; } = string.Empty;
    public string AdOuPath { get; set; } = string.Empty;
    public Dictionary<string, string> OuTags { get; set; } = new();
}

public class AdHostImportRequest
{
    public List<AdHostPreviewItem> Hosts { get; set; } = new();
}
