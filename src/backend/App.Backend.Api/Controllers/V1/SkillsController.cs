namespace App.Backend.Api.Controllers.V1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Contracts.Enums;
using App.Contracts.Personnel;
using App.Contracts.Security;
using App.Shared.Data;
using App.Shared.Entities;

public record AssignSkillRequest(
    string UserId,
    Guid SkillId,
    string? CertificateNumber = null,
    string? VerifiedBy = null,
    DateTimeOffset? ExpiresAt = null
);

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SkillsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SkillsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<List<SkillDefinitionDto>>> GetSkillCatalog()
    {
        var skills = await _context.SkillDefinitions
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .Select(s => new SkillDefinitionDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Description = s.Description,
                Category = s.Category,
                Issuer = s.Issuer,
                ValidityMonths = s.ValidityMonths,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return Ok(skills);
    }

    [HttpPost("catalog")]
    [Authorize(Policy = AuthorizationPolicies.SkillsManagement)]
    public async Task<ActionResult<SkillDefinitionDto>> CreateSkillDefinition([FromBody] SkillDefinitionDto dto)
    {
        var exists = await _context.SkillDefinitions.AnyAsync(s => s.Code.ToLower() == dto.Code.ToLower());
        if (exists)
        {
            return Conflict(new { error = $"Skill code '{dto.Code}' already exists." });
        }

        var skill = new SkillDefinition
        {
            Id = dto.Id != Guid.Empty ? dto.Id : Guid.NewGuid(),
            Code = dto.Code.ToUpperInvariant().Trim(),
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            Issuer = dto.Issuer,
            ValidityMonths = dto.ValidityMonths,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.SkillDefinitions.Add(skill);
        await _context.SaveChangesAsync();

        dto.Id = skill.Id;
        dto.CreatedAt = skill.CreatedAt;
        return CreatedAtAction(nameof(GetSkillCatalog), new { id = skill.Id }, dto);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<PersonnelSkillDto>>> GetUserSkills(string userId)
    {
        var skills = await _context.PersonnelSkills
            .AsNoTracking()
            .Include(ps => ps.Skill)
            .Where(ps => ps.UserId == userId)
            .OrderByDescending(ps => ps.CertifiedAt)
            .Select(ps => new PersonnelSkillDto
            {
                Id = ps.Id,
                UserId = ps.UserId,
                SkillId = ps.SkillId,
                SkillCode = ps.Skill != null ? ps.Skill.Code : string.Empty,
                SkillName = ps.Skill != null ? ps.Skill.Name : string.Empty,
                Category = ps.Skill != null ? ps.Skill.Category : SkillCategory.Certification,
                CertifiedAt = ps.CertifiedAt,
                ExpiresAt = ps.ExpiresAt,
                CertificateNumber = ps.CertificateNumber,
                VerifiedBy = ps.VerifiedBy
            })
            .ToListAsync();

        return Ok(skills);
    }

    [HttpPost("user")]
    [Authorize(Policy = AuthorizationPolicies.SkillsManagement)]
    public async Task<ActionResult<PersonnelSkillDto>> AssignSkillToUser([FromBody] AssignSkillRequest request)
    {
        var skill = await _context.SkillDefinitions.FirstOrDefaultAsync(s => s.Id == request.SkillId);
        if (skill == null) return NotFound(new { error = $"Skill definition with ID '{request.SkillId}' not found." });

        var existing = await _context.PersonnelSkills
            .FirstOrDefaultAsync(ps => ps.UserId == request.UserId && ps.SkillId == request.SkillId);

        if (existing != null)
        {
            existing.CertifiedAt = DateTimeOffset.UtcNow;
            existing.ExpiresAt = request.ExpiresAt ?? (skill.ValidityMonths.HasValue ? DateTimeOffset.UtcNow.AddMonths(skill.ValidityMonths.Value) : null);
            existing.CertificateNumber = request.CertificateNumber ?? existing.CertificateNumber;
            existing.VerifiedBy = request.VerifiedBy ?? existing.VerifiedBy;
            await _context.SaveChangesAsync();

            return Ok(new PersonnelSkillDto
            {
                Id = existing.Id,
                UserId = existing.UserId,
                SkillId = existing.SkillId,
                SkillCode = skill.Code,
                SkillName = skill.Name,
                Category = skill.Category,
                CertifiedAt = existing.CertifiedAt,
                ExpiresAt = existing.ExpiresAt,
                CertificateNumber = existing.CertificateNumber,
                VerifiedBy = existing.VerifiedBy
            });
        }

        var expiresAt = request.ExpiresAt ?? (skill.ValidityMonths.HasValue ? DateTimeOffset.UtcNow.AddMonths(skill.ValidityMonths.Value) : null);

        var personnelSkill = new PersonnelSkill
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            SkillId = request.SkillId,
            CertifiedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            CertificateNumber = request.CertificateNumber,
            VerifiedBy = request.VerifiedBy
        };

        _context.PersonnelSkills.Add(personnelSkill);
        await _context.SaveChangesAsync();

        return Ok(new PersonnelSkillDto
        {
            Id = personnelSkill.Id,
            UserId = personnelSkill.UserId,
            SkillId = personnelSkill.SkillId,
            SkillCode = skill.Code,
            SkillName = skill.Name,
            Category = skill.Category,
            CertifiedAt = personnelSkill.CertifiedAt,
            ExpiresAt = personnelSkill.ExpiresAt,
            CertificateNumber = personnelSkill.CertificateNumber,
            VerifiedBy = personnelSkill.VerifiedBy
        });
    }

    [HttpDelete("user/{personnelSkillId}")]
    [Authorize(Policy = AuthorizationPolicies.SkillsManagement)]
    public async Task<ActionResult> RevokeUserSkill(Guid personnelSkillId)
    {
        var ps = await _context.PersonnelSkills.FindAsync(personnelSkillId);
        if (ps == null) return NotFound();

        _context.PersonnelSkills.Remove(ps);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("qualified")]
    public async Task<ActionResult<List<string>>> GetQualifiedUsers([FromQuery] string skillCodes)
    {
        if (string.IsNullOrWhiteSpace(skillCodes)) return Ok(new List<string>());

        var required = skillCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var activeQualifications = await _context.PersonnelSkills
            .AsNoTracking()
            .Include(ps => ps.Skill)
            .Where(ps => (ps.ExpiresAt == null || ps.ExpiresAt > DateTimeOffset.UtcNow) &&
                         required.Contains(ps.Skill!.Code))
            .ToListAsync();

        var qualifiedUserIds = activeQualifications
            .GroupBy(ps => ps.UserId)
            .Where(g => required.All(req => g.Any(s => s.Skill?.Code == req)))
            .Select(g => g.Key)
            .ToList();

        return Ok(qualifiedUserIds);
    }
}
