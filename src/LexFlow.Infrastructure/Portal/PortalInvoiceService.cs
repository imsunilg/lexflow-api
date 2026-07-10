using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

/// <summary>Module 17 User Flow #4: invoice list/detail, scoped exclusively by the caller's ClientId (never a payload value — AC-P1).</summary>
public sealed class PortalInvoiceService(LexFlowDbContext db) : IPortalInvoiceService
{
    public async Task<IReadOnlyList<PortalInvoiceSummaryDto>> GetInvoicesAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var invoices = await db.Invoices
            .Where(i => i.TenantId == tenantId && i.ClientId == clientId && i.Status != "Draft")
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync(cancellationToken);

        return invoices.Select(ToDto).ToList();
    }

    public async Task<PortalInvoiceSummaryDto> GetInvoiceAsync(Guid tenantId, Guid clientId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.ClientId != clientId || invoice.Status == "Draft")
        {
            throw new NotFoundException(nameof(Invoice), invoiceId);
        }

        return ToDto(invoice);
    }

    private static PortalInvoiceSummaryDto ToDto(Invoice invoice) => new(
        invoice.Id, invoice.Number, invoice.MatterId, invoice.Status, invoice.IssueDate, invoice.DueDate,
        invoice.Currency, invoice.GrandTotal, invoice.AmountPaid, invoice.GrandTotal - invoice.AmountPaid);
}
