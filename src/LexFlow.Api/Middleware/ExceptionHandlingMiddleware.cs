using System.Diagnostics;
using System.Net;
using System.Text.Json;
using LexFlow.Api.Contracts;
using LexFlow.Application.Common.Exceptions;
using ValidationException = LexFlow.Application.Common.Exceptions.ValidationException;

namespace LexFlow.Api.Middleware;

/// <summary>
/// Single choke point mapping typed exceptions to the response envelope and HTTP
/// semantics defined in PRD §17 (envelope) and §28 (error handling): every error
/// carries a W3C traceparent-derived traceId; internals are never leaked on 500s.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
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
            ValidationException validationException => (
                HttpStatusCode.BadRequest,
                "VALIDATION_FAILED",
                validationException.Errors
                    .SelectMany(e => e.Value.Select(msg => new ApiErrorDetail { Field = e.Key, Code = msg }))
                    .ToArray()),
            NotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND", null),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, "FORBIDDEN", null),
            DomainRuleException domainRuleException => (HttpStatusCode.UnprocessableEntity, domainRuleException.SubCode, null),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHENTICATED", null),
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
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
