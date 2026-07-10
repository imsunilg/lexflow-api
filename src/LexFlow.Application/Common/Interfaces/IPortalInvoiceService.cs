namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 17 User Flow #4: invoice list/detail — clientId always comes from the caller's token, never the request.</summary>
public interface IPortalInvoiceService
{
    Task<IReadOnlyList<PortalInvoiceSummaryDto>> GetInvoicesAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Throws NotFoundException if invoiceId does not belong to clientId.</summary>
    Task<PortalInvoiceSummaryDto> GetInvoiceAsync(Guid tenantId, Guid clientId, Guid invoiceId, CancellationToken cancellationToken = default);
}

public sealed record PortalInvoiceSummaryDto(Guid Id, string Number, Guid MatterId, string Status, DateOnly? IssueDate, DateOnly? DueDate, string Currency, decimal GrandTotal, decimal AmountPaid, decimal OpenBalance);
