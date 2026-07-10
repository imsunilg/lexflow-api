using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Leads;

/// <summary>POST /api/v1/leads/{id}/stage {toStage, note}. AC-L2.</summary>
public sealed record ChangeLeadStageCommand(Guid LeadId, string ToStage, string? Note) : IRequest<LeadDto>;

public sealed class ChangeLeadStageCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<ChangeLeadStageCommand, LeadDto>
{
    public Task<LeadDto> Handle(ChangeLeadStageCommand request, CancellationToken cancellationToken)
    {
        var allowSkip = currentUser.Permissions.Contains("leads.stage.skip");
        return leadService.ChangeStageAsync(currentUser.TenantId!.Value, currentUser.UserId, request.LeadId, request.ToStage, request.Note, allowSkip, cancellationToken);
    }
}

/// <summary>POST /api/v1/leads/{id}/activities (type=call|email|meeting|note).</summary>
public sealed record AddLeadActivityCommand(Guid LeadId, string ActivityType, string? Direction, int? DurationMin, string? Subject, string? Body, string? Outcome) : IRequest<LeadActivityDto>;

public sealed class AddLeadActivityCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<AddLeadActivityCommand, LeadActivityDto>
{
    public Task<LeadActivityDto> Handle(AddLeadActivityCommand request, CancellationToken cancellationToken)
        => leadService.AddActivityAsync(
            currentUser.TenantId!.Value, currentUser.UserId, request.LeadId,
            request.ActivityType, request.Direction, request.DurationMin, request.Subject, request.Body, request.Outcome,
            cancellationToken);
}

/// <summary>POST /api/v1/leads/{id}/assign {userId|ruleId}.</summary>
public sealed record AssignLeadCommand(Guid LeadId, Guid? UserId, Guid? RuleId) : IRequest<LeadDto>;

public sealed class AssignLeadCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<AssignLeadCommand, LeadDto>
{
    public Task<LeadDto> Handle(AssignLeadCommand request, CancellationToken cancellationToken)
        => leadService.AssignAsync(currentUser.TenantId!.Value, request.LeadId, request.UserId, request.RuleId, cancellationToken);
}

/// <summary>POST /api/v1/leads/{id}/convert {createMatter, matterPayload?, invoicePayload?}. AC-L3: atomic.</summary>
public sealed record ConvertLeadCommand(Guid LeadId, bool CreateMatter, string? MatterPayloadJson, string? InvoicePayloadJson) : IRequest<ConvertLeadResult>;

public sealed class ConvertLeadCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<ConvertLeadCommand, ConvertLeadResult>
{
    public Task<ConvertLeadResult> Handle(ConvertLeadCommand request, CancellationToken cancellationToken)
    {
        var force = currentUser.Permissions.Contains("leads.convert.force");
        return leadService.ConvertAsync(
            currentUser.TenantId!.Value, currentUser.UserId, request.LeadId,
            request.CreateMatter, request.MatterPayloadJson, request.InvoicePayloadJson, force,
            cancellationToken);
    }
}

/// <summary>POST /api/v1/leads/{id}/lost {reasonId, note}.</summary>
public sealed record MarkLeadLostCommand(Guid LeadId, Guid ReasonId, string? Note) : IRequest<LeadDto>;

public sealed class MarkLeadLostCommandHandler(ILeadService leadService, ICurrentUserService currentUser) : IRequestHandler<MarkLeadLostCommand, LeadDto>
{
    public Task<LeadDto> Handle(MarkLeadLostCommand request, CancellationToken cancellationToken)
        => leadService.MarkLostAsync(currentUser.TenantId!.Value, currentUser.UserId, request.LeadId, request.ReasonId, request.Note, cancellationToken);
}
