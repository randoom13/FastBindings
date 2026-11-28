using Avalonia;
using Avalonia.Data;
using System;
using FastBindings;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Linq;
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



        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Action<AvaloniaObject, object?> For(AvaloniaProperty property) => Setters.GetOrAdd(property, Create);

        // A typed setter made from a generic method for the property's value type — no expression compiled (that took ~3 ms per
        // property, even with the compiler warm: about 150 ms of a view's start for some fifty bound properties). A value the typed
        // setter cannot take (a type the property does not have, a null for a value type) goes through SetValue(object) as before.
        private static Action<AvaloniaObject, object?> Create(AvaloniaProperty property)
        {
            try
            {
                var type = property.PropertyType;
                var propertyType = property.GetType();
                string? kind = IsGeneric(propertyType, typeof(StyledProperty<>), out _) ? nameof(Typed<object>.Styled)
                    : IsGeneric(propertyType, typeof(DirectPropertyBase<>), out _) ? nameof(Typed<object>.Direct) : null;
                if (kind == null)
                    return Untyped(property);
                FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._delegates);
                return (Action<AvaloniaObject, object?>)typeof(Typed<>).MakeGenericType(type).GetMethod(kind)!.Invoke(null, new object[] { property })!;
            }
            catch (Exception ex)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.FastPathUnavailable, ex, $"no typed setter for {property.Name}");
                return Untyped(property);
            }
        }

        private static class Typed<TValue>
        {
            private static readonly bool TakesNull = !typeof(TValue).IsValueType || Nullable.GetUnderlyingType(typeof(TValue)) != null;

            public static Action<AvaloniaObject, object?> Styled(AvaloniaProperty property)
            {
                var styled = (StyledProperty<TValue>)property;
                return (target, value) =>
                {
                    if (value is TValue typed)
                        target.SetValue(styled, typed, BindingPriority.LocalValue);
                    else if (value == null && TakesNull)
                        target.SetValue(styled, default(TValue)!, BindingPriority.LocalValue);
                    else
                        target.SetValue(property, value);
                };
            }

            public static Action<AvaloniaObject, object?> Direct(AvaloniaProperty property)
            {
                var direct = (DirectPropertyBase<TValue>)property;
                return (target, value) =>
                {
                    if (value is TValue typed)
                        target.SetValue(direct, typed);
                    else if (value == null && TakesNull)
                        target.SetValue(direct, default(TValue)!);
                    else
                        target.SetValue(property, value);
                };
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
