using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace LexFlow.Api.Logging;

/// <summary>
/// Ties Serilog's structured log stream to the active OpenTelemetry span (PRD §29:
/// "Correlation: traceparent propagated UI→API→bus→workers ... on every entry") by
/// pushing trace_id/span_id from System.Diagnostics.Activity — the same Activity
/// OpenTelemetry's ASP.NET Core instrumentation already creates per request — onto
/// every log event, without adding a separate Serilog↔OTel bridge package.
/// </summary>
public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("trace_id", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("span_id", activity.SpanId.ToString()));
    }
}
