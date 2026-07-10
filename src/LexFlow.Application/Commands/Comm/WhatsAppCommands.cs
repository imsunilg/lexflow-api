using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Comm;

/// <summary>POST /api/v1/comm/whatsapp/send {clientId, templateId?, variables?, sessionText?}. AC-CM2.</summary>
public sealed record SendWhatsAppCommand(Guid ClientId, Guid? TemplateId, IReadOnlyDictionary<string, string>? Variables, string? SessionText) : IRequest<WhatsappMessageDto>;

public sealed class SendWhatsAppCommandHandler(IWhatsAppService whatsAppService, ICurrentUserService currentUser) : IRequestHandler<SendWhatsAppCommand, WhatsappMessageDto>
{
    public Task<WhatsappMessageDto> Handle(SendWhatsAppCommand request, CancellationToken cancellationToken)
        => whatsAppService.SendAsync(currentUser.TenantId!.Value, new SendWhatsAppInput(request.ClientId, request.TemplateId, request.Variables, request.SessionText), cancellationToken);
}

/// <summary>Records a client's WhatsApp opt-in (mandatory before the first template send).</summary>
public sealed record OptInWhatsAppCommand(Guid ClientId, string PhoneE164, string? Source) : IRequest<WhatsAppOptinDto>;

public sealed class OptInWhatsAppCommandHandler(IWhatsAppService whatsAppService, ICurrentUserService currentUser) : IRequestHandler<OptInWhatsAppCommand, WhatsAppOptinDto>
{
    public Task<WhatsAppOptinDto> Handle(OptInWhatsAppCommand request, CancellationToken cancellationToken)
        => whatsAppService.OptInAsync(currentUser.TenantId!.Value, request.ClientId, request.PhoneE164, request.Source, cancellationToken);
}

public sealed record OptOutWhatsAppCommand(Guid ClientId) : IRequest;

public sealed class OptOutWhatsAppCommandHandler(IWhatsAppService whatsAppService, ICurrentUserService currentUser) : IRequestHandler<OptOutWhatsAppCommand>
{
    public async Task Handle(OptOutWhatsAppCommand request, CancellationToken cancellationToken)
        => await whatsAppService.OptOutAsync(currentUser.TenantId!.Value, request.ClientId, cancellationToken);
}
