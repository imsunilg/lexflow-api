using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class KbArticleConfiguration : IEntityTypeConfiguration<KbArticle>
{
    public void Configure(EntityTypeBuilder<KbArticle> builder)
    {
        builder.ToTable("kb_articles", "kb");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.Title).HasColumnName("title").IsRequired();
        builder.Property(a => a.Body).HasColumnName("body");
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.Version).HasColumnName("version").IsRequired();
        builder.Property(a => a.AuthorId).HasColumnName("author_id");
        builder.Property(a => a.ReviewerId).HasColumnName("reviewer_id");
        builder.Property(a => a.PublishedAt).HasColumnName("published_at");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
