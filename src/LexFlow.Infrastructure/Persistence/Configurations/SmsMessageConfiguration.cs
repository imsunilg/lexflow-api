using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class SmsMessageConfiguration : IEntityTypeConfiguration<SmsMessage>
{
    public void Configure(EntityTypeBuilder<SmsMessage> builder)
    {
        builder.ToTable("sms_messages", "comm");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.ClientId).HasColumnName("client_id");
        builder.Property(s => s.MatterId).HasColumnName("matter_id");
        builder.Property(s => s.Direction).HasColumnName("direction").IsRequired();
        builder.Property(s => s.FromNumber).HasColumnName("from_number");
        builder.Property(s => s.ToNumber).HasColumnName("to_number");
        builder.Property(s => s.Body).HasColumnName("body");
        builder.Property(s => s.DltTemplateId).HasColumnName("dlt_template_id");
        builder.Property(s => s.Provider).HasColumnName("provider");
        builder.Property(s => s.ProviderMessageId).HasColumnName("provider_message_id");
        builder.Property(s => s.Status).HasColumnName("status").IsRequired();
        builder.Property(s => s.SentAt).HasColumnName("sent_at").IsRequired();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
