namespace App.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

public class TechnicianRepository : ITechnicianRepository
{
    private readonly AppDbContext _context;

    public TechnicianRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TechnicianRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.TechnicianRules
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<TechnicianRule?> GetRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TechnicianRules
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<TechnicianRule> CreateRuleAsync(TechnicianRule rule, CancellationToken cancellationToken = default)
    {
        if (rule.Id == Guid.Empty)
            rule.Id = Guid.NewGuid();
        rule.CreatedAt = DateTimeOffset.UtcNow;

        _context.TechnicianRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);
        return rule;
    }

    public async Task<TechnicianRule?> UpdateRuleAsync(Guid id, TechnicianRule updates, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TechnicianRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (existing == null) return null;

        existing.Name = updates.Name;
        existing.TechnicianId = updates.TechnicianId;
        existing.TechnicianName = updates.TechnicianName;
        existing.TechnicianEmail = updates.TechnicianEmail;
        existing.ScopeType = updates.ScopeType;
        existing.TargetId = updates.TargetId;
        existing.CategoryFilter = updates.CategoryFilter;
        existing.BackupTechnicianId = updates.BackupTechnicianId;
        existing.BackupTechnicianName = updates.BackupTechnicianName;
        existing.AssignedByRole = updates.AssignedByRole;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TechnicianRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (existing == null) return false;

        _context.TechnicianRules.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<ShiftAbsence>> GetAbsencesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ShiftAbsences
            .AsNoTracking()
            .OrderByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<ShiftAbsence?> GetAbsenceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ShiftAbsences
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<ShiftAbsence> CreateAbsenceAsync(ShiftAbsence absence, CancellationToken cancellationToken = default)
    {
        if (absence.Id == Guid.Empty)
            absence.Id = Guid.NewGuid();

        _context.ShiftAbsences.Add(absence);
        await _context.SaveChangesAsync(cancellationToken);
        return absence;
    }

    public async Task<ShiftAbsence?> UpdateAbsenceAsync(Guid id, ShiftAbsence updates, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ShiftAbsences.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (existing == null) return null;

        existing.TechnicianId = updates.TechnicianId;
        existing.TechnicianName = updates.TechnicianName;
        existing.Reason = updates.Reason;
        existing.StartDate = updates.StartDate;
        existing.EndDate = updates.EndDate;
        existing.MarkedBy = updates.MarkedBy;
        existing.BackupTechnicianId = updates.BackupTechnicianId;
        existing.BackupTechnicianName = updates.BackupTechnicianName;
        existing.Active = updates.Active;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAbsenceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ShiftAbsences.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (existing == null) return false;

        _context.ShiftAbsences.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
