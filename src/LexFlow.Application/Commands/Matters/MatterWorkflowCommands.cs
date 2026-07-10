using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Matters;

/// <summary>POST /api/v1/matters/{id}/status {toStatus, outcome?, closureNote?}. AC-M3.</summary>
public sealed record ChangeMatterStatusCommand(Guid MatterId, string ToStatus, string? Outcome, string? ClosureNote) : IRequest<MatterDto>;

public sealed class ChangeMatterStatusCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<ChangeMatterStatusCommand, MatterDto>
{
    public Task<MatterDto> Handle(ChangeMatterStatusCommand request, CancellationToken cancellationToken)
    {
        var allowReopen = currentUser.Permissions.Contains("matter.reopen");
        return matterService.ChangeStatusAsync(currentUser.TenantId!.Value, currentUser.UserId, request.MatterId, request.ToStatus, request.Outcome, request.ClosureNote, allowReopen, cancellationToken);
    }
}

/// <summary>POST /api/v1/matters/{id}/team.</summary>
public sealed record AddMatterTeamMemberCommand(Guid MatterId, Guid UserId, string? RoleInMatter, decimal? RateOverride) : IRequest;

public sealed class AddMatterTeamMemberCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<AddMatterTeamMemberCommand>
{
    public async Task Handle(AddMatterTeamMemberCommand request, CancellationToken cancellationToken)
        => await matterService.AddTeamMemberAsync(currentUser.TenantId!.Value, request.MatterId, request.UserId, request.RoleInMatter, request.RateOverride, cancellationToken);
}

/// <summary>DELETE /api/v1/matters/{id}/team.</summary>
public sealed record RemoveMatterTeamMemberCommand(Guid MatterId, Guid UserId) : IRequest;

public sealed class RemoveMatterTeamMemberCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<RemoveMatterTeamMemberCommand>
{
    public async Task Handle(RemoveMatterTeamMemberCommand request, CancellationToken cancellationToken)
        => await matterService.RemoveTeamMemberAsync(currentUser.TenantId!.Value, request.MatterId, request.UserId, cancellationToken);
}

/// <summary>POST /api/v1/matters/{id}/parties.</summary>
public sealed record AddMatterPartyCommand(Guid MatterId, string Name, string PartyRole, string? AdvocateName, string? ContactJson) : IRequest<MatterPartyDto>;

public sealed class AddMatterPartyCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<AddMatterPartyCommand, MatterPartyDto>
{
    public Task<MatterPartyDto> Handle(AddMatterPartyCommand request, CancellationToken cancellationToken)
        => matterService.AddPartyAsync(currentUser.TenantId!.Value, request.MatterId, new MatterPartyInput(request.Name, request.PartyRole, request.AdvocateName, request.ContactJson ?? "{}"), cancellationToken);
}

/// <summary>PUT /api/v1/matters/{id}/parties/{partyId}.</summary>
public sealed record UpdateMatterPartyCommand(Guid MatterId, Guid PartyId, string Name, string PartyRole, string? AdvocateName, string? ContactJson) : IRequest<MatterPartyDto>;

public sealed class UpdateMatterPartyCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<UpdateMatterPartyCommand, MatterPartyDto>
{
    public Task<MatterPartyDto> Handle(UpdateMatterPartyCommand request, CancellationToken cancellationToken)
        => matterService.UpdatePartyAsync(currentUser.TenantId!.Value, request.MatterId, request.PartyId, new MatterPartyInput(request.Name, request.PartyRole, request.AdvocateName, request.ContactJson ?? "{}"), cancellationToken);
}

/// <summary>DELETE /api/v1/matters/{id}/parties/{partyId}.</summary>
public sealed record DeleteMatterPartyCommand(Guid MatterId, Guid PartyId) : IRequest;

