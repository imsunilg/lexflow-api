using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Dms;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.UnitTests.Dms;

/// <summary>AC-DOC4: template generation must produce a correct merged docx for every seeded field, including nested paths (client.address.city).</summary>
public sealed class DocumentTemplateServiceTests
{
    private static LexFlowDbContext CreateContext(string dbName) => new(
        new DbContextOptionsBuilder<LexFlowDbContext>().UseInMemoryDatabase(dbName).Options);

    private static byte[] BuildTemplateDocx(params string[] paragraphTexts)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());
            foreach (var text in paragraphTexts)
            {
                body.AppendChild(new Paragraph(new Run(new Text(text))));
            }
        }

        return stream.ToArray();
    }

    private static string ExtractText(byte[] docxBytes)
    {
        using var stream = new MemoryStream(docxBytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        return string.Join("\n", doc.MainDocumentPart!.Document.Body!.Descendants<Text>().Select(t => t.Text));
    }

    [Fact]
    public async Task GenerateAsync_merges_matter_and_nested_client_address_fields()
    {
        await using var db = CreateContext(nameof(GenerateAsync_merges_matter_and_nested_client_address_fields));
        var blobStore = new FakeBlobStorageService();
        var service = new DocumentTemplateService(db, blobStore);
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        await db.SaveChangesAsync();

        await db.ClientAddresses.AddAsync(new ClientAddress(tenantId, client.Id, "Home", "221B Baker St", null, "Mumbai", null, null, null, true));
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var template = await service.CreateAsync(
            tenantId, null, "Engagement Letter", "Litigation",
            BuildTemplateDocx("Dear {{client.name}},", "Re: Matter {{matter.number}}, {{client.address.city}}."),
            [new MergeFieldInput("client.name", "Client Name", true), new MergeFieldInput("matter.number", "Matter Number", true), new MergeFieldInput("client.address.city", "City", false)],
            CancellationToken.None);

        var generated = await service.GenerateAsync(tenantId, null, template.Id, matter.Id, overrides: null, CancellationToken.None);

        var generatedBytes = await blobStore.LastUploadedDocumentBytesAsync();
        var text = ExtractText(generatedBytes);

        text.Should().Contain("Dear Priya Shah,");
        text.Should().Contain($"Re: Matter {matter.Number}, Mumbai.");
        generated.Title.Should().Contain(matter.Number);
    }

    [Fact]
    public async Task GenerateAsync_throws_with_the_full_missing_field_list_when_a_required_field_cannot_be_resolved()
    {
        await using var db = CreateContext(nameof(GenerateAsync_throws_with_the_full_missing_field_list_when_a_required_field_cannot_be_resolved));
        var service = new DocumentTemplateService(db, new FakeBlobStorageService());
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var template = await service.CreateAsync(
            tenantId, null, "Needs Custom Field", null,
            BuildTemplateDocx("{{custom.unresolvable}}"),
            [new MergeFieldInput("custom.unresolvable", "Custom", true)],
            CancellationToken.None);

        var act = () => service.GenerateAsync(tenantId, null, template.Id, matter.Id, overrides: null, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<Application.Common.Exceptions.ValidationException>();
        exception.Which.Errors.Should().ContainKey("custom.unresolvable");
    }

    [Fact]
    public async Task GenerateAsync_accepts_an_override_for_a_field_this_build_cannot_resolve_on_its_own()
    {
        await using var db = CreateContext(nameof(GenerateAsync_accepts_an_override_for_a_field_this_build_cannot_resolve_on_its_own));
        var blobStore = new FakeBlobStorageService();
        var service = new DocumentTemplateService(db, blobStore);
        var tenantId = Guid.NewGuid();

        var client = new Client(tenantId, "CL-1", "Individual", "Priya", "Shah", null, null, null, null, null, null, null, null);
        await db.Clients.AddAsync(client);
        var matter = new Matter(tenantId, "MAT-1", "Test matter", client.Id, "Litigation", null, null, null, "Medium", null, DateOnly.FromDateTime(DateTime.UtcNow), false, null, "{}");
        await db.Matters.AddAsync(matter);
        await db.SaveChangesAsync();

        var template = await service.CreateAsync(
            tenantId, null, "Needs Custom Field", null,
            BuildTemplateDocx("Signed on {{custom.signing_date}}."),
            [new MergeFieldInput("custom.signing_date", "Signing Date", true)],
            CancellationToken.None);

        await service.GenerateAsync(tenantId, null, template.Id, matter.Id, new Dictionary<string, string> { ["custom.signing_date"] = "9 July 2026" }, CancellationToken.None);

        var text = ExtractText(await blobStore.LastUploadedDocumentBytesAsync());
        text.Should().Contain("Signed on 9 July 2026.");
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly Dictionary<string, byte[]> _blobs = new();
        private string? _lastDocumentContainerBlobPath;

        public Task<string> UploadAsync(string container, string blobPath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            _blobs[$"{container}/{blobPath}"] = content;
            if (container == "documents")
            {
                _lastDocumentContainerBlobPath = $"{container}/{blobPath}";
            }

            return Task.FromResult(blobPath);
        }

        public Task<byte[]> DownloadAsync(string container, string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult(_blobs[$"{container}/{blobPath}"]);

        public Task<string> GetDownloadUrlAsync(string container, string blobPath, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake-blob.test/{container}/{blobPath}");

        public Task<byte[]> LastUploadedDocumentBytesAsync() => Task.FromResult(_blobs[_lastDocumentContainerBlobPath!]);
    }
}
