using FluentAssertions;
using LexFlow.Api.Logging;
using Serilog.Events;

namespace LexFlow.UnitTests.Logging;

/// <summary>
/// PRD §29: "sensitive fields (passwords, tokens, doc numbers, narratives) never
/// logged (destructuring policies + unit-tested redaction)". Proves the policy masks
/// sensitive-looking properties by name and leaves everything else untouched, for any
/// LexFlow.* DTO — not just a hand-picked list of known request types.
/// </summary>
public sealed class SensitiveDataDestructuringPolicyTests
{
    private sealed record SamplePayload(string Email, string Password, string AccessToken, string CaseNarrative, int RetryCount);

    [Fact]
    public void TryDestructure_masks_sensitive_properties_and_preserves_others()
    {
        var policy = new SensitiveDataDestructuringPolicy();
        var payload = new SamplePayload("user@example.com", "hunter2", "eyJhbGciOi...", "Client alleges breach of contract on 12.07.2026", 3);

        var succeeded = policy.TryDestructure(payload, new StubPropertyValueFactory(), out var result);

        succeeded.Should().BeTrue();
        var structure = result.Should().BeOfType<StructureValue>().Subject;

        GetScalar(structure, nameof(SamplePayload.Password)).Should().Be("***REDACTED***");
        GetScalar(structure, nameof(SamplePayload.AccessToken)).Should().Be("***REDACTED***");
        GetScalar(structure, nameof(SamplePayload.CaseNarrative)).Should().Be("***REDACTED***");

        GetScalar(structure, nameof(SamplePayload.Email)).Should().Be("user@example.com");
        GetScalar(structure, nameof(SamplePayload.RetryCount)).Should().Be(3);
    }

    [Fact]
    public void TryDestructure_declines_non_LexFlow_types()
    {
        var policy = new SensitiveDataDestructuringPolicy();

        var succeeded = policy.TryDestructure("a plain string", new StubPropertyValueFactory(), out var result);

        succeeded.Should().BeFalse();
        result.Should().BeNull();
    }

    private static object? GetScalar(StructureValue structure, string propertyName)
    {
        var property = structure.Properties.Single(p => p.Name == propertyName);
        return property.Value switch
        {
            ScalarValue scalar => scalar.Value,
            _ => property.Value,
        };
    }

    private sealed class StubPropertyValueFactory : Serilog.Core.ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
            => new ScalarValue(value);
    }
}
