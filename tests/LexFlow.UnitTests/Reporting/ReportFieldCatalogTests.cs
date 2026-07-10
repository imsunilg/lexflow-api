using FluentAssertions;
using LexFlow.Infrastructure.Reporting;

namespace LexFlow.UnitTests.Reporting;

/// <summary>Module 13 Validation: "custom builder field whitelist only (no raw SQL ever)."</summary>
public sealed class ReportFieldCatalogTests
{
    [Theory]
    [InlineData("Matter")]
    [InlineData("Invoice")]
    [InlineData("TimeEntry")]
    [InlineData("Lead")]
    [InlineData("Task")]
    [InlineData("Hearing")]
    public void IsValidBaseEntity_accepts_every_PRD_named_base_entity(string baseEntity)
    {
        ReportFieldCatalog.IsValidBaseEntity(baseEntity).Should().BeTrue();
    }

    [Fact]
    public void IsValidBaseEntity_rejects_an_entity_outside_the_whitelist()
    {
        ReportFieldCatalog.IsValidBaseEntity("User").Should().BeFalse();
    }

    [Fact]
    public void IsValidField_accepts_a_whitelisted_field()
    {
        ReportFieldCatalog.IsValidField("Matter", "Status").Should().BeTrue();
    }

    [Fact]
    public void IsValidField_rejects_a_field_not_in_the_catalog()
    {
        ReportFieldCatalog.IsValidField("Matter", "BillingArrangementJson").Should().BeFalse();
    }

    [Fact]
    public void IsValidField_rejects_a_field_that_belongs_to_a_different_base_entity()
    {
        ReportFieldCatalog.IsValidField("Matter", "GrandTotal").Should().BeFalse();
    }
}
