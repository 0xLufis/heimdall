using App.Infrastructure.Repositories;
using App.Contracts.Security;
using App.Infrastructure.Services;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for managing Machine (Station) entities using repository interfaces.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MachineController : ControllerBase
{
    private readonly IStationRepository _stationRepository;
    private readonly AppDbContext? _context;
    private readonly IGenericTemplateService? _templateService;
    private readonly IAuthorizationService? _authorizationService;

    public MachineController(
        IStationRepository stationRepository,
        AppDbContext? context = null,
        IGenericTemplateService? templateService = null,
        IAuthorizationService? authorizationService = null)
    {
        _stationRepository = stationRepository;
        _context = context;
        _templateService = templateService;
        _authorizationService = authorizationService;
    }

    /// <summary>Resolves a machine id from its custom identifier or name without loading the whole fleet.</summary>
    private async Task<Guid?> FindMachineIdByIdentifierAsync(string id)
    {
        if (_context != null)
        {
            return await _context.Machines.AsNoTracking()
                .Where(m => m.CustomIdentifier == id || m.Name == id)
                .Select(m => (Guid?)m.Id)
                .FirstOrDefaultAsync();
        }
        var all = await _stationRepository.GetAllAsync();
        return all.FirstOrDefault(m => m.CustomIdentifier == id || m.Name == id)?.Id;
    }

    /// <summary>
    /// The caller's organization (Better-Auth active organization, exposed as the "OrgId" claim).
    /// Only platform administrators may act on behalf of a different organization.
    /// </summary>
    private async Task<(string? OrgId, bool Allowed)> ResolveTargetOrganizationAsync(string? requestedOrgId)
    {
        var callerOrg = User?.FindFirst("OrgId")?.Value;
        if (string.IsNullOrWhiteSpace(requestedOrgId) || string.Equals(requestedOrgId, callerOrg, StringComparison.Ordinal))
            return (callerOrg ?? "default", true);

        var isPlatformAdmin = _authorizationService != null && User != null &&
            (await _authorizationService.AuthorizeAsync(User, AuthorizationPolicies.SystemAdministration)).Succeeded;
        return (requestedOrgId, isPlatformAdmin);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<App.Backend.Api.Dtos.MachineDto>>> GetMachines()
    {
        var machines = await _stationRepository.GetAllAsync();
        var dtos = machines.Select(m => new App.Backend.Api.Dtos.MachineDto
        {
            Id = m.Id,
            Name = m.Name,
            DisplayName = m.DisplayName,
            OrganizationId = m.OrganizationId ?? "Heimdall Root",
            CustomIdentifier = m.CustomIdentifier,
            MachineType = m.MachineType,
            GroupId = m.GroupId,
            PreferredTechnicianId = m.PreferredTechnicianId,
            PreferredTechnicianName = m.PreferredTechnicianName,
            PinnedObjectHandle = m.PinnedObjectHandle,
            Status = m.Status ?? "active",
            DecommissionReason = m.DecommissionReason,
            DecommissionDate = m.DecommissionDate,
            DecommissionDisposition = m.DecommissionDisposition,
            Spoc = m.Spoc,
            SpocUserId = m.SpocUserId,
            Owner = m.Owner,
            OwnerUserId = m.OwnerUserId,
            SubLineGroupId = m.SubLineGroupId,
            SubLineGroupName = m.SubLineGroup?.Name,
            LineGroupId = m.LineGroupId,
            LineGroupName = m.LineGroup?.Name,
            ProjectGroupId = m.ProjectGroupId,
            ProjectGroupName = m.ProjectGroup?.Name,
            BusinessUnitGroupId = m.BusinessUnitGroupId,
            BusinessUnitGroupName = m.BusinessUnitGroup?.Name,
            ParentMachineId = m.ParentMachineId,
            ParentMachineName = m.ParentMachine?.Name,
            SubMachineIndex = m.SubMachineIndex,
            CellType = m.CellType,
            IsCellOrRoundtable = m.IsCellOrRoundtable,
            SubMachines = m.SubMachines.Select(sm => new App.Backend.Api.Dtos.SubMachineSummaryDto
            {
                Id = sm.Id,
                Name = sm.Name,
                DisplayName = sm.DisplayName,
                CustomIdentifier = sm.CustomIdentifier,
                SubMachineIndex = sm.SubMachineIndex,
                CellType = sm.CellType,
                MachineType = sm.MachineType,
                Status = sm.Status ?? "active",
                Spoc = sm.Spoc,
                Owner = sm.Owner,
                PreferredTechnicianName = sm.PreferredTechnicianName
            }).ToList(),
            Controllers = m.Controllers.Select(c => new App.Backend.Api.Dtos.ClientPcSummaryDto
            {
                Id = c.Id,
                Hostname = c.Hostname,
                PinnedObjectHandle = c.PinnedObjectHandle,
                Name = c.Name
            }).ToList(),
            ResponsibleTeams = m.ResponsibleTeams.Select(t => new App.Backend.Api.Dtos.TeamSummaryDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList()
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("by-technology")]
    public async Task<IActionResult> GetMachinesByTechnology()
    {
        var machines = await _stationRepository.GetAllAsync();
        var grouped = machines
            .GroupBy(m => string.IsNullOrWhiteSpace(m.MachineType) ? "General Assembly" : m.MachineType)
            .Select(g => new
            {
                Technology = g.Key,
                MachineCount = g.Count(),
                Machines = g.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.DisplayName,
                    m.CustomIdentifier,
                    m.MachineType,
                    m.GroupId,
                    m.Spoc,
                    m.Owner,
                    SubMachinesCount = m.SubMachines.Count,
                    ControllersCount = m.Controllers.Count,
                    Controllers = m.Controllers.Select(c => new { c.Id, c.Hostname, c.IpAddress, c.LastOnline })
                }).ToList()
            })
            .OrderByDescending(g => g.MachineCount)
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("by-line")]
    public async Task<IActionResult> GetMachinesByLine()
    {
        var machines = await _stationRepository.GetAllAsync();
        var grouped = machines
            .GroupBy(m => string.IsNullOrWhiteSpace(m.GroupId) ? (m.LineGroup?.Name ?? "Unassigned Line") : m.GroupId)
            .Select(g => new
            {
                LineId = g.Key,
                LineName = g.Key,
                MachineCount = g.Count(),
                ControllersCount = g.Sum(m => m.Controllers.Count),
                Machines = g.Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.DisplayName,
                    m.CustomIdentifier,
                    m.MachineType,
                    m.GroupId,
                    m.Spoc,
                    m.Owner,
                    SubMachinesCount = m.SubMachines.Count,
                    Controllers = m.Controllers.Select(c => new { c.Id, c.Hostname, c.IpAddress, c.LastOnline })
                }).ToList()
            })
            .OrderBy(g => g.LineName)
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Machine>> GetMachine(string id)
    {
        Machine? machine = Guid.TryParse(id, out var g) ? await _stationRepository.GetByIdAsync(g) : null;
        if (machine == null)
        {
            var byIdentifier = await FindMachineIdByIdentifierAsync(id);
            if (byIdentifier.HasValue) machine = await _stationRepository.GetByIdAsync(byIdentifier.Value);
        }
        if (machine == null) return NotFound();
        return Ok(machine);
    }

    [HttpGet("{id}/sub-machines")]
    public async Task<ActionResult<List<App.Backend.Api.Dtos.SubMachineSummaryDto>>> GetSubMachines(string id)
    {
        var targetGuid = Guid.TryParse(id, out var g) ? g : await FindMachineIdByIdentifierAsync(id);
        if (!targetGuid.HasValue) return Ok(new List<App.Backend.Api.Dtos.SubMachineSummaryDto>());
        var subMachines = await _stationRepository.GetSubMachinesAsync(targetGuid.Value);
        var dtos = subMachines.Select(sm => new App.Backend.Api.Dtos.SubMachineSummaryDto
        {
            Id = sm.Id,
            Name = sm.Name,
            DisplayName = sm.DisplayName,
            CustomIdentifier = sm.CustomIdentifier,
            SubMachineIndex = sm.SubMachineIndex,
            CellType = sm.CellType,
            MachineType = sm.MachineType,
            Status = sm.Status ?? "active",
            Spoc = sm.Spoc,
            Owner = sm.Owner,
            PreferredTechnicianName = sm.PreferredTechnicianName
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("{id}/sub-machines")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<Machine>> AddSubMachine(Guid id, [FromBody] Machine subMachine)
    {
        var added = await _stationRepository.AddSubMachineAsync(id, subMachine);
        if (added == null) return NotFound($"Parent machine '{id}' was not found.");
        return CreatedAtAction(nameof(GetMachine), new { id = added.Id }, added);
    }

    [HttpDelete("{id}/sub-machines/{subMachineId}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> RemoveSubMachine(Guid id, Guid subMachineId)
    {
        var success = await _stationRepository.RemoveSubMachineAsync(id, subMachineId);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<ActionResult<Machine>> CreateMachine(Machine machine)
    {
        // Mass-assignment guard: never persist the client-bound entity graph directly. Only
        // descriptive fields are copied; identity, tenant, lifecycle, audit timestamps and
        // navigation graphs are server-controlled. Hierarchy goes through POST {id}/sub-machines,
        // which performs cycle detection.
        var (orgId, allowed) = await ResolveTargetOrganizationAsync(machine.OrganizationId);
        if (!allowed) return Forbid();
        if (machine.ParentMachineId != null)
            return BadRequest(new { error = "Use POST /api/v1/machine/{parentId}/sub-machines to create child machines." });

        var sanitized = new Machine
        {
            Id = Guid.NewGuid(),
            Name = machine.Name,
            DisplayName = machine.DisplayName,
            OrganizationId = orgId,
            PlantId = machine.PlantId,
            CustomIdentifier = machine.CustomIdentifier,
            Barcode = machine.Barcode,
            SerialNumber = machine.SerialNumber,
            MachineType = machine.MachineType,
            Criticality = machine.Criticality,
            Technologies = machine.Technologies ?? new List<string>(),
            Spoc = machine.Spoc,
            SpocUserId = machine.SpocUserId,
            Owner = machine.Owner,
            OwnerUserId = machine.OwnerUserId,
            GroupId = machine.GroupId,
            SubLineGroupId = machine.SubLineGroupId,
            LineGroupId = machine.LineGroupId,
            ProjectGroupId = machine.ProjectGroupId,
            BusinessUnitGroupId = machine.BusinessUnitGroupId,
            CellType = machine.CellType,
            FloorPlanId = machine.FloorPlanId,
            PinnedObjectHandle = machine.PinnedObjectHandle,
            SpatialTransformJson = machine.SpatialTransformJson,
            UserTags = machine.UserTags ?? new List<string>(),
            CustomFieldsJson = machine.CustomFieldsJson,
            ManufacturerId = machine.ManufacturerId,
            SupplierId = machine.SupplierId,
            PurchaseDate = machine.PurchaseDate,
            CostInHUF = machine.CostInHUF,
            PreferredTechnicianId = machine.PreferredTechnicianId,
            PreferredTechnicianName = machine.PreferredTechnicianName,
            Status = "active",
        };

        var created = await _stationRepository.CreateAsync(sanitized);
        return CreatedAtAction(nameof(GetMachine), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "EndpointConfigManagement")]
    public async Task<IActionResult> UpdateMachine(Guid id, [FromBody] App.Backend.Api.Dtos.MachineUpdateDto update)
    {
        Machine? updated;
        try
        {
            updated = await _stationRepository.UpdateAsync(
            id,
            update.Name,
            update.CustomIdentifier,
            update.PinnedObjectHandle,
            null,
            update.ControllerIds,
            update.Spoc,
            update.SpocUserId,
            update.Owner,
            update.OwnerUserId,
            update.SubLineGroupId,
            update.LineGroupId,
            update.ProjectGroupId,
            update.BusinessUnitGroupId,
            update.ParentMachineId,
            update.SubMachineIndex,
            update.CellType
        );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (updated == null) return NotFound();
        return NoContent();
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = AuthorizationPolicies.MaintenanceOperations)]
    public async Task<IActionResult> UpdateMachineStatus(Guid id, [FromBody] App.Backend.Api.Dtos.MachineStatusUpdateDto update)
    {
        var machine = await _stationRepository.UpdateStatusAsync(id, update.Status);
        if (machine == null) return NotFound();
        return Ok(new { machine.Id, machine.Status });
    }

    [HttpPost("{id}/decommission")]
    [Authorize(Policy = AuthorizationPolicies.EndpointConfigManagement)]
    public async Task<IActionResult> DecommissionMachine(Guid id, [FromBody] App.Backend.Api.Dtos.MachineDecommissionDto decommission)
    {
        var machine = await _stationRepository.DecommissionAsync(
            id,
            decommission.Reason,
            decommission.TargetDisposition,
            decommission.PartsHandling,
            decommission.DecommissionDate
        );

        if (machine == null) return NotFound();
        return Ok(new
        {
            machine.Id,
            machine.Status,
            machine.DecommissionReason,
            machine.DecommissionDate,
            machine.DecommissionDisposition
        });
    }

    [HttpGet("organization-types")]
    public async Task<IActionResult> GetOrganizationMachineTypes([FromQuery] string? organizationId = null)
    {
        var (orgId, allowed) = await ResolveTargetOrganizationAsync(organizationId);
        if (!allowed) return Forbid();

        if (_templateService != null)
            await _templateService.SeedDefaultTemplatesIfEmptyAsync(HttpContext?.RequestAborted ?? default);
        var allMachineTemplates = _templateService != null
            ? await _templateService.GetTemplatesAsync(new App.Contracts.Templates.TemplateQueryFilterDto { TemplateType = "machine" })
            : new List<App.Contracts.Templates.GenericTemplateDto>();

        var allCategories = allMachineTemplates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();
        if (allCategories.Count == 0)
        {
            allCategories = new List<string> { "SMT", "Inspection", "Soldering", "Depaneling", "Dispensing", "Fastening", "Testing", "Assembly", "Robotics" };
        }

        List<string> enabledCategories = new();
        if (_context != null)
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == $"OrgMachineTypes_{orgId}" || s.Category == $"OrgMachineTypes_{orgId}");
            if (setting != null && !string.IsNullOrWhiteSpace(setting.ValueJson))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(setting.ValueJson);
                    if (parsed != null) enabledCategories = parsed;
                }
                catch { }
            }
        }

        if (enabledCategories.Count == 0)
        {
            // Default: all categories enabled
            enabledCategories = new List<string>(allCategories);
        }

        return Ok(new
        {
            OrganizationId = orgId,
            AllCategories = allCategories,
            EnabledCategories = enabledCategories,
            Templates = allMachineTemplates.Select(t => new
            {
                t.Id,
                t.Code,
                t.Name,
                t.Category,
                t.Icon,
                IsEnabled = enabledCategories.Contains(t.Category, StringComparer.OrdinalIgnoreCase)
            }).ToList()
        });
    }

    [HttpPut("organization-types")]
    [Authorize(Policy = AuthorizationPolicies.FunctionalSettingsManagement)]
    public async Task<IActionResult> UpdateOrganizationMachineTypes([FromBody] App.Contracts.Templates.OrganizationMachineTypeFilterDto dto)
    {
        var (orgId, allowed) = await ResolveTargetOrganizationAsync(dto.OrganizationId);
        if (!allowed) return Forbid();

        if (_context == null) return Ok(new { success = true, enabledCategories = dto.EnabledCategories });

        var key = $"OrgMachineTypes_{orgId}";
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        var json = System.Text.Json.JsonSerializer.Serialize(dto.EnabledCategories);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Category = key,
                ValueJson = json,
                UpdatedBy = User?.Identity?.Name ?? "system",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.ValueJson = json;
            setting.UpdatedBy = User?.Identity?.Name ?? "system";
            setting.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, organizationId = orgId, enabledCategories = dto.EnabledCategories });
    }
}
