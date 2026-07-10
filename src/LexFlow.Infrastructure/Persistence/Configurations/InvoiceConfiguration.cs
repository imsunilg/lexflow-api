using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", "fin");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(i => i.Number).HasColumnName("number").IsRequired();
        builder.Property(i => i.SeriesId).HasColumnName("series_id");
        builder.Property(i => i.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(i => i.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").IsRequired();
        builder.Property(i => i.IssueDate).HasColumnName("issue_date");
        builder.Property(i => i.DueDate).HasColumnName("due_date");
        builder.Property(i => i.Currency).HasColumnName("currency").IsRequired();
        builder.Property(i => i.SubTotal).HasColumnName("sub_total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.DiscountTotal).HasColumnName("discount_total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.TaxTotal).HasColumnName("tax_total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.GrandTotal).HasColumnName("grand_total").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.AmountPaid).HasColumnName("amount_paid").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(i => i.Notes).HasColumnName("notes");
        builder.Property(i => i.PdfBlobPath).HasColumnName("pdf_blob_path");
        builder.Property(i => i.SentAt).HasColumnName("sent_at");
        builder.Property(i => i.VoidedAt).HasColumnName("voided_at");
        builder.Property(i => i.VoidReason).HasColumnName("void_reason");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");
        builder.Property(i => i.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
