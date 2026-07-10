using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.template_merge_fields (lexflow-database Scripts/05_DMS/TemplateMergeFields).</summary>
public sealed class TemplateMergeField : AuditableEntity
{
    private TemplateMergeField()
    {
    }

    public TemplateMergeField(Guid tenantId, Guid templateId, string fieldKey, string? label, bool required)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TemplateId = templateId;
        FieldKey = fieldKey;
        Label = label;
        Required = required;
    }

    public Guid TemplateId { get; private set; }
    public string FieldKey { get; private set; } = null!;
    public string? Label { get; private set; }
    public bool Required { get; private set; }
}
