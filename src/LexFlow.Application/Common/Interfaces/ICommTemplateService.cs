namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 15 §11 Email/SMS/WhatsApp Templates CRUD (PRD §17: "CRUD ... templates"). Placeholder validation is a warning, not a hard rejection (PRD Module 15 Validation).</summary>
public interface ICommTemplateService
{
    Task<IReadOnlyList<CommTemplateDto>> GetAllAsync(Guid tenantId, string? channel, CancellationToken cancellationToken = default);

    Task<CommTemplateDto> CreateAsync(Guid tenantId, string channel, string name, string body, string variablesJson, CancellationToken cancellationToken = default);

    Task<CommTemplateDto> UpdateAsync(Guid tenantId, Guid id, string body, string variablesJson, bool isActive, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}

public sealed record CommTemplateDto(
    Guid Id,
    string Channel,
    string Name,
    string Body,
    string VariablesJson,
    string? DltTemplateId,
    string? WaHsmName,
    bool IsActive,
    IReadOnlyList<string> UnknownPlaceholders);
