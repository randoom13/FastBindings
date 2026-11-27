using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

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
            _propertyInfos = new Lazy<PropertyInfo[]>(() => TargetType.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }

        // Read and filled from any thread a binding runs on (async bindings, view-models raising PropertyChanged off the UI thread):
        // a plain Dictionary written by two threads at once can lose entries or loop forever.
        private readonly Lazy<PropertyInfo[]> _propertyInfos;
        private readonly ConcurrentDictionary<string, PropertyInfoResult> _propertyByNames = new ConcurrentDictionary<string, PropertyInfoResult>(StringComparer.Ordinal);

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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private PropertyInfoResult GetPropertyInfoResult(string propertyName)
        {
            if (_propertyByNames.TryGetValue(propertyName, out var propertyInfo))
                return propertyInfo;
            return _propertyByNames.GetOrAdd(propertyName,
                name => new PropertyInfoResult(_propertyInfos.Value.FirstOrDefault(it => it.Name == name), name, TargetType));
        }

        /// <summary>The getter and setter of one property of this type, looked up once (a binding keeps it per source type).</summary>
        internal PropertyInfoResult Find(string propertyName) => GetPropertyInfoResult(propertyName);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
        // Shared by every binding of the process, whatever thread it runs on.
        private static readonly ConcurrentDictionary<Type, PropertyAccessor> _propertyAccessorByType = new ConcurrentDictionary<Type, PropertyAccessor>();
        private static readonly ConcurrentDictionary<Type, MethodsAccessor> _methodAccessorByType = new ConcurrentDictionary<Type, MethodsAccessor>();

        public static MethodsAccessor GetMethodAccessor(Type type)
        {
            if (_methodAccessorByType.TryGetValue(type, out var accessor))
                return accessor;
            return _methodAccessorByType.GetOrAdd(type, t => new MethodsAccessor(t));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static PropertyAccessor GetPropertyAccessor(Type type)
        {
            if (_propertyAccessorByType.TryGetValue(type, out var accessor))
                return accessor;
            return _propertyAccessorByType.GetOrAdd(type, t => new PropertyAccessor(t));
        }

        internal static PropertyAccessor GetPropertyAccessor<T>()
        {
            return GetPropertyAccessor(typeof(T));
        }
    }
}
