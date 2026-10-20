using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace VepayDO.Api.Kernel.Logging;

/// <summary>
/// Masks sensitive values before they ever reach a log sink (AGENTS.md section 13):
/// sender names, account digits, references, tokens and passwords must never be
/// persisted in plain text. The policy destructures any object whose property
/// name matches a sensitive name into a <c>***</c> placeholder, so an accidental
/// <c>logger.LogInformation("{@Receipt}", receipt)</c> cannot leak personal or
/// financial data.
/// </summary>
public sealed class RedactingDestructuringPolicy : IDestructuringPolicy
{
    /// <summary>The placeholder written in place of a redacted value.</summary>
    public const string Placeholder = "***";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordhash",
        "newpassword",
        "currentpassword",
        "currentpasswordhash",
        "token",
        "tokenhash",
        "accesstoken",
        "refreshtoken",
        "refreshtokenhash",
        "passwordresettoken",
        "resettoken",
        "authorization",
        "cookie",
        "setcookie",
        "secret",
        "jwtaccesssecret",
        "refreshtokenpepper",
        "apikey",
        "sender",
        "sendername",
        "sendernamenormalized",
        "remitente",
        "reference",
        "referencenormalized",
        "last4",
        "destinationlast4",
        "accountdigits",
        "cardnumber",
        "email",
    };

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> Cache = new();

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        if (value is null)
        {
            result = null;
            return false;
        }

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(string))
        {
            result = null;
            return false;
        }

        var properties = Cache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray());

        if (properties.Count == 0)
        {
            result = null;
            return false;
        }

        var logProperties = new List<LogEventProperty>(properties.Count);
        foreach (var property in properties)
        {
            var name = property.Name;
            LogEventPropertyValue propertyValue;

            if (SensitiveNames.Contains(name))
            {
                propertyValue = new ScalarValue(Placeholder);
            }
            else
            {
                object? raw;
                try
                {
                    raw = property.GetValue(value);
                }
                catch (TargetInvocationException)
                {
                    continue;
                }

                propertyValue = propertyValueFactory.CreatePropertyValue(raw, destructureObjects: false);
            }

            logProperties.Add(new LogEventProperty(name, propertyValue));
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }
}
