using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class WhatsappMessageConfiguration : IEntityTypeConfiguration<WhatsappMessage>
{
    public void Configure(EntityTypeBuilder<WhatsappMessage> builder)
    {
        builder.ToTable("whatsapp_messages", "comm");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(w => w.ClientId).HasColumnName("client_id");
        builder.Property(w => w.WaMsgId).HasColumnName("wa_msg_id").IsRequired();
        builder.Property(w => w.Direction).HasColumnName("direction").IsRequired();
        builder.Property(w => w.TemplateId).HasColumnName("template_id");
        builder.Property(w => w.Body).HasColumnName("body");
        builder.Property(w => w.MediaDocumentId).HasColumnName("media_document_id");
        builder.Property(w => w.Status).HasColumnName("status").IsRequired();
        builder.Property(w => w.WindowExpiresAt).HasColumnName("window_expires_at");

        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");
        builder.Property(w => w.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
