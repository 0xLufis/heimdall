namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using App.Contracts.Enums;
using App.Contracts.Machine;
using App.Shared.Data;
using App.Shared.Entities;

public class ComponentInventoryBridge : IComponentInventoryBridge
{
    private readonly AppDbContext _context;

    public ComponentInventoryBridge(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ComponentPartAssignmentResult> AssignPartAsync(AssignPartToComponentRequestDto request, CancellationToken cancellationToken = default)
    {
        var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId, cancellationToken);
        if (machine == null)
        {
            return new ComponentPartAssignmentResult
            {
                Success = false,
                Message = $"Target Machine with ID {request.MachineId} not found."
            };
        }

        var part = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == request.PartId, cancellationToken);
        if (part == null)
        {
            return new ComponentPartAssignmentResult
            {
                Success = false,
                Message = $"Inventory Part with ID {request.PartId} not found."
            };
        }

        // Adjust inventory shelf stock
        if (part.IsStockItem)
        {
            part.StockQuantity = Math.Max(0, (part.StockQuantity ?? 1) - 1);
            if (part.StockQuantity == 0)
            {
                part.EquipmentStatus = "Installed";
            }
        }
        else
        {
            part.EquipmentStatus = "Operational";
        }
        part.MachineId = request.MachineId;

        MachineComponent targetComponent;
        bool isNewSlot = false;
        ComponentPartHistory history;

        if (request.ExistingComponentId.HasValue && request.ExistingComponentId.Value != Guid.Empty)
        {
            var existing = await _context.MachineComponents
                .Include(c => c.CurrentPart)
                .FirstOrDefaultAsync(c => c.Id == request.ExistingComponentId.Value, cancellationToken);

            if (existing == null)
            {
                return new ComponentPartAssignmentResult
                {
                    Success = false,
                    Message = $"Existing component slot with ID {request.ExistingComponentId.Value} not found on machine."
                };
            }

            var previousPart = existing.CurrentPart;
            if (previousPart != null)
            {
                if (request.PreviousPartDisposition == PartDisposition.ReturnedToStock)
                {
                    previousPart.MachineId = null;
                    previousPart.IsStockItem = true;
                    previousPart.StockQuantity = (previousPart.StockQuantity ?? 0) + 1;
                    previousPart.EquipmentStatus = "InStock";
                }
                else if (request.PreviousPartDisposition == PartDisposition.Scrapped)
                {
                    previousPart.EquipmentStatus = "Scrapped";
                }
                else if (request.PreviousPartDisposition == PartDisposition.SentForRepair)
                {
                    previousPart.EquipmentStatus = "UnderRepair";
                }
            }

            history = new ComponentPartHistory
            {
                Id = Guid.NewGuid(),
                ComponentId = existing.Id,
                MachineId = request.MachineId,
                SwappedAt = DateTimeOffset.UtcNow,
                SwappedByUserId = request.SwappedByUserId,
                SwappedByUserName = request.SwappedByUserName,
                PreviousPartId = existing.CurrentPartId,
                PreviousPartSerialNumber = existing.CurrentPart?.SerialNumber,
                NewPartId = part.Id,
                NewPartSerialNumber = part.SerialNumber,
                Reason = request.Reason,
                PreviousPartDisposition = request.PreviousPartDisposition,
                AssociatedTicketId = request.AssociatedTicketId,
                Notes = request.Notes
            };

            existing.CurrentPartId = part.Id;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            if (request.EplanCadRef != null)
            {
                existing.EplanCadRefJson = JsonSerializer.Serialize(request.EplanCadRef);
            }

            if (request.AdditionalTags != null && request.AdditionalTags.Count > 0)
            {
                existing.Tags = existing.Tags.Union(request.AdditionalTags).Distinct().ToList();
            }

            _context.ComponentPartHistories.Add(history);
            targetComponent = existing;
        }
        else
        {
            isNewSlot = true;
            var tags = new List<string>();

            if (request.ComponentRecipeId.HasValue)
            {
                var recipe = await _context.ComponentRecipes.FirstOrDefaultAsync(r => r.Id == request.ComponentRecipeId.Value, cancellationToken);
                if (recipe != null && recipe.AutoTags != null)
                {
                    tags.AddRange(recipe.AutoTags);
                }
            }

            if (request.AdditionalTags != null)
            {
                tags.AddRange(request.AdditionalTags);
            }

            string slotId = !string.IsNullOrWhiteSpace(request.NewComponentSlotIdentifier)
                ? request.NewComponentSlotIdentifier
                : $"SLOT-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            targetComponent = new MachineComponent
            {
                Id = Guid.NewGuid(),
                MachineId = request.MachineId,
                Name = !string.IsNullOrWhiteSpace(request.NewComponentName) ? request.NewComponentName : part.Name,
                FunctionalSlotIdentifier = slotId,
                Category = request.Category,
                ComponentRecipeId = request.ComponentRecipeId,
                CurrentPartId = part.Id,
                Tags = tags.Distinct().ToList(),
                InstalledAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                EplanCadRefJson = request.EplanCadRef != null ? JsonSerializer.Serialize(request.EplanCadRef) : null
            };

            history = new ComponentPartHistory
            {
                Id = Guid.NewGuid(),
                ComponentId = targetComponent.Id,
                MachineId = request.MachineId,
                SwappedAt = DateTimeOffset.UtcNow,
                SwappedByUserId = request.SwappedByUserId,
                SwappedByUserName = request.SwappedByUserName,
                PreviousPartId = null,
                PreviousPartSerialNumber = null,
                NewPartId = part.Id,
                NewPartSerialNumber = part.SerialNumber,
                Reason = PartSwapReason.InitialCommissioning,
                PreviousPartDisposition = PartDisposition.RetainedForAnalysis,
                AssociatedTicketId = request.AssociatedTicketId,
                Notes = request.Notes ?? "Initial component slot installation"
            };

            _context.MachineComponents.Add(targetComponent);
            _context.ComponentPartHistories.Add(history);
        }

