using System;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;

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

        /// <summary>The property named <paramref name="name"/> of <paramref name="ownerType"/>, or none (then a read is <see cref="Diagnostics.BindingIssueKind.PathNotFound"/>).</summary>
        public PropertyInfoResult(PropertyInfo? fieldInfo, string name, Type ownerType) : this(fieldInfo)
        {
            _name = name;
            _ownerType = ownerType;
        }

        private Type? _ownerType;

        public PropertyInfoResult(PropertyInfo? fieldInfo)
        {
            PropertyType = fieldInfo?.PropertyType;
            HasGetter = fieldInfo?.GetGetMethod() != null;
            HasSetter = fieldInfo?.GetSetMethod() != null;
            _getterFunc = null;
            _setterFunc = null;
            _fieldInfo = fieldInfo;
            _name = fieldInfo?.Name ?? "None";
            _ownerType = fieldInfo?.DeclaringType;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool Set(Type targetType, object target, object? value)
        {
            if (!IsValid)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound, detail: $"'{_name}' is not a property of {targetType.Name}", value: value);
                return false;
            }

            if (!HasSetter)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.NoSetter, detail: $"{targetType.Name}.{_name} has no setter", value: value);
                return false;
            }

            if (target?.GetType() != targetType)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.SetterThrew,
                    detail: $"{targetType.Name}.{_name} was asked to set an object of type {target?.GetType().Name ?? "null"}", value: value);
                return false;
            }
            if (!ReflectionUtility.IsValidType(value, PropertyType) && !ReflectionUtility.TryConvert(PropertyType, ref value))
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.WriteBackTypeMismatch,
                    detail: $"{targetType.Name}.{_name} is {PropertyType?.Name ?? "?"}, the value is {value?.GetType().Name ?? "null"}", value: value);
                return false;
            }

            var action = _setterFunc;
            if (action == null)
            {
                action = ExpressionTreeHelper.CreateSetter(_fieldInfo, targetType, target);
                if (action == null)
                    return false;
                // two threads may compile it at once: one delegate is kept, both work
                action = Interlocked.CompareExchange(ref _setterFunc, action, null) ?? action;
            }

            action(target, value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public object? Get(Type targetType, object target)
        {
            // A name that is not a property, or one without a getter, reads as an error value (the binding shows its fallback, and a
            // longer path passes it on unread): it read as false, so a TextBlock bound to a misspelled name showed "False".
            if (target is ExceptionHolder)
                return target;

            if (!IsValid)
                return NotReadable(Diagnostics.BindingIssueKind.PathNotFound, $"'{_name}' is not a property of {(_ownerType ?? targetType).Name}");

            if (!HasGetter)
                return NotReadable(Diagnostics.BindingIssueKind.NoGetter, $"{targetType.Name}.{_name} has no getter");

            if (target?.GetType() != targetType)
                return NotReadable(Diagnostics.BindingIssueKind.GetterThrew, $"{targetType.Name}.{_name} was asked to read an object of type {target?.GetType().Name ?? "null"}");

            var func = _getterFunc;
            if (func == null)
            {
                func = ExpressionTreeHelper.CreateGetter(_fieldInfo, targetType, target);
                if (func == null)
                {
                    return null;
                }
                func = Interlocked.CompareExchange(ref _getterFunc, func, null) ?? func;
            }

            return func(target);
        }

        private static ExceptionHolder NotReadable(Diagnostics.BindingIssueKind kind, string detail)
        {
            if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(kind, detail: detail);
            return new ExceptionHolder(new Diagnostics.BindingPathException(kind, detail));
        }

    }

}
