
using System;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace FastBindings.Helpers
{
    internal static class PropertyUtility
    {
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
