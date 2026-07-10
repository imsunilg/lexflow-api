using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class LeadStageHistoryConfiguration : IEntityTypeConfiguration<LeadStageHistory>
{
    public void Configure(EntityTypeBuilder<LeadStageHistory> builder)
    {
        builder.ToTable("lead_stage_history", "crm");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.LeadId).HasColumnName("lead_id").IsRequired();
        builder.Property(h => h.FromStage).HasColumnName("from_stage");
        builder.Property(h => h.ToStage).HasColumnName("to_stage").IsRequired();
        builder.Property(h => h.At).HasColumnName("at").IsRequired();
        builder.Property(h => h.ByUser).HasColumnName("by_user");

        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(h => h.CreatedBy).HasColumnName("created_by");
        builder.Property(h => h.UpdatedAt).HasColumnName("updated_at");
        builder.Property(h => h.UpdatedBy).HasColumnName("updated_by");
        builder.Property(h => h.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(h => h.DeletedAt).HasColumnName("deleted_at");
        builder.Property(h => h.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(h => !h.IsDeleted);
    }
}
