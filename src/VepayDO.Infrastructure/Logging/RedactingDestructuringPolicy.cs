using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace VepayDO.Infrastructure.Logging;

public sealed partial class RedactingDestructuringPolicy : IDestructuringPolicy
{
    public const string Placeholder = "***";
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> Cache = new();

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        if (value is null) { result = null; return false; }

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) { result = null; return false; }

        var properties = Cache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray());

        if (properties.Count == 0) { result = null; return false; }

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
                try { raw = property.GetValue(value); }
                catch (TargetInvocationException) { continue; }
                propertyValue = propertyValueFactory.CreatePropertyValue(raw, destructureObjects: false);
            }

            logProperties.Add(new LogEventProperty(name, propertyValue));
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }
}
