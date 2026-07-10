using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Kb;

public sealed record UploadKbJudgmentCommand(string Citation, string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties, string? Headnote, byte[] FileContent, string FileName, string? Mime) : IRequest<KbJudgmentDto>;

public sealed class UploadKbJudgmentCommandHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<UploadKbJudgmentCommand, KbJudgmentDto>
{
    public Task<KbJudgmentDto> Handle(UploadKbJudgmentCommand request, CancellationToken cancellationToken)
        => service.UploadAsync(
            currentUser.TenantId!.Value, currentUser.UserId,
            new UploadJudgmentInput(request.Citation, request.NeutralCitation, request.CourtId, request.DecisionDate, request.Parties, request.Headnote),
            request.FileContent, request.FileName, request.Mime, cancellationToken);
}

public sealed record UpdateKbJudgmentCommand(Guid Id, string? NeutralCitation, Guid? CourtId, DateOnly? DecisionDate, string? Parties) : IRequest<KbJudgmentDto>;

public sealed class UpdateKbJudgmentCommandHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<UpdateKbJudgmentCommand, KbJudgmentDto>
{
    public Task<KbJudgmentDto> Handle(UpdateKbJudgmentCommand request, CancellationToken cancellationToken)
        => service.UpdateAsync(currentUser.TenantId!.Value, request.Id, new UpdateJudgmentInput(request.NeutralCitation, request.CourtId, request.DecisionDate, request.Parties), cancellationToken);
}

public sealed record UpdateKbJudgmentHeadnoteCommand(Guid Id, string? Headnote) : IRequest<KbJudgmentDto>;

public sealed class UpdateKbJudgmentHeadnoteCommandHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<UpdateKbJudgmentHeadnoteCommand, KbJudgmentDto>
{
    public Task<KbJudgmentDto> Handle(UpdateKbJudgmentHeadnoteCommand request, CancellationToken cancellationToken)
        => service.UpdateHeadnoteAsync(currentUser.TenantId!.Value, request.Id, request.Headnote, cancellationToken);
}

public sealed record RetryKbJudgmentExtractionCommand(Guid Id) : IRequest;

public sealed class RetryKbJudgmentExtractionCommandHandler(IKbJudgmentService service, ICurrentUserService currentUser) : IRequestHandler<RetryKbJudgmentExtractionCommand>
{
    public async Task Handle(RetryKbJudgmentExtractionCommand request, CancellationToken cancellationToken)
        => await service.RetryExtractionAsync(currentUser.TenantId!.Value, request.Id, cancellationToken);
}
