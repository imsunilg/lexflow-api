namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 7 template library + server-side OpenXML merge. AC-DOC4: generation must
/// produce a correct merged docx for every seeded field, including nested paths like
/// client.address.city. Validation: "template merge fails on missing required fields
/// with field list."
/// </summary>
public interface IDocumentTemplateService
{
    Task<DocumentTemplateDto> CreateAsync(Guid tenantId, Guid? actorId, string name, string? category, byte[] docxContent, IReadOnlyList<MergeFieldInput> fields, CancellationToken cancellationToken = default);

    Task<DocumentTemplateDto?> GetByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentTemplateDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/v1/documents/templates/{id}/generate {matterId, overrides}. Resolves
    /// merge-field values from the matter/client/case/court/hearing graph plus any
    /// caller-supplied overrides, fails fast with the full missing-field list if any
    /// required field can't be resolved, then performs the OpenXML merge and files the
    /// result as a new draft Document in the matter.
    /// </summary>
    Task<DocumentDto> GenerateAsync(Guid tenantId, Guid? actorId, Guid templateId, Guid matterId, IReadOnlyDictionary<string, string>? overrides, CancellationToken cancellationToken = default);
}

public sealed record MergeFieldInput(string FieldKey, string? Label, bool Required);

public sealed record DocumentTemplateDto(Guid Id, string Name, string? Category, int Version, IReadOnlyList<MergeFieldInput> Fields);
