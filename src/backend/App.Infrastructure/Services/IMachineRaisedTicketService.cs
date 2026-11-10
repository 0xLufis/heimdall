namespace App.Infrastructure.Services;

using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Telemetry;

public interface IMachineRaisedTicketService
{
    /// <summary>
    /// Ingests an automated alarm or error dispatch directly from a machine PLC/controller,
    /// attaches telemetry probes, runs delegation recipe matching, and creates an incident ticket.
    /// </summary>
    Task<MachineRaisedTicketResponseDto> CreateMachineRaisedTicketAsync(
        MachineRaisedTicketRequestDto request,
        CancellationToken cancellationToken = default);
}
