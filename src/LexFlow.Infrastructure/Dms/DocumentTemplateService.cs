using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Dms;

/// <summary>
/// Module 7 template library + server-side OpenXML merge. AC-DOC4: must produce a
/// correct merged docx for every seeded field, including nested paths
/// (client.address.city). Merge fields in the docx look like {{client.name}}; this
/// walks every Text run in the document body (main + headers/footers) doing a plain
/// string replace — sufficient for fields that live inside a single run, which is how
/// Word normally saves unformatted merge-field text typed in one go.
/// </summary>
public sealed class DocumentTemplateService(LexFlowDbContext db, IBlobStorageService blobStorage) : IDocumentTemplateService
{
    private const string TemplateContainer = "templates";
    private const string DocumentContainer = "documents";

    public async Task<DocumentTemplateDto> CreateAsync(Guid tenantId, Guid? actorId, string name, string? category, byte[] docxContent, IReadOnlyList<MergeFieldInput> fields, CancellationToken cancellationToken = default)
    {
        var blobPath = $"{tenantId:N}/{Guid.NewGuid():N}.docx";
        await blobStorage.UploadAsync(TemplateContainer, blobPath, docxContent, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", cancellationToken);

        var template = new DocumentTemplate(tenantId, name, category, blobPath, "{}");
        await db.DocumentTemplates.AddAsync(template, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var field in fields)
        {
            await db.TemplateMergeFields.AddAsync(new TemplateMergeField(tenantId, template.Id, field.FieldKey, field.Label, field.Required), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(tenantId, template.Id, cancellationToken) ?? throw new InvalidOperationException("Template was just created but could not be reloaded.");
    }

    public async Task<DocumentTemplateDto?> GetByIdAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await db.DocumentTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == templateId, cancellationToken);
        return template is null ? null : await ToDtoAsync(template, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentTemplateDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var templates = await db.DocumentTemplates.Where(t => t.TenantId == tenantId).OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var result = new List<DocumentTemplateDto>();
        foreach (var template in templates)
        {
            result.Add(await ToDtoAsync(template, cancellationToken));
        }

        return result;
    }

    public async Task<DocumentDto> GenerateAsync(Guid tenantId, Guid? actorId, Guid templateId, Guid matterId, IReadOnlyDictionary<string, string>? overrides, CancellationToken cancellationToken = default)
    {
        var template = await db.DocumentTemplates.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.Id == templateId, cancellationToken)
            ?? throw new NotFoundException(nameof(DocumentTemplate), templateId);

        var fields = await db.TemplateMergeFields.Where(f => f.TenantId == tenantId && f.TemplateId == templateId).ToListAsync(cancellationToken);

        var values = await ResolveFieldValuesAsync(tenantId, matterId, fields, overrides, cancellationToken);

        var missingRequired = fields.Where(f => f.Required && !values.ContainsKey(f.FieldKey)).Select(f => f.FieldKey).ToList();
        if (missingRequired.Count > 0)
        {
            throw new Application.Common.Exceptions.ValidationException(missingRequired.Select(k => new FluentValidation.Results.ValidationFailure(k, $"Required merge field '{k}' could not be resolved and has no override.")));
        }

        var templateBytes = await blobStorage.DownloadAsync(TemplateContainer, template.DocxBlobPath, cancellationToken);
        var mergedBytes = MergeDocx(templateBytes, values);

        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken)
            ?? throw new NotFoundException(nameof(Matter), matterId);

        var document = new LexFlow.Domain.Entities.Document(tenantId, folderId: null, matterId, matter.ClientId, caseId: null, $"{template.Name} — {matter.Number}", "Agreement", "Normal");
        await db.Documents.AddAsync(document, cancellationToken);

        var fileName = $"{template.Name}.docx";
        var blobPath = $"{tenantId:N}/{document.Id:N}/v1-{fileName}";
        await blobStorage.UploadAsync(DocumentContainer, blobPath, mergedBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", cancellationToken);

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(mergedBytes)).ToLowerInvariant();
        var version = new DocumentVersion(tenantId, document.Id, 1, blobPath, mergedBytes.LongLength, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", hash, actorId);
        await db.DocumentVersions.AddAsync(version, cancellationToken);

        var outbox = new DocumentIndexOutbox(tenantId, document.Id, version.Id, "Index");
        await db.DocumentIndexOutbox.AddAsync(outbox, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new DocumentDto(document.Id, document.Title, document.DocType, document.Confidentiality, document.FolderId, document.MatterId, document.ClientId, document.CaseId, document.CurrentVersionId, document.PortalPublished, document.CreatedAt);
    }

    /// <summary>
    /// Resolves {{matter.number}}, {{client.name}}, {{client.address.city}}, {{court.name}},
    /// {{hearing.next_date}} etc. from the matter/client/case/court/hearing graph — the exact
    /// field set PRD Module 7 calls out — falling back to any caller-supplied override for
    /// fields this build can't resolve on its own.
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveFieldValuesAsync(Guid tenantId, Guid matterId, IReadOnlyList<TemplateMergeField> fields, IReadOnlyDictionary<string, string>? overrides, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(overrides ?? new Dictionary<string, string>());

        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken);
        if (matter is not null)
        {
            values.TryAdd("matter.number", matter.Number);
            values.TryAdd("matter.title", matter.Title);

            var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == matter.ClientId, cancellationToken);
            if (client is not null)
            {
                // client.DisplayName is a Postgres GENERATED ALWAYS AS STORED column — it's
                // never populated by the EF InMemory provider used in unit tests, and there's
                // no reason to depend on a DB-computed column here anyway when the same
                // logic is trivial to mirror: Individual -> "First Last", Corporate -> legal name.
                var displayName = client.Type == "Corporate"
                    ? client.LegalName
                    : string.Join(' ', new[] { client.FirstName, client.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
                values.TryAdd("client.name", displayName ?? string.Empty);

                var address = await db.ClientAddresses.FirstOrDefaultAsync(a => a.ClientId == client.Id, cancellationToken);
                if (address is not null)
                {
                    values.TryAdd("client.address.city", address.City ?? string.Empty);
                }
            }

            var courtCase = await db.CourtCases.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.MatterId == matterId, cancellationToken);
            if (courtCase is not null)
            {
                var court = await db.Courts.SingleOrDefaultAsync(c => c.Id == courtCase.CourtId, cancellationToken);
                if (court is not null)
                {
                    values.TryAdd("court.name", court.Name);
                }

                var nextHearing = await db.Hearings
                    .Where(h => h.TenantId == tenantId && h.CaseId == courtCase.Id && h.Status == "Scheduled")
                    .OrderBy(h => h.Date)
                    .FirstOrDefaultAsync(cancellationToken);
                if (nextHearing is not null)
                {
                    values.TryAdd("hearing.next_date", nextHearing.Date.ToString("yyyy-MM-dd"));
                }
            }
        }

        return values;
    }

    private static byte[] MergeDocx(byte[] templateBytes, IReadOnlyDictionary<string, string> values)
    {
        using var stream = new MemoryStream();
        stream.Write(templateBytes);
        stream.Position = 0;

        using (var wordDocument = WordprocessingDocument.Open(stream, isEditable: true))
        {
            ReplaceInPart(wordDocument.MainDocumentPart, values);

            foreach (var header in wordDocument.MainDocumentPart?.HeaderParts ?? [])
            {
                ReplaceInPart(header, values);
            }

            foreach (var footer in wordDocument.MainDocumentPart?.FooterParts ?? [])
            {
                ReplaceInPart(footer, values);
            }
        }

        return stream.ToArray();
    }

    private static void ReplaceInPart(OpenXmlPart? part, IReadOnlyDictionary<string, string> values)
    {
        if (part is null)
        {
            return;
        }

        OpenXmlElement? root = part switch
        {
            MainDocumentPart main => main.Document.Body,
            HeaderPart header => header.Header,
            FooterPart footer => footer.Footer,
            _ => null,
        };

        if (root is null)
        {
            return;
        }

        foreach (var text in root.Descendants<Text>())
        {
            var value = text.Text;
            foreach (var (key, replacement) in values)
            {
                value = value.Replace("{{" + key + "}}", replacement);
            }

            text.Text = value;
        }
    }

    private async Task<DocumentTemplateDto> ToDtoAsync(DocumentTemplate template, CancellationToken cancellationToken)
    {
        var fields = await db.TemplateMergeFields.Where(f => f.TenantId == template.TenantId && f.TemplateId == template.Id).ToListAsync(cancellationToken);
        return new DocumentTemplateDto(template.Id, template.Name, template.Category, template.Version, fields.Select(f => new MergeFieldInput(f.FieldKey, f.Label, f.Required)).ToList());
    }
}
