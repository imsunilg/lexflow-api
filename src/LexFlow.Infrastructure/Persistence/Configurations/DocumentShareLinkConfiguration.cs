using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class DocumentShareLinkConfiguration : IEntityTypeConfiguration<DocumentShareLink>
{
    public void Configure(EntityTypeBuilder<DocumentShareLink> builder)
    {
        builder.ToTable("document_share_links", "dms");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(s => s.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(s => s.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(s => s.PasswordHash).HasColumnName("password_hash");
        builder.Property(s => s.MaxDownloads).HasColumnName("max_downloads");
        builder.Property(s => s.Downloads).HasColumnName("downloads").IsRequired();
        builder.Property(s => s.Watermark).HasColumnName("watermark").IsRequired();
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");

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
