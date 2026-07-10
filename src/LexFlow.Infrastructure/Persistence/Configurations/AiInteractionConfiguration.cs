using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class AiInteractionConfiguration : IEntityTypeConfiguration<AiInteraction>
{
    public void Configure(EntityTypeBuilder<AiInteraction> builder)
    {
        builder.ToTable("ai_interactions", "ai");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(i => i.Feature).HasColumnName("feature").IsRequired();
        builder.Property(i => i.PromptTemplateKey).HasColumnName("prompt_template_key").IsRequired();
        builder.Property(i => i.PromptTemplateVersion).HasColumnName("prompt_template_version").IsRequired();
        builder.Property(i => i.Model).HasColumnName("model").IsRequired();
        builder.Property(i => i.TokensInput).HasColumnName("tokens_input").IsRequired();
        builder.Property(i => i.TokensOutput).HasColumnName("tokens_output").IsRequired();
        builder.Property(i => i.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(i => i.CreditsCharged).HasColumnName("credits_charged").IsRequired();
        builder.Property(i => i.UserId).HasColumnName("user_id");
        builder.Property(i => i.TargetRefKind).HasColumnName("target_ref_kind");
        builder.Property(i => i.TargetRefId).HasColumnName("target_ref_id");
        builder.Property(i => i.InputText).HasColumnName("input_text");
        builder.Property(i => i.OutputText).HasColumnName("output_text");
        builder.Property(i => i.Rating).HasColumnName("rating");
        builder.Property(i => i.RatingReason).HasColumnName("rating_reason");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.RetentionExpiresAt).HasColumnName("retention_expires_at");
    }
}
