using FluentValidation;
using LexFlow.Application.Commands.Documents;
using LexFlow.Application.Commands.Folders;

namespace LexFlow.Application.Validators.Documents;

public sealed class CreateFolderCommandValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    // Module 7 Validation Rules: allowed extensions.
    private static readonly string[] AllowedExtensions = ["pdf", "docx", "doc", "xlsx", "xls", "pptx", "png", "jpg", "jpeg", "tiff", "msg", "eml", "txt", "zip"];
    private static readonly string[] DocTypes = ["Pleading", "Order", "Agreement", "Evidence", "ID", "Invoice", "Other"];
    private static readonly string[] ConfidentialityLevels = ["Normal", "Confidential", "Privileged"];
    private const long MaxSizeBytes = 100 * 1024 * 1024;

    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(1, 300);
        RuleFor(x => x.DocType).NotEmpty().Must(t => DocTypes.Contains(t)).WithMessage($"DocType must be one of {string.Join('|', DocTypes)}.");
        RuleFor(x => x.Confidentiality).NotEmpty().Must(c => ConfidentialityLevels.Contains(c)).WithMessage($"Confidentiality must be one of {string.Join('|', ConfidentialityLevels)}.");
        RuleFor(x => x.FileName).NotEmpty()
            .Must(name => AllowedExtensions.Contains(Path.GetExtension(name).TrimStart('.').ToLowerInvariant()))
            .WithMessage($"File extension must be one of {string.Join('|', AllowedExtensions)}.");
        RuleFor(x => x.FileContent).NotEmpty().Must(c => c.LongLength <= MaxSizeBytes).WithMessage("File exceeds the 100 MB size limit.");
    }
}

public sealed class AddDocumentVersionCommandValidator : AbstractValidator<AddDocumentVersionCommand>
{
    private const long MaxSizeBytes = 100 * 1024 * 1024;

    public AddDocumentVersionCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.FileContent).NotEmpty().Must(c => c.LongLength <= MaxSizeBytes).WithMessage("File exceeds the 100 MB size limit.");
    }
}

public sealed class CreateShareLinkCommandValidator : AbstractValidator<CreateShareLinkCommand>
{
    public CreateShareLinkCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.ExpiresAt).Must(e => e <= DateTimeOffset.UtcNow.AddDays(30)).WithMessage("Share expiry cannot exceed 30 days.");
        RuleFor(x => x.MaxDownloads).GreaterThan(0).When(x => x.MaxDownloads.HasValue);
    }
}

public sealed class SendForSignatureCommandValidator : AbstractValidator<SendForSignatureCommand>
{
    private static readonly string[] Providers = ["DocuSign", "AdobeSign"];

    public SendForSignatureCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.Provider).NotEmpty().Must(p => Providers.Contains(p)).WithMessage($"Provider must be one of {string.Join('|', Providers)}.");
        RuleFor(x => x.Signers).NotEmpty();
    }
}

public sealed class CreateDocumentTemplateCommandValidator : AbstractValidator<CreateDocumentTemplateCommand>
{
    public CreateDocumentTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.DocxContent).NotEmpty();
    }
}

public sealed class GenerateFromTemplateCommandValidator : AbstractValidator<GenerateFromTemplateCommand>
{
    public GenerateFromTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.MatterId).NotEmpty();
    }
}
