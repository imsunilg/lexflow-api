using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Settings;

/// <summary>PUT /api/v1/settings/{section} (PRD §17, Module 15: "validated per-section schema").</summary>
public sealed record UpdateSettingsSectionCommand(string Section, string ValueJson) : IRequest<string>;

public sealed class UpdateSettingsSectionCommandHandler(ISettingsService settingsService, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSettingsSectionCommand, string>
{
    public Task<string> Handle(UpdateSettingsSectionCommand request, CancellationToken cancellationToken)
        => settingsService.UpdateSectionAsync(currentUser.TenantId!.Value, currentUser.UserId!.Value, request.Section, request.ValueJson, cancellationToken);
}
