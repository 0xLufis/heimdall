namespace App.Infrastructure.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Contracts.Templates;

public interface IGenericTemplateService
{
    Task<IReadOnlyList<GenericTemplateDto>> GetTemplatesAsync(TemplateQueryFilterDto filter, CancellationToken ct = default);
    Task<GenericTemplateDto?> GetTemplateByIdAsync(Guid id, CancellationToken ct = default);
    Task<GenericTemplateDto?> GetTemplateByCodeAsync(string code, CancellationToken ct = default);
    Task<GenericTemplateDto> CreateTemplateAsync(CreateGenericTemplateDto dto, string? organizationId = null, CancellationToken ct = default);
    Task<GenericTemplateDto?> UpdateTemplateAsync(Guid id, UpdateGenericTemplateDto dto, CancellationToken ct = default);
    Task<bool> DeleteTemplateAsync(Guid id, CancellationToken ct = default);
    Task<GenericTemplateDto?> DeriveTemplateAsync(Guid sourceId, DeriveTemplateRequestDto dto, string? organizationId = null, CancellationToken ct = default);
    Task<TemplateResolutionResultDto?> ResolveEffectiveSchemaAsync(Guid templateId, CancellationToken ct = default);
    Task<TemplateInstantiationResultDto> InstantiateTemplateAsync(Guid templateId, InstantiateTemplateRequestDto dto, CancellationToken ct = default);
    Task<TemplateValidationResultDto> ValidateInstanceDataAsync(Guid templateId, Dictionary<string, object?> data, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateTypeDefinitionDto>> GetSupportedTemplateTypesAsync(CancellationToken ct = default);
    Task SeedDefaultTemplatesIfEmptyAsync(CancellationToken ct = default);
}
