using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines", "fin");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(l => l.LineNo).HasColumnName("line_no").IsRequired();
        builder.Property(l => l.Type).HasColumnName("type").IsRequired();
        builder.Property(l => l.Description).HasColumnName("description");
        builder.Property(l => l.Qty).HasColumnName("qty").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(l => l.Unit).HasColumnName("unit");
        builder.Property(l => l.Rate).HasColumnName("rate").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(l => l.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(l => l.TimeEntryIds).HasColumnName("time_entry_ids").HasColumnType("uuid[]").IsRequired();
        builder.Property(l => l.ExpenseIds).HasColumnName("expense_ids").HasColumnType("uuid[]").IsRequired();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");
        builder.Property(l => l.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
