using Hangfire;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Crm;

/// <summary>
/// Module 2 CSV/XLSX import pipeline (AC-L5). <see cref="RunImportAsync"/> is the
/// Hangfire job body — it's enqueued against this same interface (Hangfire's ASP.NET
/// Core job activator resolves <see cref="ILeadImportService"/> from the DI container,
/// same as any other scoped dependency), so no separate job class is needed. Workers
/// (LexFlow.Workers) hosts the Hangfire server that actually executes it; the API
/// process only enqueues.
/// </summary>
public sealed class LeadImportService(LexFlowDbContext db, IBlobStorageService blobStorage, IBackgroundJobClient backgroundJobClient) : ILeadImportService
{
    private const string Container = "lead-imports";
    private const int MaxRows = 10_000;

    public async Task<LeadImportBatchDto> EnqueueImportAsync(Guid tenantId, Guid? actorId, string fileName, byte[] fileContent, CancellationToken cancellationToken = default)
    {
        var batch = new LeadImportBatch(tenantId, fileName, string.Empty);
        await db.LeadImportBatches.AddAsync(batch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var blobPath = $"{tenantId:N}/{batch.Id:N}/{fileName}";
        await blobStorage.UploadAsync(Container, blobPath, fileContent, "application/octet-stream", cancellationToken);

        batch.SetSourceBlobPath(blobPath);
        await db.SaveChangesAsync(cancellationToken);

        backgroundJobClient.Enqueue<ILeadImportService>(s => s.RunImportAsync(tenantId, batch.Id, CancellationToken.None));

        return ToDto(batch);
    }

    public async Task<LeadImportBatchDto?> GetBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await db.LeadImportBatches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == batchId, cancellationToken);
        return batch is null ? null : ToDto(batch);
    }

    public async Task RunImportAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await db.LeadImportBatches.SingleOrDefaultAsync(b => b.TenantId == tenantId && b.Id == batchId, cancellationToken)
            ?? throw new NotFoundException(nameof(LeadImportBatch), batchId);

        batch.Start();
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var fileContent = await blobStorage.DownloadAsync(Container, batch.SourceBlobPath, cancellationToken);
            var rows = LeadImportParser.Parse(batch.FileName, fileContent);

            if (rows.Count > MaxRows)
            {
                batch.Fail($"Import exceeds the {MaxRows}-row limit per batch ({rows.Count} rows).");
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var errors = new List<(int RowNumber, string RawRow, string Error)>();
            var successCount = 0;

            foreach (var row in rows)
            {
                var error = Validate(row);
                if (error is not null)
                {
                    errors.Add((row.RowNumber, row.RawLine, error));
                    continue;
                }

                var duplicatePhone = !string.IsNullOrWhiteSpace(row.Phone)
                    && await db.Leads.AnyAsync(l => l.TenantId == tenantId && l.PhoneE164 == row.Phone && l.Status == "Open", cancellationToken);
                if (duplicatePhone)
                {
                    errors.Add((row.RowNumber, row.RawLine, "An open lead with this phone number already exists."));
                    continue;
                }

                var count = await db.Leads.IgnoreQueryFilters().CountAsync(l => l.TenantId == tenantId, cancellationToken);
                var lead = new Lead(
                    tenantId,
                    $"LD-{DateTimeOffset.UtcNow.Year}-{count + 1:D6}",
                    row.FirstName!,
                    row.LastName,
                    row.Company,
                    row.Email,
                    row.Phone,
                    sourceId: null,
                    ownerId: null,
                    branchId: null,
                    practiceAreaId: null,
                    row.IssueSummary,
                    opposingParty: null,
                    budgetBand: null,
                    DateTimeOffset.UtcNow.AddHours(4));

                await db.Leads.AddAsync(lead, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                successCount++;
            }

            string? errorBlobPath = null;
            if (errors.Count > 0)
            {
                var errorCsv = LeadCsvXlsx.ToErrorCsv(errors);
                errorBlobPath = $"{tenantId:N}/{batch.Id:N}/errors.csv";
                await blobStorage.UploadAsync(Container, errorBlobPath, errorCsv, "text/csv", cancellationToken);
            }

            batch.Complete(rows.Count, successCount, errors.Count, errorBlobPath);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            batch.Fail(ex.Message);
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static string? Validate(ImportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.FirstName))
        {
            return "FirstName is required.";
        }

        if (string.IsNullOrWhiteSpace(row.Email) && string.IsNullOrWhiteSpace(row.Phone))
        {
            return "At least one of Email/Phone is required.";
        }

        return null;
    }

    private static LeadImportBatchDto ToDto(LeadImportBatch b) => new(
        b.Id, b.FileName, b.Status, b.TotalRows, b.SuccessCount, b.ErrorCount, b.ErrorFileBlobPath, b.StartedAt, b.CompletedAt, b.FailureReason);
}
