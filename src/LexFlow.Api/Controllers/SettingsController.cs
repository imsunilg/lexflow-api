using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Gateways;
using LexFlow.Application.Commands.Settings;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Gateways;
using LexFlow.Application.Queries.Settings;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>
/// Module 15 Settings (PRD §17): generic per-section GET/PUT + audit + the SMTP/SMS/
/// WhatsApp/gateway test-send &amp; verify endpoints. Number Series/Tax Rates/Templates
/// CRUD live on their own controllers (NumberSeriesController, TaxRatesController,
/// TemplatesController) since PRD lists them as distinct CRUD resources.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet("audit")]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> GetAudit([FromQuery] string? section, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SettingsAuditEntryDto>>.Of(await mediator.Send(new GetSettingsAuditQuery(section), cancellationToken)));

    [HttpGet("gateways")]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> GetGateways(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<GatewayConfigDto>>.Of(await mediator.Send(new GetGatewayConfigsQuery(), cancellationToken)));

    [HttpPost("smtp/test")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> TestSmtp([FromBody] TestSmtpCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<ExternalCallResult>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPost("sms/test")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> TestSms([FromBody] TestSmsCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<ExternalCallResult>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPost("whatsapp/sync-templates")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> SyncWhatsAppTemplates([FromBody] SyncWhatsAppTemplatesCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<ExternalCallResult>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPost("whatsapp/test")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> TestWhatsApp([FromBody] TestWhatsAppCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<ExternalCallResult>.Of(await mediator.Send(command, cancellationToken)));

    /// <summary>AC-S1: verify fails → save is blocked (never called) — the controller only saves when verification succeeds.</summary>
    [HttpPost("gateways/{provider}/verify")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> VerifyGateway(string provider, [FromBody] VerifyGatewayRequest request, CancellationToken cancellationToken)
    {
        var verifyResult = await mediator.Send(new VerifyPaymentGatewayCommand(provider, request.ConfigJson, request.Secret, request.IsTestMode), cancellationToken);
        if (!verifyResult.Success)
        {
            return UnprocessableEntity(ApiResponse<ExternalCallResult>.Of(verifyResult));
        }

        if (request.Save)
        {
            var saved = await mediator.Send(new SavePaymentGatewayCommand(provider, request.ConfigJson, request.Secret, IsEnabled: true, request.IsTestMode), cancellationToken);
            return Ok(ApiResponse<GatewayConfigDto>.Of(saved));
        }

        return Ok(ApiResponse<ExternalCallResult>.Of(verifyResult));
    }

    // Route last so it doesn't swallow the more specific routes above (audit, gateways, smtp/test, ...).
    [HttpGet("{section}")]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> GetSection(string section, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Of(RawJson(await mediator.Send(new GetSettingsSectionQuery(section), cancellationToken))));

    [HttpPut("{section}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> PutSection(string section, [FromBody] System.Text.Json.JsonElement value, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Of(RawJson(await mediator.Send(new UpdateSettingsSectionCommand(section, value.GetRawText()), cancellationToken))));

    private static object RawJson(string json) => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
}

public sealed record VerifyGatewayRequest(string ConfigJson, string? Secret, bool IsTestMode, bool Save);
