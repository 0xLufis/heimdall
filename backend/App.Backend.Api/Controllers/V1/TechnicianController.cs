namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class TechnicianCandidateDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = HeimdallRoles.Technician;
    public string Department { get; set; } = "Plant Maintenance";
    public string Specialization { get; set; } = "General";
    public bool IsOutOfOffice { get; set; }
    public int AssignedRulesCount { get; set; }
}

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TechnicianController : ControllerBase
{
    private readonly ITechnicianRepository _repository;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public TechnicianController(ITechnicianRepository repository, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _repository = repository;
        _dbContextFactory = dbContextFactory;
    }

    // --- Rules Endpoints ---

    [HttpGet("rules")]
    public async Task<ActionResult<List<TechnicianRule>>> GetRules(CancellationToken cancellationToken)
    {
        var rules = await _repository.GetRulesAsync(cancellationToken);
        return Ok(rules);
    }

    [HttpGet("rules/{id}")]
    public async Task<ActionResult<TechnicianRule>> GetRuleById(Guid id, CancellationToken cancellationToken)
    {
        var rule = await _repository.GetRuleByIdAsync(id, cancellationToken);
        if (rule == null) return NotFound();
        return Ok(rule);
    }

    [HttpPost("rules")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<ActionResult<TechnicianRule>> CreateRule([FromBody] TechnicianRule rule, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(rule.TechnicianId))
        {
            var userExists = await db.AuthUsers.AnyAsync(u => u.Id == rule.TechnicianId, cancellationToken);
            if (!userExists)
            {
                db.AuthUsers.Add(new AuthUser
                {
                    Id = rule.TechnicianId,
                    Name = !string.IsNullOrWhiteSpace(rule.TechnicianName) ? rule.TechnicianName : rule.TechnicianId,
                    Email = !string.IsNullOrWhiteSpace(rule.TechnicianEmail) ? rule.TechnicianEmail : $"{rule.TechnicianId.ToLowerInvariant()}@factory.corp",
                    Role = rule.AssignedByRole ?? HeimdallRoles.Technician
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        if (!string.IsNullOrWhiteSpace(rule.BackupTechnicianId))
        {
            var backupExists = await db.AuthUsers.AnyAsync(u => u.Id == rule.BackupTechnicianId, cancellationToken);
            if (!backupExists)
            {
                db.AuthUsers.Add(new AuthUser
                {
                    Id = rule.BackupTechnicianId,
                    Name = !string.IsNullOrWhiteSpace(rule.BackupTechnicianName) ? rule.BackupTechnicianName : rule.BackupTechnicianId,
                    Email = $"{rule.BackupTechnicianId.ToLowerInvariant()}@factory.corp",
                    Role = HeimdallRoles.Technician
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var created = await _repository.CreateRuleAsync(rule, cancellationToken);
        return CreatedAtAction(nameof(GetRuleById), new { id = created.Id }, created);
    }

    [HttpPut("rules/{id}")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<ActionResult<TechnicianRule>> UpdateRule(Guid id, [FromBody] TechnicianRule updates, CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateRuleAsync(id, updates, cancellationToken);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("rules/{id}")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken cancellationToken)
    {
        var success = await _repository.DeleteRuleAsync(id, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }

    // --- Absences Endpoints ---

    [HttpGet("absences")]
    public async Task<ActionResult<List<ShiftAbsence>>> GetAbsences(CancellationToken cancellationToken)
    {
        var absences = await _repository.GetAbsencesAsync(cancellationToken);
        return Ok(absences);
    }

    [HttpGet("absences/{id}")]
    public async Task<ActionResult<ShiftAbsence>> GetAbsenceById(Guid id, CancellationToken cancellationToken)
    {
        var absence = await _repository.GetAbsenceByIdAsync(id, cancellationToken);
        if (absence == null) return NotFound();
        return Ok(absence);
    }

    [HttpPost("absences")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<ActionResult<ShiftAbsence>> CreateAbsence([FromBody] ShiftAbsence absence, CancellationToken cancellationToken)
    {
        var created = await _repository.CreateAbsenceAsync(absence, cancellationToken);
        return CreatedAtAction(nameof(GetAbsenceById), new { id = created.Id }, created);
    }

    [HttpPut("absences/{id}")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<ActionResult<ShiftAbsence>> UpdateAbsence(Guid id, [FromBody] ShiftAbsence updates, CancellationToken cancellationToken)
    {
        var updated = await _repository.UpdateAbsenceAsync(id, updates, cancellationToken);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("absences/{id}")]
    [Authorize(Policy = "MaintenanceOperations")]
    public async Task<IActionResult> DeleteAbsence(Guid id, CancellationToken cancellationToken)
    {
        var success = await _repository.DeleteAbsenceAsync(id, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }

    // --- Candidates Endpoint ---

    [HttpGet("candidates")]
    public async Task<ActionResult<List<TechnicianCandidateDto>>> GetCandidates(
        [FromQuery] string? role,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.AuthUsers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var rules = await _repository.GetRulesAsync(cancellationToken);
        var absences = await _repository.GetAbsencesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var candidates = users.Select(u =>
        {
            var isOoo = absences.Any(a => a.Active && a.TechnicianId == u.Id && a.StartDate <= now && a.EndDate >= now);
            var assignedCount = rules.Count(r => r.TechnicianId == u.Id);

            return new TechnicianCandidateDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role ?? HeimdallRoles.Technician,
                Department = "Plant Maintenance",
                Specialization = "Robotics & Controls",
                IsOutOfOffice = isOoo,
                AssignedRulesCount = assignedCount
            };
        }).ToList();

        // If no users in DB (e.g. fresh environment), provision default candidate fleet as Better-Auth users
        if (candidates.Count == 0)
        {
            var defaultTechs = new List<AuthUser>
            {
                new() { Id = "tech-01", Name = "Kovács István", Email = "i.kovacs@heimdall.local", Role = HeimdallRoles.Technician },
                new() { Id = "tech-02", Name = "Nagy Péter", Email = "p.nagy@heimdall.local", Role = HeimdallRoles.Technician },
                new() { Id = "tech-03", Name = "Szabó Tamás", Email = "t.szabo@heimdall.local", Role = HeimdallRoles.ControlsEngineer },
                new() { Id = "tech-04", Name = "Varga Zoltán", Email = "z.varga@heimdall.local", Role = HeimdallRoles.Engineer },
                new() { Id = "tech-05", Name = "Tóth Bence", Email = "b.toth@heimdall.local", Role = HeimdallRoles.LeadEngineer }
            };
            db.AuthUsers.AddRange(defaultTechs);
            await db.SaveChangesAsync(cancellationToken);

            candidates = defaultTechs.Select(u => new TechnicianCandidateDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role ?? HeimdallRoles.Technician,
                Department = "Plant Maintenance",
                Specialization = "Robotics & Controls",
                IsOutOfOffice = false,
                AssignedRulesCount = 0
            }).ToList();
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToLowerInvariant();
            if (r == "technician")
            {
                candidates = candidates.Where(c => c.Role.Equals(HeimdallRoles.Technician, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else if (r == "engineer_technician")
            {
                candidates = candidates.Where(c =>
                    c.Role.Equals(HeimdallRoles.Technician, StringComparison.OrdinalIgnoreCase) ||
                    c.Role.Equals(HeimdallRoles.Engineer, StringComparison.OrdinalIgnoreCase) ||
                    c.Role.Equals(HeimdallRoles.ControlsEngineer, StringComparison.OrdinalIgnoreCase) ||
                    c.Role.Equals(HeimdallRoles.LeadEngineer, StringComparison.OrdinalIgnoreCase) ||
                    c.Role.Equals(HeimdallRoles.GroupLeader, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                candidates = candidates.Where(c => c.Role.Equals(r, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            candidates = candidates.Where(c =>
                c.Name.ToLower().Contains(s) ||
                c.Email.ToLower().Contains(s) ||
                c.Department.ToLower().Contains(s) ||
                c.Specialization.ToLower().Contains(s)).ToList();
        }

        return Ok(candidates);
    }
}
