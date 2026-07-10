using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to core.departments (lexflow-database Scripts/02_Core/Departments).</summary>
public sealed class Department : AuditableEntity
{
    private Department()
    {
    }

    public Department(Guid tenantId, string name, string? code = null, Guid? headUserId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Code = code;
        HeadUserId = headUserId;
    }

    public string Name { get; private set; } = null!;
    public string? Code { get; private set; }
    public Guid? HeadUserId { get; private set; }

    public void Update(string name, string? code, Guid? headUserId)
    {
        Name = name;
        Code = code;
        HeadUserId = headUserId;
    }
}
