using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Clients;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Clients;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 3 Client Management (PRD §17).</summary>
[ApiController]
[Route("api/v1/clients")]
public sealed class ClientsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateClientCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpGet]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetAll([FromQuery] string? type, [FromQuery] string? status, [FromQuery] Guid? owner, [FromQuery] Guid? branch, [FromQuery] string? q, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ClientDto>>.Of(await mediator.Send(new GetClientsQuery(type, status, owner, branch, q), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientDto>.Of(await mediator.Send(new GetClientQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateClientCommand(id, request.FirstName, request.LastName, request.LegalName, request.Email, request.PhoneE164, request.Gstin, request.Cin, request.CreditLimit, request.OwnerId, request.BranchId);
        return Ok(ApiResponse<ClientDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    /// <summary>AC-C4: blocked with 409 while open matters/unpaid invoices reference this client.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteClientCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/contacts")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> AddContact(Guid id, [FromBody] ClientContactInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientContactDto>.Of(await mediator.Send(new AddClientContactCommand(id, input.Name, input.Designation, input.Email, input.Phone, input.IsPrimary), cancellationToken)));

    [HttpGet("{id:guid}/contacts")]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetContacts(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ClientContactDto>>.Of(await mediator.Send(new GetClientContactsQuery(id), cancellationToken)));

    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> UpdateContact(Guid id, Guid contactId, [FromBody] ClientContactInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientContactDto>.Of(await mediator.Send(new UpdateClientContactCommand(id, contactId, input.Name, input.Designation, input.Email, input.Phone, input.IsPrimary), cancellationToken)));

    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> DeleteContact(Guid id, Guid contactId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteClientContactCommand(id, contactId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/addresses")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> AddAddress(Guid id, [FromBody] ClientAddressInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientAddressDto>.Of(await mediator.Send(new AddClientAddressCommand(id, input.Kind, input.Line1, input.Line2, input.City, input.StateCode, input.Postal, input.Country, input.IsPrimaryOfKind), cancellationToken)));

    [HttpGet("{id:guid}/addresses")]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetAddresses(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ClientAddressDto>>.Of(await mediator.Send(new GetClientAddressesQuery(id), cancellationToken)));

    [HttpPut("{id:guid}/addresses/{addressId:guid}")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> UpdateAddress(Guid id, Guid addressId, [FromBody] ClientAddressInput input, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientAddressDto>.Of(await mediator.Send(new UpdateClientAddressCommand(id, addressId, input.Kind, input.Line1, input.Line2, input.City, input.StateCode, input.Postal, input.Country, input.IsPrimaryOfKind), cancellationToken)));

    [HttpDelete("{id:guid}/addresses/{addressId:guid}")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> DeleteAddress(Guid id, Guid addressId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteClientAddressCommand(id, addressId), cancellationToken);
        return NoContent();
    }

    /// <summary>KYC upload (multipart) — the numeric field arrives as a form field, not JSON, since it may be paired with a scanned file upload in the future DMS integration.</summary>
    [HttpPost("{id:guid}/identity-documents")]
    [RequirePermission("clients.kyc.read")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> AddIdentityDocument(Guid id, [FromForm] AddIdentityDocumentForm form, CancellationToken cancellationToken)
    {
        var command = new AddClientIdentityDocumentCommand(id, form.DocKind, form.DocNumber, form.ExpiryDate, DocumentId: null);
        return Ok(ApiResponse<ClientIdentityDocumentDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpGet("{id:guid}/identity-documents")]
    [RequirePermission("clients.kyc.read")]
    public async Task<IActionResult> GetIdentityDocuments(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ClientIdentityDocumentDto>>.Of(await mediator.Send(new GetClientIdentityDocumentsQuery(id), cancellationToken)));

    [HttpPatch("{id:guid}/identity-documents/{docId:guid}/verify")]
    [RequirePermission("clients.kyc.read")]
    public async Task<IActionResult> VerifyIdentityDocument(Guid id, Guid docId, [FromBody] VerifyIdentityDocumentRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientIdentityDocumentDto>.Of(await mediator.Send(new VerifyClientIdentityDocumentCommand(id, docId, request.Approve), cancellationToken)));

    [HttpPost("{id:guid}/relationships")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> AddRelationship(Guid id, [FromBody] AddRelationshipRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientRelationshipDto>.Of(await mediator.Send(new AddClientRelationshipCommand(id, request.RelatedClientId, request.PersonName, request.RelationType), cancellationToken)));

    [HttpGet("{id:guid}/relationships")]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetRelationships(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ClientRelationshipDto>>.Of(await mediator.Send(new GetClientRelationshipsQuery(id), cancellationToken)));

    /// <summary>AC-C1: all 360° tab counts in one summary call.</summary>
    [HttpGet("{id:guid}/summary")]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientSummaryDto>.Of(await mediator.Send(new GetClientSummaryQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/portal-access")]
    [RequirePermission("clients.portal.manage")]
    public async Task<IActionResult> SetPortalAccess(Guid id, [FromBody] SetPortalAccessRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ClientPortalUserDto>.Of(await mediator.Send(new SetClientPortalAccessCommand(id, request.Enable), cancellationToken)));

    [HttpPost("{id:guid}/portal-access/resend-invite")]
    [RequirePermission("clients.portal.manage")]
    public async Task<IActionResult> ResendPortalInvite(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ResendClientPortalInviteCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>AC-C3: full re-parenting merge.</summary>
    [HttpPost("merge")]
    [RequirePermission("clients.manage.all")]
    public async Task<IActionResult> Merge([FromBody] MergeClientsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new MergeClientsCommand(request.SurvivorId, request.DuplicateId, request.FieldChoices ?? new Dictionary<string, string>()), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/communications")]
    [RequirePermission("clients.read.all")]
    public async Task<IActionResult> GetCommunications(Guid id, [FromQuery] string? channel, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ClientCommunicationDto>>.Of(await mediator.Send(new GetClientCommunicationsQuery(id, channel, from, to), cancellationToken)));
}

public sealed record UpdateClientRequest(string? FirstName, string? LastName, string? LegalName, string? Email, string? PhoneE164, string? Gstin, string? Cin, decimal? CreditLimit, Guid? OwnerId, Guid? BranchId);

public sealed record AddIdentityDocumentForm(string DocKind, string DocNumber, DateOnly? ExpiryDate);

public sealed record VerifyIdentityDocumentRequest(bool Approve);

public sealed record AddRelationshipRequest(Guid? RelatedClientId, string? PersonName, string RelationType);

public sealed record SetPortalAccessRequest(bool Enable);

public sealed record MergeClientsRequest(Guid SurvivorId, Guid DuplicateId, Dictionary<string, string>? FieldChoices);
