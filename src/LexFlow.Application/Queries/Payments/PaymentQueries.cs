using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Payments;

public sealed record GetPaymentQuery(Guid Id) : IRequest<PaymentDto?>;

public sealed class GetPaymentQueryHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<GetPaymentQuery, PaymentDto?>
{
    public Task<PaymentDto?> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
        => service.GetAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}

public sealed record GetPaymentsForClientQuery(Guid ClientId) : IRequest<IReadOnlyList<PaymentDto>>;

public sealed class GetPaymentsForClientQueryHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<GetPaymentsForClientQuery, IReadOnlyList<PaymentDto>>
{
    public Task<IReadOnlyList<PaymentDto>> Handle(GetPaymentsForClientQuery request, CancellationToken cancellationToken)
        => service.GetForClientAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

public sealed record GetClientStatementQuery(Guid ClientId, DateOnly From, DateOnly To) : IRequest<ClientStatementDto>;

public sealed class GetClientStatementQueryHandler(IPaymentService service, ICurrentUserService currentUser) : IRequestHandler<GetClientStatementQuery, ClientStatementDto>
{
    public Task<ClientStatementDto> Handle(GetClientStatementQuery request, CancellationToken cancellationToken)
        => service.GetStatementAsync(currentUser.TenantId!.Value, request.ClientId, request.From, request.To, cancellationToken);
}
