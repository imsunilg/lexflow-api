using System.Text.Json.Nodes;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// Module 8: invoice lifecycle, the batch-billing engine (WIP pull + milestone/retainer line
/// generation), and BR-9 GST tax calculation. BR-4 invoice immutability is enforced first at the
/// DB (fin.invoices/invoice_lines/invoice_taxes 004_Triggers.sql) and again defensively in
/// Invoice's own mutators; any DbUpdateException carrying the trigger's BR-4 text is translated to
/// DomainRuleException("INVOICE_NOT_DRAFT", ...) — defense in depth, per this module's build brief.
/// </summary>
public sealed class BillingService(LexFlowDbContext db, IInvoicePdfRenderer pdfRenderer, IBlobStorageService blobStorage) : IBillingService
{
    private const string InvoicesContainer = "invoices";

    public async Task<InvoiceDto> CreateDraftAsync(Guid tenantId, Guid matterId, CreateInvoiceInput input, CancellationToken cancellationToken = default)
    {
        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken)
            ?? throw new NotFoundException(nameof(Matter), matterId);

        var invoice = new Invoice(tenantId, matterId, matter.ClientId, input.IssueDate, (input.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(input.DueInDays), "INR", input.Notes);

        var (lines, subTotal) = await BuildLinesAsync(tenantId, invoice.Id, input.PullTimeEntryIds, input.ExtraLines, cancellationToken);
        await FinalizeDraftTotalsAsync(tenantId, invoice, matter, lines, subTotal, input.Discount, cancellationToken);

        await db.Invoices.AddAsync(invoice, cancellationToken);
        RecordStatusHistory(tenantId, invoice.Id, null, "Draft", null, null);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(tenantId, invoice.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<InvoiceDto>> CreateBatchAsync(Guid tenantId, BatchBillingFilter filter, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var arrangements = await db.BillingArrangements.Where(b => b.TenantId == tenantId && b.IsActive).ToListAsync(cancellationToken);
        var results = new List<InvoiceDto>();

        foreach (var arrangement in arrangements)
        {
            var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == arrangement.MatterId, cancellationToken);
            if (matter is null || matter.Status != "Open")
            {
                continue;
            }

            if (filter.BranchId.HasValue && matter.BranchId != filter.BranchId)
            {
                continue;
            }

            if (filter.MatterTypeId.HasValue)
            {
                continue; // fin.matters has no direct matter_type_id link surfaced here — practice-area-based batching is out of scope for this pass.
            }

            var wipEntries = await db.TimeEntries
                .Where(e => e.TenantId == tenantId && e.MatterId == matter.Id && e.Status == "Approved" && e.Billable && e.InvoiceLineId == null)
                .ToListAsync(cancellationToken);

            var wipTotal = wipEntries.Sum(e => e.AmountSnapshot ?? 0);
            var extraLines = new List<ExtraLineInput>();

            if (arrangement.ArrangementType == "Retainer" && arrangement.AutoInvoiceDay == asOf.Day && arrangement.RetainerAmount is { } retainer)
            {
                extraLines.Add(new ExtraLineInput("Retainer", $"Retainer for {asOf:yyyy-MM}", 1, null, retainer));
            }

            if (arrangement.ArrangementType == "Fixed" && arrangement.MilestonesJson is { } milestonesJson)
            {
                foreach (var milestone in ParseDueMilestones(milestonesJson, asOf))
                {
                    extraLines.Add(new ExtraLineInput("Fixed", milestone.Description, 1, null, milestone.Amount));
                }
            }

            var extraTotal = extraLines.Sum(l => l.Qty * l.Rate);
            if (wipTotal + extraTotal < filter.MinWip)
            {
                continue;
            }

            if (wipEntries.Count == 0 && extraLines.Count == 0)
            {
                continue;
            }

            var invoice = new Invoice(tenantId, matter.Id, matter.ClientId, asOf, asOf.AddDays(15), "INR", null);
            var (lines, subTotal) = await BuildLinesAsync(tenantId, invoice.Id, wipEntries.Select(e => e.Id).ToList(), extraLines, cancellationToken);
            await FinalizeDraftTotalsAsync(tenantId, invoice, matter, lines, subTotal, null, cancellationToken);

            await db.Invoices.AddAsync(invoice, cancellationToken);
            RecordStatusHistory(tenantId, invoice.Id, null, "Draft", "Batch billing run", null);

            results.Add((await BuildDtoAsync(invoice, lines, [], cancellationToken)));
        }

        await db.SaveChangesAsync(cancellationToken);
        return results;
    }

    public async Task<InvoiceDto?> GetAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var lines = await db.InvoiceLines.Where(l => l.TenantId == tenantId && l.InvoiceId == invoiceId).OrderBy(l => l.LineNo).ToListAsync(cancellationToken);
        var taxes = await db.InvoiceTaxes.Where(t => t.TenantId == tenantId && t.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        return await BuildDtoAsync(invoice, lines, taxes, cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(Guid tenantId, InvoiceFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.Invoices.Where(i => i.TenantId == tenantId).AsQueryable();

        if (filter.Status is not null)
        {
            query = query.Where(i => i.Status == filter.Status);
        }

        if (filter.ClientId.HasValue)
        {
            query = query.Where(i => i.ClientId == filter.ClientId);
        }

        if (filter.MatterId.HasValue)
        {
            query = query.Where(i => i.MatterId == filter.MatterId);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(i => i.IssueDate >= filter.From);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(i => i.IssueDate <= filter.To);
        }

        if (filter.OverdueOnly == true)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(i => i.Status == "Overdue" || ((i.Status == "Sent" || i.Status == "PartiallyPaid") && i.DueDate < today));
        }

        var invoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        var results = new List<InvoiceDto>();
        foreach (var invoice in invoices)
        {
            var lines = await db.InvoiceLines.Where(l => l.InvoiceId == invoice.Id).OrderBy(l => l.LineNo).ToListAsync(cancellationToken);
            var taxes = await db.InvoiceTaxes.Where(t => t.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
            results.Add(await BuildDtoAsync(invoice, lines, taxes, cancellationToken));
        }

        return results;
    }

    public async Task<InvoiceDto> UpdateDraftAsync(Guid tenantId, Guid invoiceId, UpdateInvoiceInput input, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);

        try
        {
            invoice.UpdateDraft(input.IssueDate, input.DueDate, input.Notes);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (FinDbTriggerExceptions.IsInvoiceNotDraftError(ex))
        {
            throw new DomainRuleException("INVOICE_NOT_DRAFT", "This invoice is no longer Draft — lines/totals are immutable once sent; correct via credit note (BR-4).");
        }

        return (await GetAsync(tenantId, invoiceId, cancellationToken))!;
    }

    public async Task<InvoiceDto> SubmitAsync(Guid tenantId, Guid actorId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);
        invoice.Submit();
        RecordStatusHistory(tenantId, invoiceId, "Draft", "Submitted", null, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(tenantId, invoiceId, cancellationToken))!;
    }

    public async Task<InvoiceDto> ApproveAsync(Guid tenantId, Guid actorId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);
        invoice.Approve();
        RecordStatusHistory(tenantId, invoiceId, "Submitted", "Approved", null, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(tenantId, invoiceId, cancellationToken))!;
    }

    public async Task<InvoiceDto> RejectAsync(Guid tenantId, Guid actorId, Guid invoiceId, string reason, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);
        invoice.Reject();
        RecordStatusHistory(tenantId, invoiceId, "Submitted", "Rejected", reason, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(tenantId, invoiceId, cancellationToken))!;
    }

