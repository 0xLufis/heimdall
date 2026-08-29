using App.Shared.Entities;

namespace App.Infrastructure.Repositories;

public interface IControllerRepository
{
    Task<List<ClientPc>> GetAllAsync();
    Task<ClientPc?> GetByIdAsync(Guid id);
    Task<ClientPc> CreateAsync(ClientPc pc);
    Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc);
    Task<ClientPc?> UpdateAsync(Guid id, string? name, string? hostname, string? macAddress, string? pinnedObjectHandle, List<Guid>? controlledMachineIds);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetCountAsync();
    Task<int> GetActiveCountAsync(TimeSpan activeThreshold);
    Task<List<ClientPc>> GetRecentClientsAsync(int count);
}
