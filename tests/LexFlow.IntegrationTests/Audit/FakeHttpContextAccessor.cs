using Microsoft.AspNetCore.Http;

namespace LexFlow.IntegrationTests.Audit;

/// <summary>Stands in for the real per-request IHttpContextAccessor so the interceptor sees a deterministic actor/ip/ua/trace.</summary>
internal sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
