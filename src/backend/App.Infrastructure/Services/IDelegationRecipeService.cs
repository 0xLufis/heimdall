namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Enums;
using App.Contracts.Personnel;

public interface IDelegationRecipeService
{
    /// <summary>
    /// Resolves the responsible personnel/crew for a machine under a given discipline and schedule context.
    /// </summary>
    Task<ResolvedPersonnelDto> ResolvePersonnelForMachineAsync(
        Guid machineId,
        DelegationDiscipline discipline = DelegationDiscipline.General,
        WorkerScheduleType scheduleType = WorkerScheduleType.Shift,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all delegation recipes bound to a specific machine.
    /// </summary>
    Task<List<DelegationRecipeDto>> GetRecipesForMachineAsync(Guid machineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a delegation recipe by its identifier.
    /// </summary>
    Task<DelegationRecipeDto?> GetRecipeByIdAsync(Guid recipeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all delegation recipes defined in the system.
    /// </summary>
    Task<List<DelegationRecipeDto>> GetAllRecipesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a delegation recipe.
    /// </summary>
    Task<DelegationRecipeDto> CreateOrUpdateRecipeAsync(DelegationRecipeDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Binds a delegation recipe to a machine with a specified discipline.
    /// </summary>
    Task<MachineDelegationBindingDto> BindRecipeToMachineAsync(
        Guid machineId,
        Guid recipeId,
        DelegationDiscipline discipline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a delegation binding from a machine.
    /// </summary>
    Task<bool> UnbindRecipeFromMachineAsync(Guid bindingId, CancellationToken cancellationToken = default);
}
