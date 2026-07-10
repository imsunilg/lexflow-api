using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class PaymentGatewaySessionConfiguration : IEntityTypeConfiguration<PaymentGatewaySession>
{
    public void Configure(EntityTypeBuilder<PaymentGatewaySession> builder)
    {
        builder.ToTable("payment_gateway_sessions", "portal");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.InvoiceId).HasColumnName("invoice_id").IsRequired();
        builder.Property(s => s.ClientPortalUserId).HasColumnName("client_portal_user_id");
        builder.Property(s => s.Gateway).HasColumnName("gateway").IsRequired();
        builder.Property(s => s.GatewayRef).HasColumnName("gateway_ref");
        builder.Property(s => s.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").IsRequired();
        builder.Property(s => s.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        builder.Property(s => s.ReturnUrl).HasColumnName("return_url");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");
    }
}
