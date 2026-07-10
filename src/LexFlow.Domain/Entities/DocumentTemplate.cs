using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to dms.document_templates (lexflow-database Scripts/05_DMS/DocumentTemplates). docx merge-field templates.</summary>
public sealed class DocumentTemplate : AuditableEntity
{
    private DocumentTemplate()
    {
    }

    public DocumentTemplate(Guid tenantId, string name, string? category, string docxBlobPath, string fieldsJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Category = category;
        DocxBlobPath = docxBlobPath;
        FieldsJson = fieldsJson;
        Version = 1;
    }

    public string Name { get; private set; } = null!;
    public string? Category { get; private set; }
    public string DocxBlobPath { get; private set; } = null!;
    public string FieldsJson { get; private set; } = "{}";
    public int Version { get; private set; }

    public void ReplaceDocx(string docxBlobPath)
    {
        DocxBlobPath = docxBlobPath;
        Version++;
    }
}
