using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceStatusHistoryConfiguration : IEntityTypeConfiguration<InvoiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<InvoiceStatusHistory> builder)
    {
        builder.ToTable("invoice_status_history", "fin");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id");
        builder.Property(h => h.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(h => h.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(h => h.FromStatus).HasColumnName("from_status");
        builder.Property(h => h.ToStatus).HasColumnName("to_status").IsRequired();
        builder.Property(h => h.Reason).HasColumnName("reason");
        builder.Property(h => h.ChangedBy).HasColumnName("changed_by");
        builder.Property(h => h.ChangedAt).HasColumnName("changed_at").IsRequired();

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
