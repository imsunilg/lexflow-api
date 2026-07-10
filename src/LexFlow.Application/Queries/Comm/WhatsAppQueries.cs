using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Comm;

public sealed record GetWhatsAppForClientQuery(Guid ClientId) : IRequest<IReadOnlyList<WhatsappMessageDto>>;

public sealed class GetWhatsAppForClientQueryHandler(IWhatsAppService whatsAppService, ICurrentUserService currentUser) : IRequestHandler<GetWhatsAppForClientQuery, IReadOnlyList<WhatsappMessageDto>>
{
    public Task<IReadOnlyList<WhatsappMessageDto>> Handle(GetWhatsAppForClientQuery request, CancellationToken cancellationToken)
        => whatsAppService.GetForClientAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}
