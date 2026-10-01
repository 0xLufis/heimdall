using App.Shared.Entities;

namespace App.Infrastructure.Repositories;

public interface IStationRepository
{
    Task<List<Machine>> GetAllAsync();
    Task<Machine?> GetByIdAsync(Guid id);
    Task<Machine> CreateAsync(Machine machine);
    Task<Machine?> UpdateAsync(Guid id, string? name, string? customIdentifier, string? pinnedObjectHandle, string? organizationId, List<Guid>? controllerIds);
    Task<Machine?> UpdateStatusAsync(Guid id, string status);
    Task<Machine?> DecommissionAsync(Guid id, string reason, string targetDisposition, string partsHandling, DateTimeOffset? date);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetCountAsync();
}
