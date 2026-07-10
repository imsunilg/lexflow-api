using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class WhatsappOptinConfiguration : IEntityTypeConfiguration<WhatsappOptin>
{
    public void Configure(EntityTypeBuilder<WhatsappOptin> builder)
    {
        builder.ToTable("whatsapp_optins", "comm");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(o => o.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(o => o.PhoneE164).HasColumnName("phone_e164").IsRequired();
        builder.Property(o => o.OptedInAt).HasColumnName("opted_in_at").IsRequired();
        builder.Property(o => o.OptedOutAt).HasColumnName("opted_out_at");
        builder.Property(o => o.Source).HasColumnName("source");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");
        builder.Property(o => o.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
