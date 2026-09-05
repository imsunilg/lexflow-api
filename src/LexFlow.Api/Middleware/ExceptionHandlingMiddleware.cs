using System.Diagnostics;
using System.Net;
using System.Text.Json;
using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using ValidationException = LexFlow.Application.Common.Exceptions.ValidationException;

namespace LexFlow.Api.Middleware;

/// <summary>
/// Single choke point mapping typed exceptions to the response envelope and HTTP
/// semantics defined in PRD §17 (envelope) and §28 (error handling table): every
/// error carries a W3C traceparent-derived traceId; internals are never leaked on 500s.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    // ASP.NET Core's MVC pipeline serializes controller-returned bodies (Ok(...)) with
    // camelCase property names by default. This middleware writes directly to the response
    // body instead of going through that pipeline, so without explicitly matching those
    // options here, every error response came back PascalCase (Success/Error/Code/Message)
    // while every success response was camelCase (success/data) — silently breaking every
    // Angular error handler that reads `error.error.error.message` (got `undefined`, since
    // the real key was `Message`), which is why the UI only ever showed generic fallback
    // messages like "Something went wrong" instead of the specific backend error.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, code, details) = exception switch
        {
            // 400 — shape/validation.
            ValidationException validationException => (
                HttpStatusCode.BadRequest,
                "VALIDATION_FAILED",
                validationException.Errors
                    .SelectMany(e => e.Value.Select(msg => new ApiErrorDetail { Field = e.Key, Code = msg }))
                    .ToArray()),

            // 401 — authn.
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHENTICATED", null),

            // 403 — authz (record exists but forbidden). Cross-tenant access instead
            // throws NotFoundException (404) to prevent enumeration — see that type.
            ForbiddenAccessException => (HttpStatusCode.Forbidden, "FORBIDDEN", null),

            // 404 — not found, also used for cross-tenant access.
            NotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND", null),

            // 409 — state/uniqueness/idempotent-replay.
            ConflictException conflictException => (HttpStatusCode.Conflict, conflictException.Code, null),

            // 412 — concurrency (ETag/If-Match on PUT of versioned aggregates).
            ConcurrencyConflictException => (HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED", null),
            DbUpdateConcurrencyException => (HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED", null),

            // 402-style — Module 16 Error Handling: "quota exceeded -> 402-style AI_QUOTA_EXCEEDED with upgrade CTA."
            AiQuotaExceededException => ((HttpStatusCode)402, "AI_QUOTA_EXCEEDED", null),

            // 422 — domain rule (sub-coded: CONFLICT_OF_INTEREST_SUSPECTED, INSUFFICIENT_TRUST_BALANCE, ...).
            DomainRuleException domainRuleException => (HttpStatusCode.UnprocessableEntity, domainRuleException.SubCode, null),

            // 429 — rate limited.
            RateLimitExceededException => ((HttpStatusCode)429, "RATE_LIMITED", null),

            // 500 — no internals leaked; generic message + traceId.
            _ => (HttpStatusCode.InternalServerError, "SERVER_ERROR", null),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception. traceId={TraceId}", traceId);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception {Code}. traceId={TraceId}", code, traceId);
        }

        if (exception is RateLimitExceededException rateLimitExceeded)
        {
            context.Response.Headers.RetryAfter = rateLimitExceeded.RetryAfterSeconds.ToString();
        }

        var message = statusCode == HttpStatusCode.InternalServerError
            ? "An unexpected error occurred."
            : exception.Message;

        var response = new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = code,
                Message = message,
                TraceId = traceId,
                Details = details,
            },
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
