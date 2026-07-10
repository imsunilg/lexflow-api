namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 16 Error Handling: "hallucinated-citation detector strips and annotates." AC-AI3:
/// "Research answers contain only verifiable KB citations; injected fake-citation test is
/// caught." A citation an LLM emits is never trusted at face value — every one is cross-checked
/// against the real record (does it exist, and is it retrievable by *this* caller — reusing
/// <see cref="IAiRetrievalGuard"/>, the same check RAG retrieval itself uses) before it's allowed
/// into a response; anything that fails either check is stripped, not silently kept.
/// </summary>
public interface ICitationVerifier
{
    Task<CitationVerificationResult> VerifyAsync(Guid tenantId, Guid callerId, IReadOnlyCollection<string> callerPermissions, IReadOnlyList<AiCitation> citations, CancellationToken cancellationToken = default);
}

public sealed record AiCitation(string Kind, Guid Id, string? Label);

public sealed record CitationVerificationResult(IReadOnlyList<AiCitation> Verified, IReadOnlyList<AiCitation> Stripped)
{
    public bool AnyStripped => Stripped.Count > 0;
}
