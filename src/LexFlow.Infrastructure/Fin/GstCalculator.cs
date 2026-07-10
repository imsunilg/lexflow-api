namespace LexFlow.Infrastructure.Fin;

/// <summary>
/// BR-9 Tax: "India: place-of-supply = client billing state vs firm branch state -&gt; intra:
/// CGST+SGST split equally; inter: IGST; SEZ/export zero-rated with flag; tax rows stored per
/// line" (computed at invoice level here, matching PRD §17's POST /invoices example response
/// shape — one CGST/SGST or IGST pair per invoice, not per line). Pure/static so it is directly
/// exercisable by the §37 FsCheck property tests without any DB or service scaffolding.
/// Rounding: line-level round-half-even (Module 8 Edge Cases), so the sum of the returned tax
/// lines plus the (already-rounded) taxable amount reconciles to the grand total exactly — no
/// separate grand-total rounding step is ever applied on top.
/// </summary>
public static class GstCalculator
{
    public static IReadOnlyList<GstLine> Calculate(decimal taxableAmount, string? clientStateCode, string? firmStateCode, decimal cgstPct, decimal sgstPct, decimal igstPct, bool isZeroRated)
    {
        if (isZeroRated || taxableAmount <= 0)
        {
            return [];
        }

        // No firm state to compare against, or no client state on record (B2C with no billing
        // address state) -> documented simplification: treat as intra-state (CGST+SGST), the more
        // common case for a domestic firm's walk-in/individual clients.
        var isInterState = firmStateCode is not null && clientStateCode is not null && !string.Equals(firmStateCode, clientStateCode, StringComparison.OrdinalIgnoreCase);

        if (isInterState)
        {
            return [new GstLine("IGST", igstPct, taxableAmount, Round(taxableAmount * igstPct / 100m))];
        }

        var cgstAmount = Round(taxableAmount * cgstPct / 100m);
        var sgstAmount = Round(taxableAmount * sgstPct / 100m);
        return [new GstLine("CGST", cgstPct, taxableAmount, cgstAmount), new GstLine("SGST", sgstPct, taxableAmount, sgstAmount)];
    }

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);

    /// <summary>GST state codes are the first two characters of a GSTIN (e.g. "27" for Maharashtra).</summary>
    public static string? ExtractStateCode(string? gstin) => string.IsNullOrWhiteSpace(gstin) || gstin.Length < 2 ? null : gstin[..2];
}

public sealed record GstLine(string Name, decimal RatePct, decimal TaxableAmount, decimal Amount);