public sealed class DeleteMatterPartyCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<DeleteMatterPartyCommand>
{
    public async Task Handle(DeleteMatterPartyCommand request, CancellationToken cancellationToken)
        => await matterService.DeletePartyAsync(currentUser.TenantId!.Value, request.MatterId, request.PartyId, cancellationToken);
}

/// <summary>POST /api/v1/matters/{id}/important-dates. BR-2.</summary>
public sealed record AddMatterImportantDateCommand(Guid MatterId, string Kind, string Title, DateTimeOffset DueAt, string? ReminderPolicyJson, string? Severity) : IRequest<MatterImportantDateDto>;

public sealed class AddMatterImportantDateCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<AddMatterImportantDateCommand, MatterImportantDateDto>
{
    public Task<MatterImportantDateDto> Handle(AddMatterImportantDateCommand request, CancellationToken cancellationToken)
        => matterService.AddImportantDateAsync(currentUser.TenantId!.Value, request.MatterId, new ImportantDateInput(request.Kind, request.Title, request.DueAt, request.ReminderPolicyJson, request.Severity), cancellationToken);
}

/// <summary>PUT /api/v1/matters/{id}/important-dates/{dateId}.</summary>
public sealed record UpdateMatterImportantDateCommand(Guid MatterId, Guid DateId, string Kind, string Title, DateTimeOffset DueAt, string? ReminderPolicyJson, string? Severity) : IRequest<MatterImportantDateDto>;

public sealed class UpdateMatterImportantDateCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<UpdateMatterImportantDateCommand, MatterImportantDateDto>
{
    public Task<MatterImportantDateDto> Handle(UpdateMatterImportantDateCommand request, CancellationToken cancellationToken)
        => matterService.UpdateImportantDateAsync(currentUser.TenantId!.Value, request.MatterId, request.DateId, new ImportantDateInput(request.Kind, request.Title, request.DueAt, request.ReminderPolicyJson, request.Severity), cancellationToken);
}

/// <summary>DELETE /api/v1/matters/{id}/important-dates/{dateId}. BR-2 30-day guard enforced at the DB layer.</summary>
public sealed record DeleteMatterImportantDateCommand(Guid MatterId, Guid DateId) : IRequest;

public sealed class DeleteMatterImportantDateCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<DeleteMatterImportantDateCommand>
{
    public async Task Handle(DeleteMatterImportantDateCommand request, CancellationToken cancellationToken)
        => await matterService.DeleteImportantDateAsync(currentUser.TenantId!.Value, request.MatterId, request.DateId, cancellationToken);
}

/// <summary>POST /api/v1/matters/{id}/expenses.</summary>
public sealed record AddMatterExpenseCommand(Guid MatterId, DateOnly IncurredOn, string? Category, string? Description, decimal Amount, bool Billable, Guid? ReceiptDocumentId) : IRequest<MatterExpenseDto>;

public sealed class AddMatterExpenseCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<AddMatterExpenseCommand, MatterExpenseDto>
{
    public Task<MatterExpenseDto> Handle(AddMatterExpenseCommand request, CancellationToken cancellationToken)
        => matterService.AddExpenseAsync(currentUser.TenantId!.Value, request.MatterId, new MatterExpenseInput(request.IncurredOn, request.Category, request.Description, request.Amount, request.Billable, request.ReceiptDocumentId), cancellationToken);
}

/// <summary>POST /api/v1/matters/{id}/related.</summary>
public sealed record AddMatterRelatedCommand(Guid MatterId, Guid RelatedMatterId, string RelationType) : IRequest<MatterRelatedDto>;

public sealed class AddMatterRelatedCommandHandler(IMatterService matterService, ICurrentUserService currentUser) : IRequestHandler<AddMatterRelatedCommand, MatterRelatedDto>
{
    public Task<MatterRelatedDto> Handle(AddMatterRelatedCommand request, CancellationToken cancellationToken)
        => matterService.AddRelatedAsync(currentUser.TenantId!.Value, request.MatterId, request.RelatedMatterId, request.RelationType, cancellationToken);
}
