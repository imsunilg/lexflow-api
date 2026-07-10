using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events", "audit");
        builder.HasKey(e => new { e.Id, e.At });

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.At).HasColumnName("at").IsRequired();
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(e => e.ActorType).HasColumnName("actor_type").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id");
        builder.Property(e => e.Before).HasColumnName("before").HasColumnType("jsonb");
        builder.Property(e => e.After).HasColumnName("after").HasColumnType("jsonb");
        builder.Property(e => e.Ip).HasColumnName("ip").HasColumnType("inet");
        builder.Property(e => e.Ua).HasColumnName("ua");
        builder.Property(e => e.TraceId).HasColumnName("trace_id");

        // No query filter here (unlike every AuditableEntity config): audit rows have
        // no is_deleted column — they are never soft- or hard-deleted (§30 immutability).
    }
}
