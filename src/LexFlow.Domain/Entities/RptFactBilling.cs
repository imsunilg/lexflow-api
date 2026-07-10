namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to rpt.rpt_fact_billing (lexflow-database Scripts/17_Reporting_StarSchema/RptFactBilling).
/// Module 13 star schema. Grain: one row per fin.invoices row, upserted by invoice_id by the hourly
/// incremental ETL job (see the table's own 001_Table.sql comment for the "by lawyer" grain note).
/// </summary>
public sealed class RptFactBilling
{
    private RptFactBilling()
    {
    }

    public RptFactBilling(
        Guid tenantId,
        Guid invoiceId,
        int? dateKey,
        Guid? lawyerKey,
        Guid? clientKey,
        Guid? practiceAreaKey,
        Guid? branchId,
        decimal billedAmount,
        decimal collectedAmount,
        decimal writeOffAmount,
        decimal outstandingAmount,
        decimal taxAmount)
    {
        FactBillingKey = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceId = invoiceId;
        Apply(dateKey, lawyerKey, clientKey, practiceAreaKey, branchId, billedAmount, collectedAmount, writeOffAmount, outstandingAmount, taxAmount);
    }

    public Guid FactBillingKey { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int? DateKey { get; private set; }
    public Guid? LawyerKey { get; private set; }
    public Guid? ClientKey { get; private set; }
    public Guid? PracticeAreaKey { get; private set; }
    public Guid? BranchId { get; private set; }
    public decimal BilledAmount { get; private set; }
    public decimal CollectedAmount { get; private set; }
    public decimal WriteOffAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Apply(
        int? dateKey,
        Guid? lawyerKey,
        Guid? clientKey,
        Guid? practiceAreaKey,
        Guid? branchId,
        decimal billedAmount,
        decimal collectedAmount,
        decimal writeOffAmount,
        decimal outstandingAmount,
        decimal taxAmount)
    {
        DateKey = dateKey;
        LawyerKey = lawyerKey;
        ClientKey = clientKey;
        PracticeAreaKey = practiceAreaKey;
        BranchId = branchId;
        BilledAmount = billedAmount;
        CollectedAmount = collectedAmount;
        WriteOffAmount = writeOffAmount;
        OutstandingAmount = outstandingAmount;
        TaxAmount = taxAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
