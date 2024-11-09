
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;

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

        public static bool IsValidType(object value, Type expectingType)
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

        public static DependencyProperty FindDependencyPropertyByName(DependencyObject obj, string propertyName)
        {
            if (obj == null) 
                throw new ArgumentException(nameof(obj));

            if (string.IsNullOrEmpty(propertyName)) 
                throw new ArgumentException(nameof(propertyName));

           Type type = obj.GetType();
           var field = type.GetProperty(propertyName);
            var properties = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Where(it =>it.Name == $"{propertyName}Property").ToList();
            var t = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Where(it => it.Name == propertyName).Select(it => it.GetValue(null) as DependencyProperty);
            return properties?.SingleOrDefault()?.GetValue(obj) as DependencyProperty;
        }

        public static EventInfo FindEventByName(DependencyObject obj, string eventName)
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

                PropertyInfo fieldInfo = obj.GetType().GetProperty(propertyName);
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
