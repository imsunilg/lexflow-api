using FluentAssertions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Ai;

namespace LexFlow.UnitTests.Ai;

/// <summary>Module 16 Error Handling: "hallucinated-citation detector strips and annotates." AC-AI3: "injected fake-citation test is caught."</summary>
public sealed class CitationVerifierTests
{
    [Fact]
    public async Task VerifyAsync_keeps_a_real_accessible_citation_and_strips_a_fabricated_one()
    {
        var realId = Guid.NewGuid();
        var fakeId = Guid.NewGuid();
        var guard = new SelectiveGuard(allow: realId);
        var verifier = new CitationVerifier(guard);

        var result = await verifier.VerifyAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            [new AiCitation("KbJudgment", realId, null), new AiCitation("KbJudgment", fakeId, null)],
            CancellationToken.None);

        result.Verified.Should().ContainSingle(c => c.Id == realId);
        result.Stripped.Should().ContainSingle(c => c.Id == fakeId);
        result.AnyStripped.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_returns_no_stripped_citations_when_every_citation_is_accessible()
    {
        var id = Guid.NewGuid();
        var verifier = new CitationVerifier(new SelectiveGuard(allow: id));

        var result = await verifier.VerifyAsync(Guid.NewGuid(), Guid.NewGuid(), [], [new AiCitation("Document", id, null)], CancellationToken.None);

        result.AnyStripped.Should().BeFalse();
        result.Verified.Should().ContainSingle();
    }

    private sealed class SelectiveGuard(Guid allow) : IAiRetrievalGuard
    {
        public Task<bool> CanAccessAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, string sourceKind, Guid sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(sourceId == allow);
    }
}
