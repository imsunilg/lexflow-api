using FluentValidation;
using LexFlow.Application.Commands.Clients;

namespace LexFlow.Application.Validators.Clients;

public sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    private static readonly string[] PanCharacterClasses = ["Individual", "Corporate"];

    public CreateClientCommandValidator()
    {
        RuleFor(x => x.Type).NotEmpty().Must(t => PanCharacterClasses.Contains(t)).WithMessage("Type must be Individual or Corporate.");

        RuleFor(x => x.FirstName).NotEmpty().When(x => x.Type == "Individual").WithMessage("Individual clients require a first name.");
        RuleFor(x => x.LegalName).NotEmpty().When(x => x.Type == "Corporate").WithMessage("Corporate clients require a legal name.");
        RuleFor(x => x.Contacts).Must(c => c is { Count: > 0 }).When(x => x.Type == "Corporate")
            .WithMessage("Corporate clients require at least one contact person.");

        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneE164).Matches(@"^\+[1-9]\d{6,14}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneE164));
        RuleFor(x => x.Gstin).Matches(@"^\d{2}[A-Z]{5}\d{4}[A-Z]\d[A-Z\d]Z[A-Z\d]$").When(x => !string.IsNullOrWhiteSpace(x.Gstin));
    }
}

public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneE164).Matches(@"^\+[1-9]\d{6,14}$").When(x => !string.IsNullOrWhiteSpace(x.PhoneE164));
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit.HasValue);
    }
}

public sealed class AddClientContactCommandValidator : AbstractValidator<AddClientContactCommand>
{
    public AddClientContactCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class AddClientAddressCommandValidator : AbstractValidator<AddClientAddressCommand>
{
    private static readonly string[] Kinds = ["Home", "Office", "Registered", "Billing", "Communication"];

    public AddClientAddressCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().Must(k => Kinds.Contains(k)).WithMessage($"Kind must be one of {string.Join('|', Kinds)}.");
        RuleFor(x => x.Line1).NotEmpty();
    }
}

public sealed class AddClientIdentityDocumentCommandValidator : AbstractValidator<AddClientIdentityDocumentCommand>
{
    public AddClientIdentityDocumentCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.DocKind).NotEmpty();
        RuleFor(x => x.DocNumber).NotEmpty();
    }
}

public sealed class AddClientRelationshipCommandValidator : AbstractValidator<AddClientRelationshipCommand>
{
    private static readonly string[] RelationTypes = ["Spouse", "Child", "Parent", "ParentCompany", "Subsidiary", "Referrer", "CoParty"];

    public AddClientRelationshipCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.RelationType).NotEmpty().Must(t => RelationTypes.Contains(t)).WithMessage($"RelationType must be one of {string.Join('|', RelationTypes)}.");
        RuleFor(x => x).Must(x => x.RelatedClientId.HasValue || !string.IsNullOrWhiteSpace(x.PersonName))
            .WithMessage("Either relatedClientId or personName is required.")
            .WithName("RelatedClientId");
    }
}

public sealed class MergeClientsCommandValidator : AbstractValidator<MergeClientsCommand>
{
    public MergeClientsCommandValidator()
    {
        RuleFor(x => x.SurvivorId).NotEmpty();
        RuleFor(x => x.DuplicateId).NotEmpty();
        RuleFor(x => x).Must(x => x.SurvivorId != x.DuplicateId).WithMessage("SurvivorId and DuplicateId must differ.").WithName("SurvivorId");
    }
}
