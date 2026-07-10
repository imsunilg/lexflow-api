using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.ToTable("mailboxes", "comm");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Provider).HasColumnName("provider").IsRequired();
        builder.Property(m => m.EmailAddress).HasColumnName("email_address").IsRequired();
        builder.Property(m => m.AccessTokenEnc).HasColumnName("access_token_enc");
        builder.Property(m => m.RefreshTokenEnc).HasColumnName("refresh_token_enc");
        builder.Property(m => m.SyncStateJson).HasColumnName("sync_state").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(m => m.ConnectedAt).HasColumnName("connected_at").IsRequired();
        builder.Property(m => m.DisconnectedAt).HasColumnName("disconnected_at");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");
        builder.Property(m => m.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
