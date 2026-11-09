using App.Shared.Entities;

namespace App.Infrastructure.Repositories;

public interface IStationRepository
{
    Task<List<Machine>> GetAllAsync();
    Task<Machine?> GetByIdAsync(Guid id);
    Task<Machine> CreateAsync(Machine machine);
    Task<Machine?> UpdateAsync(
        Guid id,
        string? name,
        string? customIdentifier,
        string? pinnedObjectHandle,
        string? organizationId,
        List<Guid>? controllerIds,
        string? spoc = null,
        string? spocUserId = null,
        string? owner = null,
        string? ownerUserId = null,
        Guid? subLineGroupId = null,
        Guid? lineGroupId = null,
        Guid? projectGroupId = null,
        Guid? businessUnitGroupId = null,
        Guid? parentMachineId = null,
        int? subMachineIndex = null,
        string? cellType = null);
    Task<Machine?> UpdateStatusAsync(Guid id, string status);
    Task<Machine?> DecommissionAsync(Guid id, string reason, string targetDisposition, string partsHandling, DateTimeOffset? date);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetCountAsync();
    Task<List<Machine>> GetSubMachinesAsync(Guid parentMachineId);
    Task<Machine?> AddSubMachineAsync(Guid parentMachineId, Machine subMachine);
    Task<bool> RemoveSubMachineAsync(Guid parentMachineId, Guid subMachineId);
}
