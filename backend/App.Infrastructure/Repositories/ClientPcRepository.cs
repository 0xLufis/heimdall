using App.Shared.Data;

namespace App.Infrastructure.Repositories;

public class ClientPcRepository : ControllerRepository, IClientPcRepository
{
    public ClientPcRepository(AppDbContext context) : base(context)
    {
    }
}
