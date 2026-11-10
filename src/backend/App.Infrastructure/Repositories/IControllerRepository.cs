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
    Task<DiagnosticSnapshot> CreateDiagnosticSnapshotAsync(Guid clientPcId, string userId, string? userName, string? orgId);
    Task<List<DiagnosticSnapshot>> GetSnapshotsByClientPcIdAsync(Guid clientPcId, int limit = 20);
    Task<DiagnosticSnapshot?> GetSnapshotByIdAsync(Guid snapshotId);
}
