using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Ai;

/// <summary>See ICitationVerifier's own doc comment. "Exists" and "retrievable by caller" are both checked by the single IAiRetrievalGuard.CanAccessAsync call (it already returns false for a nonexistent id), so there is only one code path to keep honest, not two.</summary>
public sealed class CitationVerifier(IAiRetrievalGuard retrievalGuard) : ICitationVerifier
{
    public async Task<CitationVerificationResult> VerifyAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, IReadOnlyList<AiCitation> citations, CancellationToken cancellationToken = default)
    {
        var verified = new List<AiCitation>();
        var stripped = new List<AiCitation>();

        foreach (var citation in citations)
        {
            var accessible = await retrievalGuard.CanAccessAsync(tenantId, callerId, callerPermissions, citation.Kind, citation.Id, cancellationToken);
            (accessible ? verified : stripped).Add(citation);
        }

        return new CitationVerificationResult(verified, stripped);
    }
}
