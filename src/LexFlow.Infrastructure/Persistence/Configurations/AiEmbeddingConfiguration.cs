using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class AiEmbeddingConfiguration : IEntityTypeConfiguration<AiEmbedding>
{
    public void Configure(EntityTypeBuilder<AiEmbedding> builder)
    {
        builder.ToTable("ai_embeddings", "ai");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.SourceKind).HasColumnName("source_kind").IsRequired();
        builder.Property(e => e.SourceId).HasColumnName("source_id").IsRequired();
        builder.Property(e => e.ChunkIndex).HasColumnName("chunk_index").IsRequired();
        builder.Property(e => e.ChunkText).HasColumnName("chunk_text").IsRequired();
        builder.Property(e => e.Embedding).HasColumnName("embedding").HasColumnType("double precision[]");
        builder.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.SourceKind, e.SourceId, e.ChunkIndex }).IsUnique();
    }
}
