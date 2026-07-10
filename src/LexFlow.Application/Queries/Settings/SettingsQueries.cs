using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Settings;

/// <summary>GET /api/v1/settings/{section}.</summary>
public sealed record GetSettingsSectionQuery(string Section) : IRequest<string>;

public sealed class GetSettingsSectionQueryHandler(ISettingsService settingsService, ICurrentUserService currentUser)
    : IRequestHandler<GetSettingsSectionQuery, string>
{
    public Task<string> Handle(GetSettingsSectionQuery request, CancellationToken cancellationToken)
        => settingsService.GetSectionAsync(currentUser.TenantId!.Value, request.Section, cancellationToken);
}

/// <summary>GET /api/v1/settings/audit?section= (PRD §17, §30, AC-S3).</summary>
public sealed record GetSettingsAuditQuery(string? Section) : IRequest<IReadOnlyList<SettingsAuditEntryDto>>;

public sealed class GetSettingsAuditQueryHandler(ISettingsService settingsService, ICurrentUserService currentUser)
    : IRequestHandler<GetSettingsAuditQuery, IReadOnlyList<SettingsAuditEntryDto>>
{
    public Task<IReadOnlyList<SettingsAuditEntryDto>> Handle(GetSettingsAuditQuery request, CancellationToken cancellationToken)
        => settingsService.GetAuditAsync(currentUser.TenantId!.Value, request.Section, cancellationToken);
}
