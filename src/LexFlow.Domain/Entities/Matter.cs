using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to legal.matters (lexflow-database Scripts/04_Legal/Matters). Module 4 — the core engagement object.</summary>
public sealed class Matter : AuditableEntity
{
    private Matter()
    {
    }

    public Matter(
        Guid tenantId,
        string number,
        string title,
        Guid clientId,
        string matterType,
        Guid? practiceAreaId,
        Guid? branchId,
        Guid? responsibleLawyerId,
        string priority,
        string? description,
        DateOnly openedOn,
        bool isPrivate,
        decimal? budget,
        string billingArrangementJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Number = number;
        Title = title;
        ClientId = clientId;
        MatterType = matterType;
        PracticeAreaId = practiceAreaId;
        BranchId = branchId;
        ResponsibleLawyerId = responsibleLawyerId;
        Priority = priority;
        Status = "Open";
        Description = description;
        OpenedOn = openedOn;
        IsPrivate = isPrivate;
        Budget = budget;
        BillingArrangementJson = billingArrangementJson;
        AiAllowed = true;
    }

    public string Number { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public Guid ClientId { get; private set; }
    public string MatterType { get; private set; } = null!;
    public Guid? PracticeAreaId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? ResponsibleLawyerId { get; private set; }
    public string Priority { get; private set; } = "Medium";
    public string Status { get; private set; } = "Open";
    public string? Outcome { get; private set; }
    public DateOnly OpenedOn { get; private set; }
    public DateOnly? ClosedOn { get; private set; }
    public bool IsPrivate { get; private set; }
    public decimal? Budget { get; private set; }
    public string? Description { get; private set; }
    public string BillingArrangementJson { get; private set; } = "{}";
    public bool AiAllowed { get; private set; } = true;

    public void Update(string title, string matterType, Guid? practiceAreaId, Guid? branchId, Guid? responsibleLawyerId, string priority, string? description, decimal? budget, bool isPrivate, string billingArrangementJson)
    {
        Title = title;
        MatterType = matterType;
        PracticeAreaId = practiceAreaId;
        BranchId = branchId;
        ResponsibleLawyerId = responsibleLawyerId;
        Priority = priority;
        Description = description;
        Budget = budget;
        IsPrivate = isPrivate;
        BillingArrangementJson = billingArrangementJson;
    }

    public void ChangeStatus(string toStatus, string? outcome, DateOnly? closedOn)
    {
        Status = toStatus;
        Outcome = outcome;
        ClosedOn = closedOn;
    }
}
