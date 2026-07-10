using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Ai;

public sealed record AiChatCommand(AiChatRequest Request) : IRequest<AiChatResponse>;

public sealed class AiChatCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiChatCommand, AiChatResponse>
{
    public Task<AiChatResponse> Handle(AiChatCommand request, CancellationToken cancellationToken)
        => service.ChatAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.Request, cancellationToken);
}

public sealed record AiSummarizeDocumentCommand(Guid DocumentId, string? LengthPreset) : IRequest<AiDocumentSummaryResponse>;

public sealed class AiSummarizeDocumentCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiSummarizeDocumentCommand, AiDocumentSummaryResponse>
{
    public Task<AiDocumentSummaryResponse> Handle(AiSummarizeDocumentCommand request, CancellationToken cancellationToken)
        => service.SummarizeDocumentAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.DocumentId, request.LengthPreset, cancellationToken);
}

public sealed record AiSummarizeMatterCommand(Guid MatterId) : IRequest<AiMatterSummaryResponse>;

public sealed class AiSummarizeMatterCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiSummarizeMatterCommand, AiMatterSummaryResponse>
{
    public Task<AiMatterSummaryResponse> Handle(AiSummarizeMatterCommand request, CancellationToken cancellationToken)
        => service.SummarizeMatterAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.MatterId, cancellationToken);
}

public sealed record AiReviewContractCommand(Guid DocumentId) : IRequest<AiContractReviewResponse>;

public sealed class AiReviewContractCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiReviewContractCommand, AiContractReviewResponse>
{
    public Task<AiContractReviewResponse> Handle(AiReviewContractCommand request, CancellationToken cancellationToken)
        => service.ReviewContractAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.DocumentId, cancellationToken);
}

public sealed record AiDraftCommand(AiDraftRequest Request) : IRequest<AiDraftResponse>;

public sealed class AiDraftCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiDraftCommand, AiDraftResponse>
{
    public Task<AiDraftResponse> Handle(AiDraftCommand request, CancellationToken cancellationToken)
        => service.DraftAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.Request, cancellationToken);
}

public sealed record AiResearchCommand(string Question, bool WebGroundedMode) : IRequest<AiResearchResponse>;

public sealed class AiResearchCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiResearchCommand, AiResearchResponse>
{
    public Task<AiResearchResponse> Handle(AiResearchCommand request, CancellationToken cancellationToken)
        => service.ResearchAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.Question, request.WebGroundedMode, cancellationToken);
}

public sealed record AiSimilarMattersCommand(Guid MatterId, int TopN) : IRequest<AiSimilarMattersResponse>;

public sealed class AiSimilarMattersCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiSimilarMattersCommand, AiSimilarMattersResponse>
{
    public Task<AiSimilarMattersResponse> Handle(AiSimilarMattersCommand request, CancellationToken cancellationToken)
        => service.GetSimilarMattersAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.MatterId, request.TopN, cancellationToken);
}

public sealed record AiHearingPredictionCommand(Guid CaseId) : IRequest<AiHearingPredictionResponse>;

public sealed class AiHearingPredictionCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiHearingPredictionCommand, AiHearingPredictionResponse>
{
    public Task<AiHearingPredictionResponse> Handle(AiHearingPredictionCommand request, CancellationToken cancellationToken)
        => service.PredictNextHearingAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.CaseId, cancellationToken);
}

public sealed record AiRiskAnalysisCommand(Guid MatterId) : IRequest<AiRiskAnalysisResponse>;

public sealed class AiRiskAnalysisCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiRiskAnalysisCommand, AiRiskAnalysisResponse>
{
    public Task<AiRiskAnalysisResponse> Handle(AiRiskAnalysisCommand request, CancellationToken cancellationToken)
        => service.GetRiskAnalysisAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.MatterId, cancellationToken);
}

public sealed record AiGenerateEmailCommand(AiEmailGenerateRequest Request) : IRequest<AiEmailGenerateResponse>;

public sealed class AiGenerateEmailCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiGenerateEmailCommand, AiEmailGenerateResponse>
{
    public Task<AiEmailGenerateResponse> Handle(AiGenerateEmailCommand request, CancellationToken cancellationToken)
        => service.GenerateEmailAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.Request, cancellationToken);
}

public sealed record AiSummarizeMeetingCommand(Guid TranscriptionId) : IRequest<AiMeetingSummaryResponse>;

public sealed class AiSummarizeMeetingCommandHandler(IAiAssistantService service, ICurrentUserService currentUser) : IRequestHandler<AiSummarizeMeetingCommand, AiMeetingSummaryResponse>
{
    public Task<AiMeetingSummaryResponse> Handle(AiSummarizeMeetingCommand request, CancellationToken cancellationToken)
        => service.SummarizeMeetingAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, currentUser.Permissions, request.TranscriptionId, cancellationToken);
}

public sealed record AiRequestTranscriptionCommand(Guid? MatterId, byte[] AudioContent, string FileName, string ContentType, string? Language) : IRequest<AiTranscriptionDto>;

public sealed class AiRequestTranscriptionCommandHandler(IAiTranscriptionService service, ICurrentUserService currentUser) : IRequestHandler<AiRequestTranscriptionCommand, AiTranscriptionDto>
{
    public Task<AiTranscriptionDto> Handle(AiRequestTranscriptionCommand request, CancellationToken cancellationToken)
        => service.RequestAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.MatterId, request.AudioContent, request.FileName, request.ContentType, request.Language, cancellationToken);
}

public sealed record AiRecordFeedbackCommand(Guid InteractionId, int Rating, string? Reason) : IRequest;

public sealed class AiRecordFeedbackCommandHandler(IAiInteractionAuditService service, ICurrentUserService currentUser) : IRequestHandler<AiRecordFeedbackCommand>
{
    public async Task Handle(AiRecordFeedbackCommand request, CancellationToken cancellationToken)
        => await service.RecordFeedbackAsync(currentUser.TenantId!.Value, request.InteractionId, request.Rating, request.Reason, cancellationToken);
}
