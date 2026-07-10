using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class MatterStatusHistoryConfiguration : IEntityTypeConfiguration<MatterStatusHistory>
{
    public void Configure(EntityTypeBuilder<MatterStatusHistory> builder)
    {
        builder.ToTable("matter_status_history", "legal");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status");
        builder.Property(h => h.ToStatus).HasColumnName("to_status").IsRequired();
        builder.Property(h => h.Outcome).HasColumnName("outcome");
        builder.Property(h => h.ClosureNote).HasColumnName("closure_note");
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
