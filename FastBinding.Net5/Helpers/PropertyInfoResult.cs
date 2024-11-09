using System;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal class PropertyInfoResult
    {
        public bool HasGetter { get; private set; }
        public bool HasSetter { get; private set; }
        public bool IsValid => _fieldInfo != null;
        public Type? PropertyType { get; private set; }
        private Func<object, object?>? _getterFunc;
        private Action<object, object?>? _setterFunc;
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

        public bool Set(Type targetType, object target, object? value)
        {
            if (!IsValid)
            {
                System.Diagnostics.Debug.Write($"Failed to create setter for {targetType}");
                return false;
            }

            if (!HasSetter)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] setter for {_name} {targetType} is absent");
                return false;
            }

            if (target?.GetType() != targetType)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] target [{_name}] should be {targetType} but in reality is {target?.GetType()?.ToString() ?? "None"}");
                return false;
            }

            if (!ReflectionUtility.IsValidType(value, PropertyType))
            {
                System.Diagnostics.Debug.Write($"[FastBinding] setter for {_name} {PropertyType?.ToString() ?? "none"} but value is {value?.GetType()?.ToString() ?? "None"}");

                return false;
            }

            var action = _setterFunc;
            if (action == null)
            {
                action = ExpressionTreeHelper.CreateSetter(_fieldInfo, targetType, target);
                _setterFunc = action;
                if (action == null)
                    return false;
            }

            action(target, value);
            return true;
        }


        public object? Get(Type targetType, object target)
        {
            if (!IsValid)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create getter for {targetType}");
                return false;
            }

            if (!HasGetter)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] getter for {_name} {targetType} is absent");
                return null;
            }

            if (target?.GetType() != targetType)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] target [{_name}] should be {targetType} but in reality is {target?.GetType()?.ToString() ?? "None"}");
                return false;
            }

            var func = _getterFunc;
            if (func == null)
            {
                func = ExpressionTreeHelper.CreateGetter(_fieldInfo, targetType, target);
                _getterFunc = func;
                if (func == null)
                {
                    return null;
                }
            }
            return func(target);
        }
    }

}
