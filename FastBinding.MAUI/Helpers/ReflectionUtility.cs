using System.Reflection;

namespace FastBindings.Helpers
{
    internal static class ReflectionUtility
    {
        public static object? FindBindableProperty(BindableObject? obj, BindableObject? objinside)
        {
            try
            {
                if (obj == null)
                    throw new ArgumentException(nameof(obj));

                if (objinside == null)
                    throw new ArgumentException(nameof(objinside));

                var properties = obj.GetType().GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).ToList();
                var filter = properties.Select(it => it.GetValue(obj)).OfType<BindableProperty>()
                    .FirstOrDefault(it => obj.IsSet(it) && obj.GetValue(it) == objinside);
                return filter;
            }
            catch (Exception ex)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound, ex,
                    $"which property of {obj?.GetType().Name ?? "null"} holds the {objinside?.GetType().Name ?? "null"} the binding sets", target: objinside);
                return BindableProperty.UnsetValue;
            }
        }

        public static bool IsValidTypeEx(Type? type, Type? expectingType)
        {

            if (type == null)
            {
                // check on nullable type
                return expectingType == null || (expectingType.IsClass || Nullable.GetUnderlyingType(expectingType) != null);
            }
            else
            {
                return type == expectingType || expectingType?.IsAssignableFrom(type) == true;
            }
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

        public static BindableProperty? FindDependencyPropertyByName(BindableObject? obj, string? propertyName)
        {
            if (obj == null)
                throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException(nameof(propertyName));

            var properties = obj.GetType().GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Where(it => it.Name == $"{propertyName}Property").ToList();
            return properties?.SingleOrDefault()?.GetValue(obj) as BindableProperty;
        }

        public static EventInfo? FindEventByName(BindableObject? obj, string? eventName)
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

        public static PropertyInfoResult BildPropertyInfo(BindableObject? obj, string? propertyName)
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
