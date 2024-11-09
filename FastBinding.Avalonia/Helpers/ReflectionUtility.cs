using Avalonia;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal static class ReflectionUtility
    {
        private static string GetCallerMethodInfo()
        {
            var stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(2)?.GetMethod();
            if (callingMethod != null)
            {
                if (callingMethod.DeclaringType != null)
                    return $"{callingMethod.Name}-{callingMethod.DeclaringType.Name}";

                else return callingMethod.Name;
            }
            return "Unknown";
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

        public static AvaloniaProperty? FindDependencyPropertyByName(AvaloniaObject obj, string? propertyName)
        {
            if (obj == null)
               throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException(nameof(propertyName));

            var properties = obj.GetType().GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Where(it => it.Name == $"{propertyName}Property").ToList();
            return properties?.SingleOrDefault()?.GetValue(obj) as AvaloniaProperty;
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
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to BildPropertyInfo");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return new PropertyInfoResult();
            }
        }
    }
}
