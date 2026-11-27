using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace FastBindings.Helpers
{
    internal static class ReflectionUtility
    {
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

        public static DependencyProperty? FindDependencyPropertyByName(DependencyObject obj, string? propertyName)
        {
            if (obj == null)
                throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException(nameof(propertyName));

            Type type = obj.GetType();
            var fields =
                 type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                 .Where(it => it.FieldType == typeof(DependencyProperty) && it.Name == $"{propertyName}Property");
            return fields?.SingleOrDefault()?.GetValue(null) as DependencyProperty;
        }

        public static EventInfo? FindEventByName(DependencyObject? obj, string? eventName)
        {
            if (obj == null)
                throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException(nameof(eventName));

            var type = obj.GetType();
            // Get the event info
            var eventInfo = type.GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
            return eventInfo;
        }

        public static PropertyInfoResult BildPropertyInfo(DependencyObject obj, string propertyName)
        {
            try
            {
                if (obj == null)
                    throw new ArgumentException(nameof(obj));

                if (string.IsNullOrEmpty(propertyName))
                    throw new ArgumentException(nameof(propertyName));

                var fieldInfo = obj?.GetType()?.GetProperty(propertyName);
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
