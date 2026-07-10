namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_fact_time (lexflow-database Scripts/17_Reporting_StarSchema/RptFactTime).
/// Module 13 star schema. Grain: one row per fin.time_entries row, upserted by time_entry_id by the
/// hourly incremental ETL job.
/// </summary>
public sealed class RptFactTime
{
    private RptFactTime()
    {
    }

    public RptFactTime(
        Guid tenantId,
        Guid timeEntryId,
        int? dateKey,
        Guid? lawyerKey,
        Guid? clientKey,
        Guid? practiceAreaKey,
        Guid? branchId,
        Guid? matterId,
        int durationMin,
        int roundedMin,
        bool billable,
        bool isBilled,
        decimal amount)
    {
        FactTimeKey = Guid.NewGuid();
        TenantId = tenantId;
        TimeEntryId = timeEntryId;
        Apply(dateKey, lawyerKey, clientKey, practiceAreaKey, branchId, matterId, durationMin, roundedMin, billable, isBilled, amount);
    }

    public Guid FactTimeKey { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid TimeEntryId { get; private set; }
    public int? DateKey { get; private set; }
    public Guid? LawyerKey { get; private set; }
    public Guid? ClientKey { get; private set; }
    public Guid? PracticeAreaKey { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? MatterId { get; private set; }
    public int DurationMin { get; private set; }
    public int RoundedMin { get; private set; }
    public bool Billable { get; private set; }
    public bool IsBilled { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Apply(
        int? dateKey,
        Guid? lawyerKey,
        Guid? clientKey,
        Guid? practiceAreaKey,
        Guid? branchId,
        Guid? matterId,
        int durationMin,
        int roundedMin,
        bool billable,
        bool isBilled,
        decimal amount)
    {
        DateKey = dateKey;
        LawyerKey = lawyerKey;
        ClientKey = clientKey;
        PracticeAreaKey = practiceAreaKey;
        BranchId = branchId;
        MatterId = matterId;
        DurationMin = durationMin;
        RoundedMin = roundedMin;
        Billable = billable;
        IsBilled = isBilled;
        Amount = amount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
