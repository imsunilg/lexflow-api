using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.billing_arrangements (lexflow-database Scripts/06_Fin/BillingArrangements).
/// Module 8: "Fee setup per matter: Hourly (rate card + overrides), Fixed (total + optional
/// milestones), Retainer (amount, period, auto-invoice day, replenishment threshold),
/// Contingency (%), Pro Bono."
/// </summary>
public sealed class BillingArrangement : AuditableEntity
{
    private BillingArrangement()
    {
    }

    public BillingArrangement(
        Guid tenantId,
        Guid matterId,
        string arrangementType,
        Guid? rateCardId,
        decimal? fixedAmount,
        string milestonesJson,
        decimal? retainerAmount,
        string? retainerPeriod,
        int? autoInvoiceDay,
        decimal? replenishmentThreshold,
        decimal? contingencyPct)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        MatterId = matterId;
        ArrangementType = arrangementType;
        RateCardId = rateCardId;
        FixedAmount = fixedAmount;
        MilestonesJson = milestonesJson;
        RetainerAmount = retainerAmount;
        RetainerPeriod = retainerPeriod;
        AutoInvoiceDay = autoInvoiceDay;
        ReplenishmentThreshold = replenishmentThreshold;
        ContingencyPct = contingencyPct;
        IsActive = true;
    }

    public Guid MatterId { get; private set; }
    public string ArrangementType { get; private set; } = null!;
    public Guid? RateCardId { get; private set; }
    public decimal? FixedAmount { get; private set; }
    public string MilestonesJson { get; private set; } = "[]";
    public decimal? RetainerAmount { get; private set; }
    public string? RetainerPeriod { get; private set; }
    public int? AutoInvoiceDay { get; private set; }
    public decimal? ReplenishmentThreshold { get; private set; }
    public decimal? ContingencyPct { get; private set; }
    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}
