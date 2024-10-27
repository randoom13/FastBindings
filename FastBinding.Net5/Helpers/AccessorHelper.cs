using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal class PropertyAccessor<T>
    {
        private PropertyInfo[]? _propertyInfos;
        private readonly Dictionary<string, PropertyInfoResult> _propertyByNames = new Dictionary<string, PropertyInfoResult>();

        private static void Verify(object target, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) 
            {
                throw new ArgumentException($"{nameof(propertyName)}");
            }
            if (target == null)
            {
                throw new ArgumentException($"{nameof(target)}");
            }
            if (target.GetType() != typeof(T))
            {
                throw new ArgumentException($"{nameof(target)} should be {typeof(T)}, not {target.GetType()}");
            }
        }

        private PropertyInfoResult GetPropertyInfoResult(string propertyName)
        {
            PropertyInfoResult? propertyInfo;
            if (!_propertyByNames.TryGetValue(propertyName, out propertyInfo))
            {
                _propertyInfos = _propertyInfos ?? typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                propertyInfo = new PropertyInfoResult(_propertyInfos?.FirstOrDefault(it => it.Name == propertyName));
                _propertyByNames[propertyName] = propertyInfo;
            }
            return propertyInfo;
        }


        public object? GetProperty(object target, string propertyName)
        {
            Verify(target, propertyName);
            return GetPropertyInfoResult(propertyName).Get<T>(target);
        }

        public void SetProperty(object target, string propertyName, object? value)
        {
            Verify(target, propertyName);
            GetPropertyInfoResult(propertyName).Set<T>(target, value);
        }
    }


    internal static class AccessorHelper
    {
        private static Dictionary<Type, object> _propertyAccessorByType = new Dictionary<Type, object>();
        internal static PropertyAccessor<T> GetPropertyAccessor<T>()
        {
            PropertyAccessor<T>? accessor = null;
            if (!_propertyAccessorByType.TryGetValue(typeof(T), out object? accessorObj))
            {
                accessor = new PropertyAccessor<T>();
                _propertyAccessorByType[typeof(T)] = accessor;
            }
            else accessor = (PropertyAccessor<T>)accessorObj;

            return accessor;
        }
    }
}
