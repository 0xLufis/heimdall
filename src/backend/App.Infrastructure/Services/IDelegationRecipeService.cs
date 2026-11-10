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
    /// Resolves the responsible personnel/crew for a machine under a given discipline, technology, and schedule context.
    /// Prioritizes technology dedication primarily while supporting complete machine dedications.
    /// </summary>
    Task<ResolvedPersonnelDto> ResolvePersonnelForMachineAsync(
        Guid machineId,
        DelegationDiscipline discipline = DelegationDiscipline.General,
        WorkerScheduleType scheduleType = WorkerScheduleType.Shift,
        string? technology = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all delegation recipes bound to a specific machine.
    /// </summary>
    Task<List<DelegationRecipeDto>> GetRecipesForMachineAsync(Guid machineId, CancellationToken cancellationToken = default);
    Task<List<DelegationRecipeDto>> GetRecipesForMachineAsync(string machineIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all delegation recipes applicable to a machine across its hierarchy (Sub-Line, Line, Project, Business Unit) and direct bindings.
    /// </summary>
    Task<List<DelegationRecipeDto>> GetRecipesApplicableToMachineAsync(Guid machineId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Deletes a delegation recipe together with its machine bindings and group junction rows.
    /// Returns false when the recipe does not exist (or is outside the caller's tenant scope).
    /// </summary>
    Task<bool> DeleteRecipeAsync(Guid recipeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a personnel group and its memberships. Refuses (<see cref="PersonnelGroupDeleteResult.InUse"/>)
    /// while any delegation recipe still routes work to the group, so on-call routing is never silently dropped.
    /// </summary>
    Task<PersonnelGroupDeleteResult> DeletePersonnelGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all direct, inherited, and automatic technological dedications for a machine.
    /// </summary>
    Task<MachineDedicationsDto> GetMachineDedicationsAsync(Guid machineId, CancellationToken cancellationToken = default);
    Task<MachineDedicationsDto> GetMachineDedicationsAsync(string machineIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Directly dedicates a person or group to a machine.
    /// </summary>
    Task<MachineDedicationsDto> DedicateToMachineAsync(Guid machineId, DedicationAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<MachineDedicationsDto> DedicateToMachineAsync(string machineIdentifier, DedicationAssignmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the direct dedication from a machine (or a specific technology/target dedication).
    /// </summary>
    Task<bool> RemoveMachineDedicationAsync(Guid machineId, string? targetId = null, string? technology = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveMachineDedicationAsync(string machineIdentifier, string? targetId = null, string? technology = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all machine dedications, machine groups, and technological coverages for a specific user (vice-versa).
    /// </summary>
    Task<UserDedicationsDto> GetUserDedicationsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dedicates a user to a machine (vice-versa action).
    /// </summary>
    Task<bool> DedicateUserToMachineAsync(string userId, Guid machineId, string? technology = null, CancellationToken cancellationToken = default);
    Task<bool> DedicateUserToMachineAsync(string userId, string machineIdentifier, string? technology = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user dedication from a machine.
    /// </summary>
    Task<bool> RemoveUserDedicationFromMachineAsync(string userId, Guid machineId, string? technology = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveUserDedicationFromMachineAsync(string userId, string machineIdentifier, string? technology = null, CancellationToken cancellationToken = default);
}

public enum PersonnelGroupDeleteResult
{
    Deleted,
    NotFound,
    InUse
}
