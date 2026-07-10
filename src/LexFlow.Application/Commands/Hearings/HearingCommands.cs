using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Hearings;

/// <summary>POST /api/v1/cases/{id}/hearings.</summary>
public sealed record CreateHearingCommand(Guid CaseId, DateOnly Date, TimeOnly? Time, string? Purpose, string? Courtroom, Guid? AssignedLawyerId) : IRequest<HearingDto>;

public sealed class CreateHearingCommandHandler(IHearingService hearingService, ICurrentUserService currentUser) : IRequestHandler<CreateHearingCommand, HearingDto>
{
    public Task<HearingDto> Handle(CreateHearingCommand request, CancellationToken cancellationToken)
    {
        var allowBackdate = currentUser.Permissions.Contains("hearing.backdate");
        return hearingService.CreateAsync(currentUser.TenantId!.Value, request.CaseId, new CreateHearingInput(request.Date, request.Time, request.Purpose, request.Courtroom, request.AssignedLawyerId, allowBackdate), cancellationToken);
    }
}

/// <summary>
/// POST /api/v1/hearings/{id}/outcome {summary, nextHearing{date,purpose}?, adjournReason?, orders[]}.
/// AC-CC1/G-AC2: routes straight to IHearingService.RecordOutcomeAsync — the single insertion
/// point for the outcome+next-hearing+reminders transaction (see HearingService's own doc comment).
/// </summary>
public sealed record RecordHearingOutcomeCommand(
    Guid HearingId,
    string Summary,
    string? AdjournReason,
    DateOnly? NextHearingDate,
    TimeOnly? NextHearingTime,
    string? NextHearingPurpose,
    bool SineDie,
    bool Disposed,
    IReadOnlyList<CreateOrderInput>? Orders,
    bool CreateComplianceTask) : IRequest<RecordOutcomeResult>;

public sealed class RecordHearingOutcomeCommandHandler(IHearingService hearingService, ICurrentUserService currentUser) : IRequestHandler<RecordHearingOutcomeCommand, RecordOutcomeResult>
{
    public Task<RecordOutcomeResult> Handle(RecordHearingOutcomeCommand request, CancellationToken cancellationToken)
        => hearingService.RecordOutcomeAsync(
            currentUser.TenantId!.Value, currentUser.UserId, request.HearingId,
            new RecordOutcomeInput(request.Summary, request.AdjournReason, request.NextHearingDate, request.NextHearingTime, request.NextHearingPurpose, request.SineDie, request.Disposed, request.Orders, request.CreateComplianceTask),
            cancellationToken);
}
