using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LexFlow.Infrastructure.Ai;

/// <summary>See IAiQuotaService's own doc comment. Usage is derived from ai.ai_interactions.credits_charged for the current calendar month rather than a separately-tracked running counter, so it can never drift from what was actually charged.</summary>
public sealed class AiQuotaService(LexFlowDbContext db, IOptions<AiOptions> options) : IAiQuotaService
{
    public async Task EnsureWithinQuotaAsync(Guid tenantId, decimal estimatedCredits, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(tenantId, cancellationToken);
        if (status.UsedThisMonth + estimatedCredits > status.MonthlyLimit)
        {
            throw new AiQuotaExceededException(status.MonthlyLimit, status.UsedThisMonth);
        }
    }

    public async Task<AiQuotaStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var quota = await db.AiTenantQuotas.SingleOrDefaultAsync(q => q.TenantId == tenantId, cancellationToken);
        var limit = quota?.MonthlyCreditLimit ?? options.Value.DefaultMonthlyCreditLimit;

        var monthStart = new DateTimeOffset(new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var used = await db.AiInteractions
            .Where(i => i.TenantId == tenantId && i.CreatedAt >= monthStart)
            .SumAsync(i => (decimal?)i.CreditsCharged, cancellationToken) ?? 0;

        return new AiQuotaStatus(limit, used, Math.Max(0, limit - used));
    }
}
