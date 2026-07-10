using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class MatterExpenseConfiguration : IEntityTypeConfiguration<MatterExpense>
{
    public void Configure(EntityTypeBuilder<MatterExpense> builder)
    {
        builder.ToTable("matter_expenses", "legal");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(e => e.IncurredOn).HasColumnName("incurred_on").IsRequired();
        builder.Property(e => e.Category).HasColumnName("category");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(e => e.Billable).HasColumnName("billable").IsRequired();
        builder.Property(e => e.ReceiptDocumentId).HasColumnName("receipt_document_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
