using System;
using System.Linq.Expressions;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal class PropertyInfoResult
    {
        public bool HasGetter { get; private set; }
        public bool HasSetter { get; private set; }
        public bool IsValid => _fieldInfo != null;
        public Type? PropertyType { get; private set; }
        private object? _getterFunc;
        private object? _setterFunc;
        private PropertyInfo? _fieldInfo;
        private string _name = string.Empty;

        public PropertyInfoResult() 
        {
        }

        public PropertyInfoResult(PropertyInfo? fieldInfo)
        {
            PropertyType = fieldInfo?.PropertyType;
            HasGetter = fieldInfo?.GetGetMethod() != null;
            HasSetter = fieldInfo?.GetSetMethod() != null;
            _getterFunc = null;
            _setterFunc = null;
            _fieldInfo = fieldInfo;
            _name = fieldInfo?.Name ?? "None";
        }

        public bool Set<T>(object target, object? value)
        {
            if (!IsValid)
            {
                System.Diagnostics.Debug.Write($"Failed to create setter for {typeof(T)}");
                return false;
            }

            if (!HasSetter)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] setter for {_name} {typeof(T)} is absent");
                return false;
            }


            if (target?.GetType() != typeof(T))
            {
                System.Diagnostics.Debug.Write($"[FastBinding] target [{_name}] should be {typeof(T)} but in reality is {target?.GetType()?.ToString() ?? "None"}");
                return false;
            }

            if (value?.GetType() != PropertyType)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] setter for {_name} {PropertyType?.ToString() ?? "none"} but value is {value?.GetType()?.ToString() ?? "None"}");

                return false;
            }

            var action = _setterFunc as Action<T, object?>;
            if (action == null)
            {
                action = CreateSetter<T>(target);
                _setterFunc = action;
                if (action == null)
                    return false;
            }

            action((T)target, value);
            return true;
        }

        public object? Get<T>(object target)
        {
            if (!IsValid)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create getter for {typeof(T)}");
                return false;
            }

            if (!HasGetter)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] getter for {_name} {typeof(T)} is absent");
                return null;
            }

            if (target?.GetType() != typeof(T))
            {
                System.Diagnostics.Debug.Write($"[FastBinding] target [{_name}] should be {typeof(T)} but in reality is {target?.GetType()?.ToString() ?? "None"}");
                return false;
            }

            var func = _getterFunc as Func<T, object?>;
            if (func == null)
            {
                func = CreateGetter<T>(target);
                _getterFunc = func;
                if (func == null)
                {
                    return null;
                }
            }

            return func((T)target);
        }

        private Func<T, object?>? CreateGetter<T>(object target)
        {
            try
            {
                var param = Expression.Parameter(target.GetType(), "x");
                var propertyAccess = Expression.Property(param, _fieldInfo!);
                var convert = Expression.Convert(propertyAccess, typeof(object));
                return Expression.Lambda<Func<T, object>>(convert, param).Compile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create expression tree for getter");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }

        private Action<T, object?>? CreateSetter<T>(object target)
        {
            try
            {
                var param = Expression.Parameter(typeof(T), "x");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var propertyAccess = Expression.Property(param, _fieldInfo!);
                var assign = Expression.Assign(propertyAccess, Expression.Convert(valueParam, PropertyType!));
                return Expression.Lambda<Action<T, object?>>(assign, param, valueParam).Compile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create expression tree for setter");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }
    }

}
