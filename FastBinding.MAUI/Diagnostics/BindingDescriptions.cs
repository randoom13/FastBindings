using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
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
    /// differ from their defaults (Sources first, unnamed, as written), the target, and the view from the loader (its root object's type and the line).
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
                var root = (services?.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider)?.RootObject ?? OutermostParent(services);
                var line = (services?.GetService(typeof(IXmlLineInfoProvider)) as IXmlLineInfoProvider)?.XmlLineInfo;
                var view = root == null ? null
                    : line != null && line.HasLineInfo() ? root.GetType().FullName + ":" + line.LineNumber : root.GetType().FullName;
                return BindingDescription.Of(binding.GetType().Name, ParametersOf(binding), targetType, targetProperty, view);
            }
            catch (Exception)
            {
                return BindingDescription.Unknown;
            }
        }

        /// <summary>
        /// Compiled XAML (XamlC) gives no IRootObjectProvider; its value target lists the objects around the one being set, the
        /// view's root last (MAUI's non-public IProvideParentValues.ParentObjects, read by name).
        /// </summary>
        private static object? OutermostParent(IServiceProvider? services)
        {
            var target = services?.GetService(typeof(IProvideValueTarget));
            if (target == null)
                return null;
            var property = target.GetType().GetInterfaces().Select(i => i.GetProperty("ParentObjects")).FirstOrDefault(p => p != null)
                ?? target.GetType().GetProperty("ParentObjects", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return (property?.GetValue(target) as System.Collections.Generic.IEnumerable<object>)?.LastOrDefault();
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
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.DeclaringType != typeof(BindableObject)
                    && p.DeclaringType != typeof(object) && !typeof(BindableObject).GetProperties().Any(o => o.Name == p.Name))
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
