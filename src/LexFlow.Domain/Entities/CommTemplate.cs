using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to comm.comm_templates (lexflow-database Scripts/08_Comm/CommTemplates). Backs Settings §11 Email/SMS/WhatsApp Templates.</summary>
public sealed class CommTemplate : AuditableEntity
{
    private CommTemplate()
    {
    }

    public CommTemplate(Guid tenantId, string channel, string name, string body, string variablesJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Channel = channel;
        Name = name;
        Body = body;
        VariablesJson = variablesJson;
        IsActive = true;
    }

    public string Channel { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string VariablesJson { get; private set; } = "[]";
    public string? DltTemplateId { get; private set; }
    public string? WaHsmName { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdateBody(string body, string variablesJson) => (Body, VariablesJson) = (body, variablesJson);

    public void SetDltTemplateId(string? dltTemplateId) => DltTemplateId = dltTemplateId;

    public void SetWaHsmName(string? waHsmName) => WaHsmName = waHsmName;

    public void SetActive(bool isActive) => IsActive = isActive;
}
