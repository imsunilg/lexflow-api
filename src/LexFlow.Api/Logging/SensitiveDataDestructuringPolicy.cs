using System.Collections;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace LexFlow.Api.Logging;

/// <summary>
/// PRD §29 PII policy: "sensitive fields (passwords, tokens, doc numbers, narratives)
/// never logged (destructuring policies + unit-tested redaction)". Rather than
/// enumerating every DTO by hand, this walks any destructured object's public
/// properties by name and masks ones that look sensitive — so a new request/response
/// type with a "Password" or "Token" property is redacted automatically instead of
/// depending on every future author remembering to opt in.
/// </summary>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly string[] SensitiveNameFragments =
    [
        "password",
        "token",
        "secret",
        "narrative",
        "docnumber",
        "documentnumber",
        "refreshhash",
        "authorization",
    ];

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue? result)
    {
        var type = value.GetType();

        // Only intervene for plain DTO-shaped types; leave primitives, collections,
        // and framework types to Serilog's normal destructuring.
        if (type.Namespace is null || !type.Namespace.StartsWith("LexFlow.", StringComparison.Ordinal) || value is IEnumerable)
        {
            result = null;
            return false;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var members = new List<LogEventProperty>(properties.Length);

        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? rawValue;
            try
            {
                rawValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            var propertyValue = IsSensitive(property.Name)
                ? propertyValueFactory.CreatePropertyValue(RedactedValue)
                : propertyValueFactory.CreatePropertyValue(rawValue, destructureObjects: true);

            members.Add(new LogEventProperty(property.Name, propertyValue));
        }

        result = new StructureValue(members, type.Name);
        return true;
    }

    private static bool IsSensitive(string propertyName)
    {
        var lowered = propertyName.ToLowerInvariant();
        return SensitiveNameFragments.Any(lowered.Contains);
    }
}
