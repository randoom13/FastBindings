using System.Reflection;

namespace FastBindings.Helpers
{
    internal class PropertyAccessor 
    {
        public Type TargetType { private set; get; }

        public PropertyAccessor(Type targetType) 
        {
            if (targetType == null)
                throw new ArgumentException(nameof(targetType));

            TargetType = targetType;
        }

        private PropertyInfo[]? _propertyInfos;
        private readonly Dictionary<string, PropertyInfoResult> _propertyByNames = new Dictionary<string, PropertyInfoResult>();

        private void Verify(object target, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) 
            {
                throw new ArgumentException($"{nameof(propertyName)}");
            }
            if (target == null)
            {
                throw new ArgumentException($"{nameof(target)}");
            }
            if (target.GetType() != TargetType)
            {
                throw new ArgumentException($"{nameof(target)} should be {TargetType}, not {target.GetType()}");
            }
        }

        public Type? GetPropertyGetterType(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) 
                return null;

            var info = GetPropertyInfoResult(propertyName);
            return info.HasGetter ? info.PropertyType : null;
        }

        private PropertyInfoResult GetPropertyInfoResult(string propertyName)
        {
            PropertyInfoResult? propertyInfo;
            if (!_propertyByNames.TryGetValue(propertyName, out propertyInfo))
            {
                _propertyInfos = _propertyInfos ?? TargetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                propertyInfo = new PropertyInfoResult(_propertyInfos?.FirstOrDefault(it => it.Name == propertyName));
                _propertyByNames[propertyName] = propertyInfo;
            }
            return propertyInfo;
        }

        public object? GetProperty(object target, string propertyName)
        {
            Verify(target, propertyName);
            return GetPropertyInfoResult(propertyName).Get(TargetType, target);
        }

        public void SetProperty(object target, string propertyName, object? value)
        {
            Verify(target, propertyName);
            GetPropertyInfoResult(propertyName).Set(TargetType, target, value);
        }
    }

    internal static class MembersHelper
    {
        private static Dictionary<Type, PropertyAccessor> _propertyAccessorByType = new Dictionary<Type, PropertyAccessor>();
        private static Dictionary<Type, MethodsAccessor> _methodAccessorByType = new Dictionary<Type, MethodsAccessor>();

        public static MethodsAccessor GetMethodAccessor(Type type)
        {
            MethodsAccessor? accessor = null;
            if (!_methodAccessorByType.TryGetValue(type, out accessor))
            {
                accessor = new MethodsAccessor(type);
                _methodAccessorByType[type] = accessor;
            }

            return accessor;
        }

        public static PropertyAccessor GetPropertyAccessor(Type type)
        {
            PropertyAccessor? accessor = null;
            if (!_propertyAccessorByType.TryGetValue(type, out accessor))
            {
                accessor = new PropertyAccessor(type);
                _propertyAccessorByType[type] = accessor;
            }

            return accessor;
        }

        internal static PropertyAccessor GetPropertyAccessor<T>()
        {
            return GetPropertyAccessor(typeof(T));
        }
    }
}
