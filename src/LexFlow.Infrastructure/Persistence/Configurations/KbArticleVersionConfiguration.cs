using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class KbArticleVersionConfiguration : IEntityTypeConfiguration<KbArticleVersion>
{
    public void Configure(EntityTypeBuilder<KbArticleVersion> builder)
    {
        builder.ToTable("kb_article_versions", "kb");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(v => v.ArticleId).HasColumnName("article_id").IsRequired();
        builder.Property(v => v.VersionNo).HasColumnName("version_no").IsRequired();
        builder.Property(v => v.Title).HasColumnName("title");
        builder.Property(v => v.Body).HasColumnName("body");
        builder.Property(v => v.AuthorId).HasColumnName("author_id");

        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(v => v.DeletedAt).HasColumnName("deleted_at");
        builder.Property(v => v.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