    public async Task<InvoiceDto> SendAsync(Guid tenantId, Guid actorId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);
        if (invoice.Status is not ("Approved" or "Draft"))
        {
            throw new ConflictException($"Invoice {invoiceId} cannot be sent from status {invoice.Status}.", "INVOICE_NOT_SENDABLE");
        }

        var matter = await db.Matters.SingleAsync(m => m.Id == invoice.MatterId, cancellationToken);
        var lines = await db.InvoiceLines.Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.LineNo).ToListAsync(cancellationToken);
        var taxable = invoice.SubTotal - invoice.DiscountTotal;

        // Validation Rules: "tax config mandatory before first send (else 422 TAX_NOT_CONFIGURED)".
        // Recomputed here (not just re-checked) in case tax config was added/changed after the
        // draft was created.
        var existingTaxes = await db.InvoiceTaxes.Where(t => t.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        if (taxable > 0)
        {
            var gstLines = await ComputeGstAsync(tenantId, matter, taxable, cancellationToken);
            if (gstLines.Count == 0)
            {
                throw new DomainRuleException("TAX_NOT_CONFIGURED", $"No active tax configuration found for matter {matter.Id}'s billing country — configure Settings > Taxes before sending (Module 8 Validation Rules).");
            }

            db.InvoiceTaxes.RemoveRange(existingTaxes);
            var taxTotal = gstLines.Sum(g => g.Amount);
            foreach (var g in gstLines)
            {
                await db.InvoiceTaxes.AddAsync(new InvoiceTax(tenantId, invoiceId, g.Name, g.RatePct, g.TaxableAmount, g.Amount), cancellationToken);
            }

            invoice.SetTotals(invoice.SubTotal, invoice.DiscountTotal, taxTotal, taxable + taxTotal);
        }

        if (string.IsNullOrEmpty(invoice.Number))
        {
            var (seriesId, number) = await AssignNumberAsync(tenantId, matter.BranchId, cancellationToken);
            invoice.AssignNumber(seriesId, number);
        }

        var client = await db.Clients.SingleAsync(c => c.Id == invoice.ClientId, cancellationToken);
        var finalTaxes = taxable > 0 ? await db.InvoiceTaxes.Where(t => t.InvoiceId == invoiceId).ToListAsync(cancellationToken) : existingTaxes;
        var pdfBytes = await RenderPdfBytesAsync(tenantId, matter, client, invoice, lines, finalTaxes, cancellationToken);

        // Error Handling: "PDF render failure -> send blocked with error (never send without
        // attachment)" — nothing above this point has been persisted yet (single SaveChangesAsync
        // at the end of this method), so a render/upload failure here leaves the invoice untouched.
        var blobPath = $"{tenantId}/{invoiceId}.pdf";
        await blobStorage.UploadAsync(InvoicesContainer, blobPath, pdfBytes, "application/pdf", cancellationToken);
        invoice.SetPdf(blobPath);
        invoice.MarkSent();

        foreach (var line in lines)
        {
            foreach (var timeEntryId in line.TimeEntryIds)
            {
                var entry = await db.TimeEntries.SingleOrDefaultAsync(e => e.Id == timeEntryId, cancellationToken);
                entry?.MarkBilled(line.Id);
            }
        }

        RecordStatusHistory(tenantId, invoiceId, invoice.Status == "Sent" ? "Approved" : invoice.Status, "Sent", null, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(tenantId, invoiceId, cancellationToken))!;
    }

    public async Task<InvoiceDto> VoidAsync(Guid tenantId, Guid actorId, Guid invoiceId, string reason, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);
        var fromStatus = invoice.Status;
        invoice.Void(reason);
        RecordStatusHistory(tenantId, invoiceId, fromStatus, "Void", reason, actorId);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(tenantId, invoiceId, cancellationToken))!;
    }

    public async Task<byte[]> RenderPdfAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceOrThrowAsync(tenantId, invoiceId, cancellationToken);
        if (!string.IsNullOrEmpty(invoice.PdfBlobPath))
        {
            return await blobStorage.DownloadAsync(InvoicesContainer, invoice.PdfBlobPath, cancellationToken);
        }

        var matter = await db.Matters.SingleAsync(m => m.Id == invoice.MatterId, cancellationToken);
        var client = await db.Clients.SingleAsync(c => c.Id == invoice.ClientId, cancellationToken);
        var lines = await db.InvoiceLines.Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.LineNo).ToListAsync(cancellationToken);
        var taxes = await db.InvoiceTaxes.Where(t => t.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        return await RenderPdfBytesAsync(tenantId, matter, client, invoice, lines, taxes, cancellationToken);
    }

    public async Task<AgingReportDto> GetAgingAsync(Guid tenantId, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var invoices = await db.Invoices
            .Where(i => i.TenantId == tenantId && (i.Status == "Sent" || i.Status == "PartiallyPaid" || i.Status == "Overdue") && i.DueDate != null)
            .ToListAsync(cancellationToken);

        decimal current = 0, b1 = 0, b31 = 0, b61 = 0, over90 = 0;
        foreach (var invoice in invoices)
        {
            var outstanding = invoice.GrandTotal - invoice.AmountPaid;
            if (outstanding <= 0)
            {
                continue;
            }

            var daysOverdue = asOf.DayNumber - invoice.DueDate!.Value.DayNumber;
            if (daysOverdue <= 0)
            {
                current += outstanding;
            }
            else if (daysOverdue <= 30)
            {
                b1 += outstanding;
            }
            else if (daysOverdue <= 60)
            {
                b31 += outstanding;
            }
            else if (daysOverdue <= 90)
            {
                b61 += outstanding;
            }
            else
            {
                over90 += outstanding;
            }
        }

        // AC-B6: buckets sum to total AR exactly — computed as the sum, never independently re-derived.
        return new AgingReportDto(current, b1, b31, b61, over90, current + b1 + b31 + b61 + over90);
    }

    public async Task<IReadOnlyList<InvoiceStatusHistoryDto>> GetStatusHistoryAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var rows = await db.InvoiceStatusHistory.Where(h => h.TenantId == tenantId && h.InvoiceId == invoiceId).OrderBy(h => h.ChangedAt).ToListAsync(cancellationToken);
        return rows.Select(h => new InvoiceStatusHistoryDto(h.FromStatus, h.ToStatus, h.Reason, h.ChangedBy, h.ChangedAt)).ToList();
    }

    private async Task<(List<InvoiceLine> Lines, decimal SubTotal)> BuildLinesAsync(Guid tenantId, Guid invoiceId, IReadOnlyList<Guid>? pullTimeEntryIds, IReadOnlyList<ExtraLineInput>? extraLines, CancellationToken cancellationToken)
    {
        var lineNo = 1;
        var lines = new List<InvoiceLine>();
        decimal subTotal = 0;

        if (pullTimeEntryIds is { Count: > 0 })
        {
            var entries = await db.TimeEntries.Where(e => e.TenantId == tenantId && pullTimeEntryIds.Contains(e.Id)).ToListAsync(cancellationToken);
            foreach (var entry in entries)
            {
                if (entry.Status != "Approved" || entry.InvoiceLineId is not null)
                {
                    throw new ConflictException($"Time entry {entry.Id} is not approved-and-unbilled WIP.", "TIME_ENTRY_NOT_BILLABLE_WIP");
                }

                var qty = GstCalculator.Round(entry.RoundedMin / 60m);
                var amount = entry.AmountSnapshot ?? 0;
                var line = new InvoiceLine(tenantId, invoiceId, lineNo++, "Time", entry.Narrative, qty, "hrs", entry.RateSnapshot ?? 0, amount, [entry.Id], null);
                lines.Add(line);
                subTotal += amount;
                entry.LinkToDraftInvoiceLine(line.Id);
            }
        }

        if (extraLines is { Count: > 0 })
        {
            foreach (var extra in extraLines)
            {
                var amount = GstCalculator.Round(extra.Qty * extra.Rate);
                var line = new InvoiceLine(tenantId, invoiceId, lineNo++, extra.Type, extra.Description, extra.Qty, extra.Unit, extra.Rate, amount, null, null);
                lines.Add(line);
                subTotal += amount;
            }
        }

        return (lines, subTotal);
    }

    private async Task FinalizeDraftTotalsAsync(Guid tenantId, Invoice invoice, Matter matter, List<InvoiceLine> lines, decimal subTotal, DiscountInput? discount, CancellationToken cancellationToken)
    {
        var discountTotal = discount switch
        {
            { Type: "Flat" } => GstCalculator.Round(discount.Value),
            { Type: "Percent" } => GstCalculator.Round(subTotal * discount.Value / 100m),
            _ => 0m,
        };

        var taxable = subTotal - discountTotal;
        var gstLines = await ComputeGstAsync(tenantId, matter, taxable, cancellationToken);
        var taxTotal = gstLines.Sum(g => g.Amount);

        invoice.SetTotals(subTotal, discountTotal, taxTotal, taxable + taxTotal);

        await db.InvoiceLines.AddRangeAsync(lines, cancellationToken);
        foreach (var g in gstLines)
        {
            await db.InvoiceTaxes.AddAsync(new InvoiceTax(tenantId, invoice.Id, g.Name, g.RatePct, g.TaxableAmount, g.Amount), cancellationToken);
        }
    }

    /// <summary>BR-9: intra-state CGST+SGST vs inter-state IGST, resolved via GSTIN state-code prefixes (documented simplification for clients/branches without a GSTIN — see GstCalculator).</summary>
    private async Task<IReadOnlyList<GstLine>> ComputeGstAsync(Guid tenantId, Matter matter, decimal taxableAmount, CancellationToken cancellationToken)
    {
        if (taxableAmount <= 0)
        {
            return [];
        }

        var taxConfig = await db.TaxConfigs
            .Where(t => t.TenantId == tenantId && t.IsActive && t.CountryCode == "IN" && (t.BranchId == matter.BranchId || t.BranchId == null))
            .OrderByDescending(t => t.BranchId != null)
            .FirstOrDefaultAsync(cancellationToken);

        if (taxConfig is null || taxConfig.TaxType == "None")
        {
            return [];
        }

        var components = JsonNode.Parse(taxConfig.ComponentsJson) as JsonObject;

        if (taxConfig.TaxType != "GST")
        {
            var flatPct = GetDecimal(components, "ratePct", 0);
            return flatPct <= 0 ? [] : [new GstLine(taxConfig.TaxType, flatPct, taxableAmount, GstCalculator.Round(taxableAmount * flatPct / 100m))];
        }

        var client = await db.Clients.SingleOrDefaultAsync(c => c.Id == matter.ClientId, cancellationToken);
        var clientStateCode = GstCalculator.ExtractStateCode(client?.Gstin)
            ?? (await db.ClientAddresses.Where(a => a.ClientId == matter.ClientId && a.Kind == "Billing" && a.IsPrimaryOfKind).Select(a => a.StateCode).FirstOrDefaultAsync(cancellationToken));

        var branch = matter.BranchId.HasValue ? await db.Branches.SingleOrDefaultAsync(b => b.Id == matter.BranchId, cancellationToken) : null;
        var firmStateCode = GstCalculator.ExtractStateCode(branch?.Gstin);

        var cgstPct = GetDecimal(components, "cgstPct", 9);
        var sgstPct = GetDecimal(components, "sgstPct", 9);
        var igstPct = GetDecimal(components, "igstPct", 18);
        var isZeroRated = GetBool(components, "zeroRated", false);

        return GstCalculator.Calculate(taxableAmount, clientStateCode, firmStateCode, cgstPct, sgstPct, igstPct, isZeroRated);
    }

    private async Task<(Guid SeriesId, string Number)> AssignNumberAsync(Guid tenantId, Guid? branchId, CancellationToken cancellationToken)
    {
        var fiscalYear = ResolveFiscalYear(DateTime.UtcNow);
        var series = await db.NumberSeries.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.SeriesKey == "INV" && s.BranchId == branchId && s.FiscalYear == fiscalYear, cancellationToken);
        if (series is null)
        {
            series = new Domain.Entities.NumberSeries(tenantId, "INV", fiscalYear, "{SERIES}-{BR}-{FY}-{SEQ}", branchId);
            await db.NumberSeries.AddAsync(series, cancellationToken);
        }

        var seq = series.ConsumeNext();
        var branchToken = branchId?.ToString("N")[..6] ?? "HQ";
        var number = series.FormatPattern
            .Replace("{SERIES}", series.SeriesKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{BR}", branchToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{FY}", fiscalYear.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{SEQ}", seq.ToString(), StringComparison.OrdinalIgnoreCase);

        return (series.Id, number);
    }

    /// <summary>PRD §14 fiscal-year convention: India FY starts April 1 (core.tenants.fiscal_year_start_month default 4) — hardcoded here to April for simplicity; a tenant-configurable start month is a documented gap.</summary>
    private static int ResolveFiscalYear(DateTime asOf) => asOf.Month >= 4 ? asOf.Year : asOf.Year - 1;

    private async Task<byte[]> RenderPdfBytesAsync(Guid tenantId, Matter matter, Client client, Invoice invoice, IReadOnlyList<InvoiceLine> lines, IReadOnlyList<InvoiceTax> taxes, CancellationToken cancellationToken)
    {
        var brandingJson = await db.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Key == "branding", cancellationToken);
        var firmDetailsJson = await db.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Key == "firm_details", cancellationToken);
        var branding = brandingJson is null ? null : JsonNode.Parse(brandingJson.ValueJson) as JsonObject;
        var firmDetails = firmDetailsJson is null ? null : JsonNode.Parse(firmDetailsJson.ValueJson) as JsonObject;
        var branch = matter.BranchId.HasValue ? await db.Branches.SingleOrDefaultAsync(b => b.Id == matter.BranchId, cancellationToken) : null;

        var model = new InvoicePdfModel(
            GetString(firmDetails, "firmName") ?? GetString(branding, "firmName") ?? "LexFlow",
            GetString(branding, "logoDataUri"),
            GetString(firmDetails, "address") ?? branch?.Address,
            branch?.Gstin ?? GetString(firmDetails, "gstin"),
            string.IsNullOrEmpty(invoice.Number) ? "DRAFT" : invoice.Number,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Currency,
            client.DisplayName ?? client.LegalName ?? $"{client.FirstName} {client.LastName}".Trim(),
            client.Gstin,
            null,
            lines.Select(l => new InvoicePdfLine(l.Description ?? l.Type, l.Qty, l.Unit, l.Rate, l.Amount)).ToList(),
            taxes.Select(t => new InvoicePdfTax(t.Name, t.RatePct, t.Amount)).ToList(),
            invoice.SubTotal,
            invoice.DiscountTotal,
            invoice.TaxTotal,
            invoice.GrandTotal,
            invoice.Notes);

        return pdfRenderer.Render(model);
    }

    private void RecordStatusHistory(Guid tenantId, Guid invoiceId, string? fromStatus, string toStatus, string? reason, Guid? changedBy)
        => db.InvoiceStatusHistory.Add(new InvoiceStatusHistory(tenantId, invoiceId, fromStatus, toStatus, reason, changedBy));

    private async Task<Invoice> GetInvoiceOrThrowAsync(Guid tenantId, Guid invoiceId, CancellationToken cancellationToken)
        => await db.Invoices.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, cancellationToken)
           ?? throw new NotFoundException(nameof(Invoice), invoiceId);

    private async Task<InvoiceDto> BuildDtoAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, IReadOnlyList<InvoiceTax> taxes, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new InvoiceDto(
            invoice.Id, invoice.Number, invoice.MatterId, invoice.ClientId, invoice.Status, invoice.IssueDate, invoice.DueDate,
            invoice.Currency, invoice.SubTotal, invoice.DiscountTotal, invoice.TaxTotal, invoice.GrandTotal, invoice.AmountPaid, invoice.Notes, invoice.PdfBlobPath,
            lines.Select(l => new InvoiceLineDto(l.Id, l.LineNo, l.Type, l.Description, l.Qty, l.Unit, l.Rate, l.Amount, l.TimeEntryIds)).ToList(),
            taxes.Select(t => new InvoiceTaxDto(t.Name, t.RatePct, t.TaxableAmount, t.Amount)).ToList());
    }

    private static IEnumerable<(string Description, decimal Amount)> ParseDueMilestones(string milestonesJson, DateOnly asOf)
    {
        if (JsonNode.Parse(milestonesJson) is not JsonArray array)
        {
            yield break;
        }

        foreach (var node in array)
        {
            if (node is not JsonObject obj)
            {
                continue;
            }

            var dueDateStr = GetString(obj, "dueDate");
            var invoiced = GetBool(obj, "invoiced", false);
            if (invoiced || dueDateStr is null || !DateOnly.TryParse(dueDateStr, out var dueDate) || dueDate > asOf)
            {
                continue;
            }

            yield return (GetString(obj, "description") ?? "Milestone", GetDecimal(obj, "amount", 0));
        }
    }

    private static string? GetString(JsonObject? node, string key) => node is not null && node.TryGetPropertyValue(key, out var v) && v is not null ? v.GetValue<string>() : null;

    private static decimal GetDecimal(JsonObject? node, string key, decimal fallback) => node is not null && node.TryGetPropertyValue(key, out var v) && v is not null ? v.GetValue<decimal>() : fallback;

    private static bool GetBool(JsonObject? node, string key, bool fallback) => node is not null && node.TryGetPropertyValue(key, out var v) && v is not null ? v.GetValue<bool>() : fallback;
}
