using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class SignatureEnvelopeConfiguration : IEntityTypeConfiguration<SignatureEnvelope>
{
    public void Configure(EntityTypeBuilder<SignatureEnvelope> builder)
    {
        builder.ToTable("signature_envelopes", "dms");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.DocumentId).HasColumnName("document_id").IsRequired();
        builder.Property(e => e.Provider).HasColumnName("provider").IsRequired();
        builder.Property(e => e.ProviderEnvelopeId).HasColumnName("provider_envelope_id");
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.CompletedDocVersionId).HasColumnName("completed_doc_version_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
