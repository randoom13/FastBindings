using System.Diagnostics;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal static class ReflectionUtility
    {
        private static string GetCallerMethodInfo()
        {
            // Get the call stack
            var stackTrace = new StackTrace();

            // Get the method that called the current method (index 2 is the calling method)
            var callingMethod = stackTrace.GetFrame(2)?.GetMethod();
            if (callingMethod != null)
            {
                if (callingMethod.DeclaringType != null)
                    return $"{callingMethod.Name}-{callingMethod.DeclaringType.Name}";

                else return callingMethod.Name;
            }
            return "Unknown";
        }

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
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to find matched bindable property");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
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
                // check on nullable type
                result = expectingType == null || (expectingType.IsClass || Nullable.GetUnderlyingType(expectingType) != null);
            }
            else
            {
                var type = value.GetType();
                result = type == expectingType || expectingType?.IsAssignableFrom(type) == true;
            }
            if (!result)
            {
                Debug.Write($"[FastBinding][{GetCallerMethodInfo()}] Expect type:{value?.GetType()?.ToString() ?? "None"} Actual type:{expectingType?.ToString() ?? "None"}");
            }
            return result;
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
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to BildPropertyInfo");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return new PropertyInfoResult();
            }
        }
    }
}
