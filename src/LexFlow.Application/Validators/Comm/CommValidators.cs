using FluentValidation;
using LexFlow.Application.Commands.Comm;

namespace LexFlow.Application.Validators.Comm;

public sealed class SendEmailCommandValidator : AbstractValidator<SendEmailCommand>
{
    public SendEmailCommandValidator()
    {
        RuleFor(x => x.MailboxId).NotEmpty();
        RuleFor(x => x.ToAddresses).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.BodyHtml).NotEmpty();
        RuleFor(x => x.Attachments).Must(a => a is null || a.Sum(x => x.Content.LongLength) <= 25 * 1024 * 1024)
            .WithMessage("Outbound email attachments cannot exceed 25 MB total.");
    }
}

public sealed class LinkEmailThreadCommandValidator : AbstractValidator<LinkEmailThreadCommand>
{
    public LinkEmailThreadCommandValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.MatterId).NotEmpty();
    }
}

/// <summary>AC-CM4: enforcement itself lives in ISmsService (needs the tenant's live compliance config); this only validates the request shape.</summary>
public sealed class SendSmsCommandValidator : AbstractValidator<SendSmsCommand>
{
    public SendSmsCommandValidator()
    {
        RuleFor(x => x.ToNumber).NotEmpty();
        RuleFor(x => x).Must(x => x.TemplateId.HasValue || !string.IsNullOrWhiteSpace(x.FreeformBody))
            .WithMessage("Either templateId or freeformBody is required.");
    }
}

public sealed class SendWhatsAppCommandValidator : AbstractValidator<SendWhatsAppCommand>
{
    public SendWhatsAppCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x).Must(x => x.TemplateId.HasValue || !string.IsNullOrWhiteSpace(x.SessionText))
            .WithMessage("Either templateId or sessionText is required.");
    }
}

public sealed class OptInWhatsAppCommandValidator : AbstractValidator<OptInWhatsAppCommand>
{
    public OptInWhatsAppCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.PhoneE164).NotEmpty().Matches(@"^\+[1-9]\d{6,14}$").WithMessage("Phone number must be in E.164 format.");
    }
}

public sealed class LogCallCommandValidator : AbstractValidator<LogCallCommand>
{
    private static readonly string[] Directions = ["Inbound", "Outbound"];

    public LogCallCommandValidator()
    {
        RuleFor(x => x.Direction).Must(d => Directions.Contains(d)).WithMessage($"Direction must be one of {string.Join('|', Directions)}.");
        RuleFor(x => x.DurationSec).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FollowUpTaskTitle).NotEmpty().When(x => x.CreateFollowUpTask).WithMessage("A title is required to create a follow-up task.");
    }
}

public sealed class ClickToCallCommandValidator : AbstractValidator<ClickToCallCommand>
{
    public ClickToCallCommandValidator()
    {
        RuleFor(x => x.ToNumber).NotEmpty();
    }
}

public sealed class CreateChatChannelCommandValidator : AbstractValidator<CreateChatChannelCommand>
{
    private static readonly string[] Kinds = ["Firm", "Team", "Matter", "DM"];

    public CreateChatChannelCommandValidator()
    {
        RuleFor(x => x.Kind).Must(k => Kinds.Contains(k)).WithMessage($"Kind must be one of {string.Join('|', Kinds)}.");
        RuleFor(x => x.MemberUserIds).NotEmpty();
    }
}

public sealed class PostChatMessageCommandValidator : AbstractValidator<PostChatMessageCommand>
{
    public PostChatMessageCommandValidator()
    {
        RuleFor(x => x.ChannelId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
    }
}
