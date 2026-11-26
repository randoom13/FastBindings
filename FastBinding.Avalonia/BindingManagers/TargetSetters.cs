using Avalonia;
using Avalonia.Data;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace FastBindings.BindingManagers
{
    /// <summary>
    /// A setter compiled once per target property and shared by every binding to it: the typed SetValue&lt;T&gt; of a StyledProperty
    /// or DirectProperty, instead of SetValue(AvaloniaProperty, object), which finds the typed one again on every call.
    /// </summary>
    internal static class TargetSetters
    {
        private static readonly ConcurrentDictionary<AvaloniaProperty, Action<AvaloniaObject, object?>> Setters
            = new ConcurrentDictionary<AvaloniaProperty, Action<AvaloniaObject, object?>>();

        private static readonly MethodInfo? SetStyled = typeof(AvaloniaObject).GetMethods().FirstOrDefault(m =>
            m.Name == nameof(AvaloniaObject.SetValue) && m.IsGenericMethodDefinition && m.GetParameters().Length == 3
            && m.GetParameters()[0].ParameterType.IsGenericType
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(StyledProperty<>));

        private static readonly MethodInfo? SetDirect = typeof(AvaloniaObject).GetMethods().FirstOrDefault(m =>
            m.Name == nameof(AvaloniaObject.SetValue) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2
            && m.GetParameters()[0].ParameterType.IsGenericType
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(DirectPropertyBase<>));

        public static Action<AvaloniaObject, object?> For(AvaloniaProperty property) => Setters.GetOrAdd(property, Create);

        private static Action<AvaloniaObject, object?> Create(AvaloniaProperty property)
        {
            try
            {
                var target = Expression.Parameter(typeof(AvaloniaObject), "target");
                var value = Expression.Parameter(typeof(object), "value");
                var type = property.PropertyType;
                // UnsetValue (TargetNullValue ?? UnsetValue) and values of another type keep the untyped path
                var typed = Expression.Convert(value, type);
                Expression? call = null;
                var propertyType = property.GetType();
                if (SetStyled != null && IsGeneric(propertyType, typeof(StyledProperty<>), out var styled))
                    call = Expression.Call(target, SetStyled.MakeGenericMethod(type), Expression.Constant(property, styled!), typed,
                        Expression.Constant(BindingPriority.LocalValue));
                else if (SetDirect != null && IsGeneric(propertyType, typeof(DirectPropertyBase<>), out var direct))
                    call = Expression.Call(target, SetDirect.MakeGenericMethod(type), Expression.Constant(property, direct!), typed);
                if (call == null)
                    return Untyped(property);
                var fast = Expression.Lambda<Action<AvaloniaObject, object?>>(Expression.Block(typeof(void), call), target, value).Compile();
                var slow = Untyped(property);
                return (t, v) =>
                {
                    if (v == null ? !type.IsValueType || Nullable.GetUnderlyingType(type) != null : type.IsInstanceOfType(v))
                        fast(t, v);
                    else
                        slow(t, v);
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create a typed setter for {property.Name}");
                System.Diagnostics.Debug.Write(ex);
                return Untyped(property);
            }
        }

        private static Action<AvaloniaObject, object?> Untyped(AvaloniaProperty property) => (t, v) => t.SetValue(property, v);

        private static bool IsGeneric(Type type, Type definition, out Type? found)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == definition)
                {
                    found = t;
                    return true;
                }
            }
            found = null;
            return false;
        }
    }
}
