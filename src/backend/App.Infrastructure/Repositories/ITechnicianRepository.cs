namespace App.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;

public interface ITechnicianRepository
{
    Task<List<TechnicianRule>> GetRulesAsync(CancellationToken cancellationToken = default);
    Task<TechnicianRule?> GetRuleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TechnicianRule> CreateRuleAsync(TechnicianRule rule, CancellationToken cancellationToken = default);
    Task<TechnicianRule?> UpdateRuleAsync(Guid id, TechnicianRule updates, CancellationToken cancellationToken = default);
    Task<bool> DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<ShiftAbsence>> GetAbsencesAsync(CancellationToken cancellationToken = default);
    Task<ShiftAbsence?> GetAbsenceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShiftAbsence> CreateAbsenceAsync(ShiftAbsence absence, CancellationToken cancellationToken = default);
    Task<ShiftAbsence?> UpdateAbsenceAsync(Guid id, ShiftAbsence updates, CancellationToken cancellationToken = default);
    Task<bool> DeleteAbsenceAsync(Guid id, CancellationToken cancellationToken = default);
}
