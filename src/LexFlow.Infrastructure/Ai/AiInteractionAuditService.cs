using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Ai;

/// <summary>See IAiInteractionAuditService's own doc comment. RetentionExpiresAt defaults to 90 days from now, the PRD's stated default firm retention policy for AI prompts/outputs.</summary>
public sealed class AiInteractionAuditService(LexFlowDbContext db) : IAiInteractionAuditService
{
    private static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(90);

    public async Task<Guid> RecordAsync(AiInteractionRecord record, CancellationToken cancellationToken = default)
    {
        var interaction = new AiInteraction(
            record.TenantId,
            record.Feature,
            record.PromptTemplateKey,
            record.PromptTemplateVersion,
            record.Model,
            record.TokensInput,
            record.TokensOutput,
            record.LatencyMs,
            record.CreditsCharged,
            record.UserId,
            record.TargetRefKind,
            record.TargetRefId,
            record.InputText,
            record.OutputText,
            DateTimeOffset.UtcNow.Add(DefaultRetention));

        await db.AiInteractions.AddAsync(interaction, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return interaction.Id;
    }

    public async Task RecordFeedbackAsync(Guid tenantId, Guid interactionId, int rating, string? reason, CancellationToken cancellationToken = default)
    {
        var interaction = await db.AiInteractions.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == interactionId, cancellationToken)
            ?? throw new NotFoundException(nameof(AiInteraction), interactionId);

        interaction.RecordFeedback(rating, reason);
        await db.SaveChangesAsync(cancellationToken);
    }
}
