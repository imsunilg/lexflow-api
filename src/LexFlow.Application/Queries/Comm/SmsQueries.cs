using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Comm;

public sealed record GetSmsForClientQuery(Guid ClientId) : IRequest<IReadOnlyList<SmsMessageDto>>;

public sealed class GetSmsForClientQueryHandler(ISmsService smsService, ICurrentUserService currentUser) : IRequestHandler<GetSmsForClientQuery, IReadOnlyList<SmsMessageDto>>
{
    public Task<IReadOnlyList<SmsMessageDto>> Handle(GetSmsForClientQuery request, CancellationToken cancellationToken)
        => smsService.GetForClientAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}
