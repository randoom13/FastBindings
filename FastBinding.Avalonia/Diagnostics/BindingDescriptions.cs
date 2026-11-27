using Avalonia;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace FastBindings.Diagnostics
{
    /// <summary>
    /// A binding's description, taken while its XAML loads and only while a logger is set: its kind, the public parameters that
    /// differ from their defaults (Sources first, unnamed, as written), the target, and the view's file from the loader.
    /// </summary>
    internal static class BindingDescriptions
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_parameters = new ConcurrentDictionary<Type, PropertyInfo[]>();

        internal static BindingDescription? For(object binding, string targetType, string targetProperty, IServiceProvider? services)
        {
            if (!BindingLog.IsOn)
                return null;
            try
            {
                var view = (services?.GetService(typeof(IUriContext)) as IUriContext)?.BaseUri?.ToString();
                return BindingDescription.Of(binding.GetType().Name, ParametersOf(binding), targetType, targetProperty, view);
            }
            catch (Exception)
            {
                return BindingDescription.Unknown;
            }
        }

        private static string ParametersOf(object binding)
        {
            var text = new StringBuilder();
            foreach (var property in s_parameters.GetOrAdd(binding.GetType(), Parameters))
            {
                var value = property.GetValue(binding);
                if (IsDefault(property, value))
                    continue;
                if (property.Name == "Sources")
                {
                    text.Insert(0, value + (text.Length > 0 ? ", " : ""));
                    continue;
                }
                if (text.Length > 0)
                    text.Append(", ");
                text.Append(property.Name).Append('=').Append(Show(value));
            }
            return text.ToString().TrimEnd(' ', ',');
        }

        private static PropertyInfo[] Parameters(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.DeclaringType != typeof(AvaloniaObject)
                    && p.DeclaringType != typeof(object) && !typeof(AvaloniaObject).GetProperties().Any(o => o.Name == p.Name))
                .OrderBy(p => p.Name == "Sources" ? 0 : 1).ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();

        private static bool IsDefault(PropertyInfo property, object? value)
        {
            if (value == null || value is string { Length: 0 })
                return true;
            if (property.GetCustomAttribute<DefaultValueAttribute>() is { } declared && Equals(declared.Value, value))
                return true;
            return value.GetType().IsValueType && Equals(value, Activator.CreateInstance(value.GetType()));
        }

        private static string Show(object? value) => value switch
        {
            string s => s,
            Enum e => e.ToString(),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            null => "null",
            _ => value.GetType().Name,
        };
    }
}
