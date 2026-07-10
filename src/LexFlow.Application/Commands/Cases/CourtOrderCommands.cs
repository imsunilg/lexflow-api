using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Cases;

/// <summary>POST /api/v1/cases/{id}/orders (multipart).</summary>
public sealed record AddCourtOrderCommand(Guid CaseId, Guid? HearingId, DateOnly OrderDate, string? Gist, DateOnly? ComplianceDue, Guid? DocumentId) : IRequest<CourtOrderDto>;

public sealed class AddCourtOrderCommandHandler(ICourtOrderService courtOrderService, ICurrentUserService currentUser) : IRequestHandler<AddCourtOrderCommand, CourtOrderDto>
{
    public Task<CourtOrderDto> Handle(AddCourtOrderCommand request, CancellationToken cancellationToken)
        => courtOrderService.AddAsync(currentUser.TenantId!.Value, request.CaseId, request.HearingId, request.OrderDate, request.Gist, request.ComplianceDue, request.DocumentId, cancellationToken);
}

/// <summary>POST /api/v1/cases/{id}/evidence.</summary>
public sealed record AddEvidenceItemCommand(Guid CaseId, string? ExhibitNo, string Kind, string? Description, Guid? DocumentId) : IRequest<EvidenceItemDto>;

public sealed class AddEvidenceItemCommandHandler(IEvidenceService evidenceService, ICurrentUserService currentUser) : IRequestHandler<AddEvidenceItemCommand, EvidenceItemDto>
{
    public Task<EvidenceItemDto> Handle(AddEvidenceItemCommand request, CancellationToken cancellationToken)
        => evidenceService.AddAsync(currentUser.TenantId!.Value, request.CaseId, request.ExhibitNo, request.Kind, request.Description, request.DocumentId, cancellationToken);
}

/// <summary>POST /api/v1/evidence/{id}/custody.</summary>
public sealed record AddEvidenceCustodyEventCommand(Guid EvidenceId, string Action, string? Holder, string? Note) : IRequest<EvidenceCustodyLogDto>;

public sealed class AddEvidenceCustodyEventCommandHandler(IEvidenceService evidenceService, ICurrentUserService currentUser) : IRequestHandler<AddEvidenceCustodyEventCommand, EvidenceCustodyLogDto>
{
    public Task<EvidenceCustodyLogDto> Handle(AddEvidenceCustodyEventCommand request, CancellationToken cancellationToken)
        => evidenceService.AddCustodyEventAsync(currentUser.TenantId!.Value, request.EvidenceId, request.Action, request.Holder, request.Note, cancellationToken);
}

/// <summary>POST /api/v1/cases/{id}/witnesses.</summary>
public sealed record AddWitnessCommand(Guid CaseId, string Name, string? Side, string? ContactJson, DateOnly? ScheduledOn) : IRequest<WitnessDto>;

public sealed class AddWitnessCommandHandler(IWitnessService witnessService, ICurrentUserService currentUser) : IRequestHandler<AddWitnessCommand, WitnessDto>
{
    public Task<WitnessDto> Handle(AddWitnessCommand request, CancellationToken cancellationToken)
        => witnessService.AddAsync(currentUser.TenantId!.Value, request.CaseId, request.Name, request.Side, request.ContactJson ?? "{}", request.ScheduledOn, cancellationToken);
}

/// <summary>PUT /api/v1/cases/{id}/witnesses/{witnessId}.</summary>
public sealed record UpdateWitnessCommand(Guid CaseId, Guid WitnessId, string Name, string? Side, string? ContactJson, DateOnly? ScheduledOn, string? ExamStatus) : IRequest<WitnessDto>;

public sealed class UpdateWitnessCommandHandler(IWitnessService witnessService, ICurrentUserService currentUser) : IRequestHandler<UpdateWitnessCommand, WitnessDto>
{
    public Task<WitnessDto> Handle(UpdateWitnessCommand request, CancellationToken cancellationToken)
        => witnessService.UpdateAsync(currentUser.TenantId!.Value, request.CaseId, request.WitnessId, request.Name, request.Side, request.ContactJson ?? "{}", request.ScheduledOn, request.ExamStatus, cancellationToken);
}

/// <summary>POST /api/v1/cases/{id}/arguments.</summary>
public sealed record AddArgumentNoteCommand(Guid CaseId, Guid? HearingId, string? Stage, string Body, IReadOnlyList<Guid>? CitationJudgmentIds) : IRequest<ArgumentNoteDto>;

public sealed class AddArgumentNoteCommandHandler(IArgumentNoteService argumentNoteService, ICurrentUserService currentUser) : IRequestHandler<AddArgumentNoteCommand, ArgumentNoteDto>
{
    public Task<ArgumentNoteDto> Handle(AddArgumentNoteCommand request, CancellationToken cancellationToken)
        => argumentNoteService.AddAsync(currentUser.TenantId!.Value, request.CaseId, request.HearingId, request.Stage, request.Body, request.CitationJudgmentIds, cancellationToken);
}
