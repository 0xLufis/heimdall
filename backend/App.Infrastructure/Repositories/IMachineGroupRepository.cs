namespace App.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;

public interface IMachineGroupRepository
{
    Task<List<MachineGroup>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MachineGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MachineGroup> CreateAsync(MachineGroup group, CancellationToken cancellationToken = default);
    Task<MachineGroup?> UpdateAsync(Guid id, MachineGroup updates, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
