using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Billing;

public sealed record GetInvoiceQuery(Guid Id) : IRequest<InvoiceDto?>;

public sealed class GetInvoiceQueryHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<GetInvoiceQuery, InvoiceDto?>
{
    public Task<InvoiceDto?> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
        => service.GetAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetInvoicesQuery(string? Status, Guid? ClientId, Guid? MatterId, DateOnly? From, DateOnly? To, bool? OverdueOnly) : IRequest<IReadOnlyList<InvoiceDto>>;

public sealed class GetInvoicesQueryHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<GetInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    public Task<IReadOnlyList<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
        => service.ListAsync(currentUser.TenantId!.Value, new InvoiceFilter(request.Status, request.ClientId, request.MatterId, request.From, request.To, request.OverdueOnly), cancellationToken);
}

public sealed record GetInvoicePdfQuery(Guid Id) : IRequest<byte[]>;

public sealed class GetInvoicePdfQueryHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<GetInvoicePdfQuery, byte[]>
{
    public Task<byte[]> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
        => service.RenderPdfAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetInvoiceStatusHistoryQuery(Guid Id) : IRequest<IReadOnlyList<InvoiceStatusHistoryDto>>;

public sealed class GetInvoiceStatusHistoryQueryHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<GetInvoiceStatusHistoryQuery, IReadOnlyList<InvoiceStatusHistoryDto>>
{
    public Task<IReadOnlyList<InvoiceStatusHistoryDto>> Handle(GetInvoiceStatusHistoryQuery request, CancellationToken cancellationToken)
        => service.GetStatusHistoryAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetAgingReportQuery(DateOnly AsOf) : IRequest<AgingReportDto>;

public sealed class GetAgingReportQueryHandler(IBillingService service, ICurrentUserService currentUser) : IRequestHandler<GetAgingReportQuery, AgingReportDto>
{
    public Task<AgingReportDto> Handle(GetAgingReportQuery request, CancellationToken cancellationToken)
        => service.GetAgingAsync(currentUser.TenantId!.Value, request.AsOf, cancellationToken);
}
