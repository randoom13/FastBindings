using Avalonia;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal static class ReflectionUtility
    {
        /// <summary>
        /// The conversion an Avalonia binding makes between the source's and the target's types (its DefaultValueConverter's:
        /// <see cref="Avalonia.Utilities.TypeUtilities.TryConvert"/>) — an int into a double, the text "False" of a FallBackValue
        /// into a bool, an enum's name — in the invariant culture, as XAML is written. False when there is none.
        /// </summary>
        public static bool TryConvert(Type? expectingType, ref object? value)
        {
            if (expectingType == null || value == null)
                return false;
            if (!Avalonia.Utilities.TypeUtilities.TryConvert(expectingType, value, System.Globalization.CultureInfo.InvariantCulture, out var converted)
                || ReferenceEquals(converted, AvaloniaProperty.UnsetValue) || !IsValidType(converted, expectingType))
                return false;
            value = converted;
            return true;
        }

        public static bool IsValidType(object? value, Type? expectingType)
        {
            bool result;
            if (value == null)
            {
                // null fits any type but a non-nullable value type. It was "IsClass", which an interface is not: null was refused for
                // ItemsSource (IEnumerable), Command (ICommand) and every other interface-typed property, so a list or command whose
                // source became null — or whose DataContext went away — kept its old value (and a list kept its rows, subscribed).
                result = expectingType == null || !expectingType.IsValueType || Nullable.GetUnderlyingType(expectingType) != null;
            }
            else
            {
                var type = value.GetType();
                result = type == expectingType || expectingType?.IsAssignableFrom(type) == true;
            }
            return result;   // a mismatch is reported by the caller, which knows what it means there
        }


        public static bool ContainsInterface(Type type, params Type[] genericInterfaces)
        {
            var typeInterfaces = type.GetInterfaces();
            return typeInterfaces.Any(i => i.IsGenericType && (genericInterfaces.Contains(i.GetGenericTypeDefinition())));
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Type, string Name), AvaloniaProperty?> DependencyProperties =
            new System.Collections.Concurrent.ConcurrentDictionary<(Type, string), AvaloniaProperty?>();

        public static AvaloniaProperty? FindDependencyPropertyByName(AvaloniaObject obj, string? propertyName)
        {
            if (obj == null)
               throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException(nameof(propertyName));

            // looked up once per (type, name): every static field listed and filtered per $[[...]] source cost ~2.7 µs and ~1.6 KB
            return DependencyProperties.GetOrAdd((obj.GetType(), propertyName!), static key =>
                key.Type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .Where(it => it.Name == key.Name + "Property").ToList().SingleOrDefault()?.GetValue(null) as AvaloniaProperty);
        }

        public static EventInfo? FindEventByName(AvaloniaObject obj, string? eventName)
        {
            if (obj == null)
                throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException(nameof(eventName));

            // Get the event info
            var eventInfo = obj.GetType().GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
            return eventInfo;
        }

        public static PropertyInfoResult BildPropertyInfo(AvaloniaObject obj, string? propertyName)
        {
            try
            {
                if (obj == null) 
                    throw new ArgumentException(nameof(obj));

                if (string.IsNullOrEmpty(propertyName)) 
                    throw new ArgumentException(nameof(propertyName));

                var fieldInfo = obj.GetType().GetProperty(propertyName);
                return new PropertyInfoResult(fieldInfo);
            }
            catch (Exception ex) 
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound, ex, $"'{propertyName}' on {obj?.GetType().Name ?? "null"}");
                return new PropertyInfoResult();
            }
        }
    }
}
