using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Cases;

/// <summary>POST /api/v1/matters/{matterId}/cases (PRD §17, Module 5).</summary>
public sealed record CreateCourtCaseCommand(Guid MatterId, Guid CourtId, string CaseType, string CaseNumber, int CaseYear, string? CnrNumber, DateOnly? FilingDate, string? Stage, Guid? JudgeId, string? Courtroom) : IRequest<CourtCaseDto>;

public sealed class CreateCourtCaseCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<CreateCourtCaseCommand, CourtCaseDto>
{
    public Task<CourtCaseDto> Handle(CreateCourtCaseCommand request, CancellationToken cancellationToken)
        => courtCaseService.CreateAsync(
            currentUser.TenantId!.Value, request.MatterId,
            new CreateCourtCaseInput(request.CourtId, request.CaseType, request.CaseNumber, request.CaseYear, request.CnrNumber, request.FilingDate, request.Stage, request.JudgeId, request.Courtroom),
            cancellationToken);
}

/// <summary>PUT /api/v1/cases/{id}.</summary>
public sealed record UpdateCourtCaseCommand(Guid CaseId, string? CnrNumber, DateOnly? FilingDate, Guid? JudgeId, string? Courtroom) : IRequest<CourtCaseDto>;

public sealed class UpdateCourtCaseCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<UpdateCourtCaseCommand, CourtCaseDto>
{
    public Task<CourtCaseDto> Handle(UpdateCourtCaseCommand request, CancellationToken cancellationToken)
        => courtCaseService.UpdateAsync(currentUser.TenantId!.Value, request.CaseId, new UpdateCourtCaseInput(request.CnrNumber, request.FilingDate, request.JudgeId, request.Courtroom), cancellationToken);
}

/// <summary>POST /api/v1/cases/{id}/stage. case.stage.update permission distinct from read.</summary>
public sealed record ChangeCourtCaseStageCommand(Guid CaseId, string ToStage) : IRequest<CourtCaseDto>;

public sealed class ChangeCourtCaseStageCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<ChangeCourtCaseStageCommand, CourtCaseDto>
{
    public Task<CourtCaseDto> Handle(ChangeCourtCaseStageCommand request, CancellationToken cancellationToken)
        => courtCaseService.ChangeStageAsync(currentUser.TenantId!.Value, currentUser.UserId, request.CaseId, request.ToStage, cancellationToken);
}

/// <summary>POST /api/v1/cases/{id}/appeal {targetCourtId, carryDocumentIds[]}. AC-CC4.</summary>
public sealed record FileCourtCaseAppealCommand(Guid CaseId, Guid TargetCourtId, IReadOnlyList<Guid>? CarryDocumentIds) : IRequest<CourtCaseDto>;

public sealed class FileCourtCaseAppealCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<FileCourtCaseAppealCommand, CourtCaseDto>
{
    public Task<CourtCaseDto> Handle(FileCourtCaseAppealCommand request, CancellationToken cancellationToken)
        => courtCaseService.FileAppealAsync(currentUser.TenantId!.Value, currentUser.UserId, request.CaseId, request.TargetCourtId, request.CarryDocumentIds ?? [], cancellationToken);
}

/// <summary>POST /api/v1/cases/{id}/parties.</summary>
public sealed record AddCasePartyCommand(Guid CaseId, string PartyRole, string Name, string? AdvocateName, Guid? AdvocateUserId, string? ContactJson) : IRequest<CasePartyDto>;

public sealed class AddCasePartyCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<AddCasePartyCommand, CasePartyDto>
{
    public Task<CasePartyDto> Handle(AddCasePartyCommand request, CancellationToken cancellationToken)
        => courtCaseService.AddPartyAsync(currentUser.TenantId!.Value, request.CaseId, new CasePartyInput(request.PartyRole, request.Name, request.AdvocateName, request.AdvocateUserId, request.ContactJson ?? "{}"), cancellationToken);
}

/// <summary>PUT /api/v1/cases/{id}/parties/{partyId}.</summary>
public sealed record UpdateCasePartyCommand(Guid CaseId, Guid PartyId, string PartyRole, string Name, string? AdvocateName, Guid? AdvocateUserId, string? ContactJson) : IRequest<CasePartyDto>;

public sealed class UpdateCasePartyCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<UpdateCasePartyCommand, CasePartyDto>
{
    public Task<CasePartyDto> Handle(UpdateCasePartyCommand request, CancellationToken cancellationToken)
        => courtCaseService.UpdatePartyAsync(currentUser.TenantId!.Value, request.CaseId, request.PartyId, new CasePartyInput(request.PartyRole, request.Name, request.AdvocateName, request.AdvocateUserId, request.ContactJson ?? "{}"), cancellationToken);
}

/// <summary>DELETE /api/v1/cases/{id}/parties/{partyId}.</summary>
public sealed record DeleteCasePartyCommand(Guid CaseId, Guid PartyId) : IRequest;

public sealed class DeleteCasePartyCommandHandler(ICourtCaseService courtCaseService, ICurrentUserService currentUser) : IRequestHandler<DeleteCasePartyCommand>
{
    public async Task Handle(DeleteCasePartyCommand request, CancellationToken cancellationToken)
        => await courtCaseService.DeletePartyAsync(currentUser.TenantId!.Value, request.CaseId, request.PartyId, cancellationToken);
}
