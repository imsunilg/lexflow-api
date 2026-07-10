using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to crm.leads (lexflow-database Scripts/03_CRM/Leads). Module 2.</summary>
public sealed class Lead : AuditableEntity
{
    private Lead()
    {
    }

    public Lead(
        Guid tenantId,
        string number,
        string firstName,
        string? lastName,
        string? company,
        string? email,
        string? phoneE164,
        Guid? sourceId,
        Guid? ownerId,
        Guid? branchId,
        Guid? practiceAreaId,
        string? issueSummary,
        string? opposingParty,
        string? budgetBand,
        DateTimeOffset? slaFirstContactDue)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Number = number;
        FirstName = firstName;
        LastName = lastName;
        Company = company;
        Email = email;
        PhoneE164 = phoneE164;
        SourceId = sourceId;
        Stage = "New";
        OwnerId = ownerId;
        BranchId = branchId;
        PracticeAreaId = practiceAreaId;
        Score = 0;
        IssueSummary = issueSummary;
        OpposingParty = opposingParty;
        BudgetBand = budgetBand;
        Status = "Open";
        SlaFirstContactDue = slaFirstContactDue;
    }

    public string Number { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string? LastName { get; private set; }
    public string? Company { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneE164 { get; private set; }
    public Guid? SourceId { get; private set; }
    public string Stage { get; private set; } = "New";
    public Guid? OwnerId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? PracticeAreaId { get; private set; }
    public int Score { get; private set; }
    public string? IssueSummary { get; private set; }
    public string? OpposingParty { get; private set; }
    public string? BudgetBand { get; private set; }
    public string Status { get; private set; } = "Open";
    public Guid? LostReasonId { get; private set; }
    public Guid? ConvertedClientId { get; private set; }
    public DateTimeOffset? SlaFirstContactDue { get; private set; }
    public DateTimeOffset? FirstContactedAt { get; private set; }

    public void Update(
        string firstName,
        string? lastName,
        string? company,
        string? email,
        string? phoneE164,
        Guid? sourceId,
        Guid? practiceAreaId,
        string? issueSummary,
        string? opposingParty,
        string? budgetBand)
    {
        FirstName = firstName;
        LastName = lastName;
        Company = company;
        Email = email;
        PhoneE164 = phoneE164;
        SourceId = sourceId;
        PracticeAreaId = practiceAreaId;
        IssueSummary = issueSummary;
        OpposingParty = opposingParty;
        BudgetBand = budgetBand;
    }

    public void ChangeStage(string toStage)
    {
        Stage = toStage;
        if (FirstContactedAt is null && toStage != "New")
        {
            FirstContactedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Assign(Guid ownerId) => OwnerId = ownerId;

    public void SetScore(int score) => Score = Math.Clamp(score, 0, 100);

    public void MarkConverted(Guid clientId)
    {
        Status = "Converted";
        ConvertedClientId = clientId;
    }

    public void MarkLost(Guid lostReasonId)
    {
        Status = "Lost";
        LostReasonId = lostReasonId;
    }
}
