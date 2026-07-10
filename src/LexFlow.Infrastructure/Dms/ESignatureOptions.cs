namespace LexFlow.Infrastructure.Dms;

/// <summary>Bound from configuration section "ESignature". Per-provider base URL + bearer token — a full OAuth/JWT-grant flow (DocuSign) or Adobe Sign's own OAuth dance is out of scope here; both providers accept a long-lived integration-key bearer token in sandbox/dev.</summary>
public sealed class ESignatureOptions
{
    public const string SectionName = "ESignature";

    public string DocuSignBaseUrl { get; set; } = "https://demo.docusign.net/restapi";
    public string? DocuSignAccessToken { get; set; }
    public string? DocuSignAccountId { get; set; }

    public string AdobeSignBaseUrl { get; set; } = "https://api.na1.adobesign.com/api/rest/v6";
    public string? AdobeSignAccessToken { get; set; }
}
