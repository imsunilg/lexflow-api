using System.Reflection;
using FluentAssertions;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.UnitTests.Ai;

/// <summary>
/// BR-19/AC-AI5: "Every AI output rendered with AI-badge and requires explicit Save/Insert
/// action." This asserts, by reflection over the whole Application assembly, that no AI response
/// DTO can omit the badge — every type whose name matches the "AiXxxResponse"/"AiXxxDto" shape
/// used by Module 16 features must derive from AiGeneratedResponse, whose IsAiGenerated property
/// has no setter (not even init) and is therefore structurally impossible to override to false.
/// </summary>
public sealed class AiGeneratedResponseTests
{
    private static readonly Type[] AiResponseTypes = typeof(AiGeneratedResponse).Assembly
        .GetTypes()
        .Where(t => t.IsPublic && t.Namespace == typeof(AiGeneratedResponse).Namespace)
        .Where(t => t != typeof(AiGeneratedResponse))
        .Where(t => (t.Name.StartsWith("Ai", StringComparison.Ordinal) && t.Name.EndsWith("Response", StringComparison.Ordinal))
            || t.Name == "AiTranscriptionDto")
        .ToArray();

    [Fact]
    public void At_least_one_AI_response_type_is_discovered_by_the_reflection_scan()
    {
        // Guards against the scan itself silently finding nothing (e.g. a namespace/naming
        // convention change) and this test suite going green for the wrong reason.
        AiResponseTypes.Should().HaveCountGreaterThanOrEqualTo(12);
    }

    [Fact]
    public void Every_AI_response_type_derives_from_AiGeneratedResponse()
    {
        var offenders = AiResponseTypes.Where(t => !typeof(AiGeneratedResponse).IsAssignableFrom(t)).ToList();

        offenders.Should().BeEmpty("every AI feature response DTO must carry the AI-generated badge (BR-19/AC-AI5) — offending types: {0}", string.Join(", ", offenders.Select(t => t.Name)));
    }

    [Fact]
    public void IsAiGenerated_has_no_setter_and_therefore_cannot_be_overridden_to_false()
    {
        var property = typeof(AiGeneratedResponse).GetProperty(nameof(AiGeneratedResponse.IsAiGenerated))!;

        property.CanWrite.Should().BeFalse("IsAiGenerated must be structurally impossible to set to false — no setter, not even init");
    }

    [Fact]
    public void A_concrete_AI_response_instance_reports_IsAiGenerated_true_and_a_non_empty_disclaimer()
    {
        var response = new AiChatResponse(Guid.NewGuid(), "Some AI-drafted text.", []);

        response.IsAiGenerated.Should().BeTrue();
        response.Disclaimer.Should().NotBeNullOrWhiteSpace();
    }
}