        // Add journal record to MachineServiceLog
        var serviceLog = new MachineServiceLog
        {
            Id = Guid.NewGuid(),
            MachineId = request.MachineId,
            Timestamp = DateTimeOffset.UtcNow,
            Author = !string.IsNullOrWhiteSpace(request.SwappedByUserName) ? request.SwappedByUserName : request.SwappedByUserId,
            EntryType = "PartReplacement",
            Summary = isNewSlot
                ? $"New component slot '{targetComponent.FunctionalSlotIdentifier}' added with part {part.SerialNumber ?? part.Name}"
                : $"Part replaced on slot '{targetComponent.FunctionalSlotIdentifier}' with part {part.SerialNumber ?? part.Name}",
            Details = request.Notes,
            LinkedTicketId = request.AssociatedTicketId
        };
        _context.MachineServiceLogs.Add(serviceLog);

        await _context.SaveChangesAsync(cancellationToken);

        return new ComponentPartAssignmentResult
        {
            Success = true,
            Message = isNewSlot ? "New component slot created and part assigned." : "Part swapped successfully on existing slot.",
            ComponentId = targetComponent.Id,
            PartId = part.Id,
            HistoryRecordId = history.Id,
            IsNewComponentCreated = isNewSlot
        };
    }

    public async Task<List<ComponentPartHistoryDto>> GetComponentPartHistoryAsync(Guid componentId, CancellationToken cancellationToken = default)
    {
        return await _context.ComponentPartHistories
            .AsNoTracking()
            .Where(h => h.ComponentId == componentId)
            .OrderByDescending(h => h.SwappedAt)
            .Select(h => new ComponentPartHistoryDto
            {
                Id = h.Id,
                ComponentId = h.ComponentId,
                MachineId = h.MachineId,
                SwappedAt = h.SwappedAt,
                SwappedByUserId = h.SwappedByUserId,
                SwappedByUserName = h.SwappedByUserName,
                PreviousPartId = h.PreviousPartId,
                PreviousPartSerialNumber = h.PreviousPartSerialNumber,
                NewPartId = h.NewPartId,
                NewPartSerialNumber = h.NewPartSerialNumber,
                Reason = h.Reason,
                PreviousPartDisposition = h.PreviousPartDisposition,
                AssociatedTicketId = h.AssociatedTicketId,
                Notes = h.Notes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<MachineComponentDto?> GetComponentByIdAsync(Guid componentId, CancellationToken cancellationToken = default)
    {
        var component = await _context.MachineComponents
            .AsNoTracking()
            .Include(c => c.ComponentRecipe)
            .Include(c => c.CurrentPart)
            .Include(c => c.PartHistory)
            .FirstOrDefaultAsync(c => c.Id == componentId, cancellationToken);

        if (component == null) return null;

        return MapToDto(component);
    }

    public async Task<List<MachineComponentDto>> GetMachineComponentsAsync(Guid machineId, CancellationToken cancellationToken = default)
    {
        var list = await _context.MachineComponents
            .AsNoTracking()
            .Include(c => c.ComponentRecipe)
            .Include(c => c.CurrentPart)
            .Include(c => c.PartHistory)
            .Where(c => c.MachineId == machineId)
            .OrderBy(c => c.FunctionalSlotIdentifier)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    private static MachineComponentDto MapToDto(MachineComponent component)
    {
        EplanCadReference? eplanRef = null;
        if (!string.IsNullOrWhiteSpace(component.EplanCadRefJson))
        {
            try
            {
                eplanRef = JsonSerializer.Deserialize<EplanCadReference>(component.EplanCadRefJson);
            }
            catch
            {
                // Fallback to empty if parse fails
            }
        }

        JsonDocument? attributes = null;
        if (!string.IsNullOrWhiteSpace(component.AttributesJson))
        {
            try
            {
                attributes = JsonDocument.Parse(component.AttributesJson);
            }
            catch
            {
                // Fallback
            }
        }

        return new MachineComponentDto
        {
            Id = component.Id,
            MachineId = component.MachineId,
            Name = component.Name,
            FunctionalSlotIdentifier = component.FunctionalSlotIdentifier,
            Category = component.Category,
            ComponentRecipeId = component.ComponentRecipeId,
            ComponentRecipeCode = component.ComponentRecipe?.RecipeCode,
            CurrentPartId = component.CurrentPartId,
            CurrentPartSerialNumber = component.CurrentPart?.SerialNumber,
            Tags = component.Tags,
            Attributes = attributes,
            EplanCadRef = eplanRef,
            InstalledAt = component.InstalledAt,
            UpdatedAt = component.UpdatedAt,
            PartHistory = component.PartHistory.Select(h => new ComponentPartHistoryDto
            {
                Id = h.Id,
                ComponentId = h.ComponentId,
                MachineId = h.MachineId,
                SwappedAt = h.SwappedAt,
                SwappedByUserId = h.SwappedByUserId,
                SwappedByUserName = h.SwappedByUserName,
                PreviousPartId = h.PreviousPartId,
                PreviousPartSerialNumber = h.PreviousPartSerialNumber,
                NewPartId = h.NewPartId,
                NewPartSerialNumber = h.NewPartSerialNumber,
                Reason = h.Reason,
                PreviousPartDisposition = h.PreviousPartDisposition,
                AssociatedTicketId = h.AssociatedTicketId,
                Notes = h.Notes
            }).OrderByDescending(h => h.SwappedAt).ToList()
        };
    }
}
