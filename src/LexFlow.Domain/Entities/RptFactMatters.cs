namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_fact_matters (lexflow-database Scripts/17_Reporting_StarSchema/RptFactMatters).
/// Module 13 star schema. Grain: one row per legal.matters row, upserted on every relevant change
/// (status/outcome/close) by the hourly incremental ETL job — a current-state snapshot fact, not an
/// event log (see the table's own 001_Table.sql comment).
/// </summary>
public sealed class RptFactMatters
{
    private RptFactMatters()
    {
    }

    public RptFactMatters(
        Guid tenantId,
        Guid matterId,
        int? openedDateKey,
        int? closedDateKey,
        Guid? lawyerKey,
        Guid? clientKey,
        Guid? practiceAreaKey,
        Guid? branchId,
        string? status,
        string? outcome,
        int? cycleTimeDays,
        bool isOpen)
    {
        FactMatterKey = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        Apply(openedDateKey, closedDateKey, lawyerKey, clientKey, practiceAreaKey, branchId, status, outcome, cycleTimeDays, isOpen);
    }

    public Guid FactMatterKey { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid MatterId { get; private set; }
    public int? OpenedDateKey { get; private set; }
    public int? ClosedDateKey { get; private set; }
    public Guid? LawyerKey { get; private set; }
    public Guid? ClientKey { get; private set; }
    public Guid? PracticeAreaKey { get; private set; }
    public Guid? BranchId { get; private set; }
    public string? Status { get; private set; }
    public string? Outcome { get; private set; }
    public int? CycleTimeDays { get; private set; }
    public bool IsOpen { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Apply(
        int? openedDateKey,
        int? closedDateKey,
        Guid? lawyerKey,
        Guid? clientKey,
        Guid? practiceAreaKey,
        Guid? branchId,
        string? status,
        string? outcome,
        int? cycleTimeDays,
        bool isOpen)
    {
        OpenedDateKey = openedDateKey;
        ClosedDateKey = closedDateKey;
        LawyerKey = lawyerKey;
        ClientKey = clientKey;
        PracticeAreaKey = practiceAreaKey;
        BranchId = branchId;
        Status = status;
        Outcome = outcome;
        CycleTimeDays = cycleTimeDays;
        IsOpen = isOpen;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
