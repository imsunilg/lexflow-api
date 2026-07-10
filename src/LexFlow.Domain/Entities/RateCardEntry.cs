using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to fin.rate_card_entries (lexflow-database Scripts/06_Fin/RateCardEntries).
/// BR-7 rate resolution order: entry override -&gt; matter-member override -&gt; matter rate card -&gt;
/// firm default. A row with <see cref="UserId"/> set is a per-timekeeper override; a row with only
/// <see cref="Role"/> set is a role-level default within the card.
/// </summary>
public sealed class RateCardEntry : AuditableEntity
{
    private RateCardEntry()
    {
    }

    public RateCardEntry(Guid tenantId, Guid rateCardId, string? role, Guid? userId, decimal rate, string currency, DateOnly effectiveFrom)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        RateCardId = rateCardId;
        Role = role;
        UserId = userId;
        Rate = rate;
        Currency = currency;
        EffectiveFrom = effectiveFrom;
    }

    public Guid RateCardId { get; private set; }
    public string? Role { get; private set; }
    public Guid? UserId { get; private set; }
    public decimal Rate { get; private set; }
    public string Currency { get; private set; } = "INR";
    public DateOnly EffectiveFrom { get; private set; }

    public void Update(decimal rate, DateOnly effectiveFrom) => (Rate, EffectiveFrom) = (rate, effectiveFrom);
}
