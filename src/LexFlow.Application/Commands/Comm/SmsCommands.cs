using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Comm;

/// <summary>POST /api/v1/comm/sms/send. AC-CM4: DLT enforcement happens inside ISmsService.</summary>
public sealed record SendSmsCommand(Guid? ClientId, Guid? MatterId, string ToNumber, Guid? TemplateId, string? FreeformBody, IReadOnlyDictionary<string, string>? Variables) : IRequest<SmsMessageDto>;

public sealed class SendSmsCommandHandler(ISmsService smsService, ICurrentUserService currentUser) : IRequestHandler<SendSmsCommand, SmsMessageDto>
{
    public Task<SmsMessageDto> Handle(SendSmsCommand request, CancellationToken cancellationToken)
        => smsService.SendAsync(currentUser.TenantId!.Value, new SendSmsInput(request.ClientId, request.MatterId, request.ToNumber, request.TemplateId, request.FreeformBody, request.Variables), cancellationToken);
}
