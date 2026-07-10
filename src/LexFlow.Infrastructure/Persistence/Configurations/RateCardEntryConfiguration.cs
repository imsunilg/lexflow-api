using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RateCardEntryConfiguration : IEntityTypeConfiguration<RateCardEntry>
{
    public void Configure(EntityTypeBuilder<RateCardEntry> builder)
    {
        builder.ToTable("rate_card_entries", "fin");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.RateCardId).HasColumnName("rate_card_id").IsRequired();
        builder.Property(r => r.Role).HasColumnName("role");
        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.Rate).HasColumnName("rate").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(r => r.Currency).HasColumnName("currency").IsRequired();
        builder.Property(r => r.EffectiveFrom).HasColumnName("effective_from").IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
