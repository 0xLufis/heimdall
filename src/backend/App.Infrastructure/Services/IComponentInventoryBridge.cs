namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Machine;

public interface IComponentInventoryBridge
{
    /// <summary>
    /// Assigns a physical/logical inventory item to an existing component slot (recording part history)
    /// or instantiates a new component slot on the machine.
    /// </summary>
    Task<ComponentPartAssignmentResult> AssignPartAsync(AssignPartToComponentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves immutable lineage swap history for a component slot.
    /// </summary>
    Task<List<ComponentPartHistoryDto>> GetComponentPartHistoryAsync(Guid componentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single component slot by ID.
    /// </summary>
    Task<MachineComponentDto?> GetComponentByIdAsync(Guid componentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all component slots installed on a machine.
    /// </summary>
    Task<List<MachineComponentDto>> GetMachineComponentsAsync(Guid machineId, CancellationToken cancellationToken = default);
}
