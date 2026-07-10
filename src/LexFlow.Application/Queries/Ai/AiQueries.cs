using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Ai;

public sealed record GetAiTranscriptionQuery(Guid TranscriptionId) : IRequest<AiTranscriptionDto?>;

public sealed class GetAiTranscriptionQueryHandler(IAiTranscriptionService service, ICurrentUserService currentUser) : IRequestHandler<GetAiTranscriptionQuery, AiTranscriptionDto?>
{
    public Task<AiTranscriptionDto?> Handle(GetAiTranscriptionQuery request, CancellationToken cancellationToken)
        => service.GetAsync(currentUser.TenantId!.Value, request.TranscriptionId, cancellationToken);
}

public sealed record GetAiQuotaStatusQuery : IRequest<AiQuotaStatus>;

public sealed class GetAiQuotaStatusQueryHandler(IAiQuotaService service, ICurrentUserService currentUser) : IRequestHandler<GetAiQuotaStatusQuery, AiQuotaStatus>
{
    public Task<AiQuotaStatus> Handle(GetAiQuotaStatusQuery request, CancellationToken cancellationToken)
        => service.GetStatusAsync(currentUser.TenantId!.Value, cancellationToken);
}
