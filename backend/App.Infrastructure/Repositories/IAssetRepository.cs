using App.Shared.Entities;

namespace App.Infrastructure.Repositories;

public interface IAssetRepository
{
    Task<List<BaseInventoryItem>> GetInventoryTreeAsync();
    Task<BaseInventoryItem?> GetByIdAsync(Guid id);
    Task<List<string>> GetSearchKeysAsync();
    Task<List<BaseInventoryItem>> SearchAsync(string? query, int limit);
    Task<List<ResponsibleTeam>> GetTeamsAsync();
    Task<List<Manufacturer>> GetManufacturersAsync();
    Task<List<Supplier>> GetSuppliersAsync();
    Task<List<Machine>> GetMachinesAsync();
    Task<List<ClientPc>> GetClientPcsAsync();
    Task<BaseInventoryItem> CreateAsync(BaseInventoryItem item);
    Task<Manufacturer> GetOrCreateManufacturerAsync(string nameOrId);
    Task<Supplier> GetOrCreateSupplierAsync(string nameOrId);
    Task<bool> DeleteAsync(Guid id);
    Task<int> GetCountAsync();
    Task<int> GetAuthUsersCountAsync();
}
