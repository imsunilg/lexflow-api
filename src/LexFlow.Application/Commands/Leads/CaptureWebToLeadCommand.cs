using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Leads;

/// <summary>
/// POST /api/v1/public/web-to-lead/{formKey} (unauthenticated). TenantId is resolved by the
/// controller from the form key (no ICurrentUserService — there is no authenticated principal
/// on this path). Module 2 Security Rules: "web-to-lead endpoint writes only, returns no data" —
/// including on a honeypot trip, which is absorbed silently rather than surfaced to the caller.
/// </summary>
public sealed record CaptureWebToLeadCommand(Guid TenantId, string FirstName, string? LastName, string? Email, string? PhoneE164, string? IssueSummary, string? Honeypot) : IRequest;

public sealed class CaptureWebToLeadCommandHandler(ILeadService leadService) : IRequestHandler<CaptureWebToLeadCommand>
{
    public async Task Handle(CaptureWebToLeadCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Honeypot))
        {
            return;
        }

        await leadService.CaptureFromWebToLeadAsync(
            request.TenantId,
            new WebToLeadInput(request.FirstName, request.LastName, request.Email, request.PhoneE164, request.IssueSummary, request.Honeypot),
            cancellationToken);
    }
}
