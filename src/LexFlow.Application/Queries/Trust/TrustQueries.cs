using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Trust;

public sealed record GetTrustAccountQuery(Guid ClientId) : IRequest<TrustAccountDto>;

public sealed class GetTrustAccountQueryHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<GetTrustAccountQuery, TrustAccountDto>
{
    public Task<TrustAccountDto> Handle(GetTrustAccountQuery request, CancellationToken cancellationToken)
        => service.GetOrCreateAccountAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

public sealed record GetTrustLedgerQuery(Guid ClientId) : IRequest<IReadOnlyList<TrustLedgerEntryDto>>;

public sealed class GetTrustLedgerQueryHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<GetTrustLedgerQuery, IReadOnlyList<TrustLedgerEntryDto>>
{
    public Task<IReadOnlyList<TrustLedgerEntryDto>> Handle(GetTrustLedgerQuery request, CancellationToken cancellationToken)
        => service.GetLedgerAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}

public sealed record GetTrustReconciliationExceptionsQuery(Guid ReconciliationId) : IRequest<IReadOnlyList<TrustReconciliationItemDto>>;

public sealed class GetTrustReconciliationExceptionsQueryHandler(ITrustService service, ICurrentUserService currentUser) : IRequestHandler<GetTrustReconciliationExceptionsQuery, IReadOnlyList<TrustReconciliationItemDto>>
{
    public Task<IReadOnlyList<TrustReconciliationItemDto>> Handle(GetTrustReconciliationExceptionsQuery request, CancellationToken cancellationToken)
        => service.GetExceptionsAsync(currentUser.TenantId!.Value, request.ReconciliationId, cancellationToken);
}
