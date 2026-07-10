using FluentAssertions;
using LexFlow.Infrastructure.Kb;

namespace LexFlow.UnitTests.Kb;

/// <summary>Module 12 Validation Rules: "Citation format validated against known patterns (SCC/AIR/SCR/neutral) with free-form fallback flag." AC-KB1's section-jump also depends on this parser.</summary>
public sealed class KbCitationQueryParserTests
{
    [Theory]
    [InlineData("IPC 420")]
    [InlineData("Companies Act 197")]
    public void Parse_recognizes_a_section_lookup(string query)
    {
        var result = KbCitationQueryParser.Parse(query);

        result.Kind.Should().Be(KbQueryKind.SectionLookup);
        result.SectionNumber.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_recognizes_a_section_lookup_with_a_lettered_suffix()
    {
        var result = KbCitationQueryParser.Parse("IPC 420A");

        result.Kind.Should().Be(KbQueryKind.SectionLookup);
        result.ActRef.Should().Be("IPC");
        result.SectionNumber.Should().Be("420A");
    }

    [Theory]
    [InlineData("(2023) 5 SCC 1", "SCC")]
    [InlineData("AIR 2020 SC 123", "AIR")]
    [InlineData("[2019] 3 SCR 45", "SCR")]
    [InlineData("2023 INSC 123", "Neutral")]
    public void Parse_recognizes_known_citation_formats(string query, string expectedFormat)
    {
        var result = KbCitationQueryParser.Parse(query);

        result.Kind.Should().Be(KbQueryKind.Citation);
        result.CitationFormat.Should().Be(expectedFormat);
    }

    [Theory]
    [InlineData("cheque bounce defense")]
    [InlineData("negotiable instruments act interpretation")]
    public void Parse_falls_back_to_freeform_for_unrecognized_queries(string query)
    {
        var result = KbCitationQueryParser.Parse(query);

        result.Kind.Should().Be(KbQueryKind.Freeform);
    }
}
