using Avalonia;
using Avalonia.Data;
using System;
using System.Reflection;

namespace FastBindings.Helpers
{
    /// <summary>
    /// Accessors made from a property's own get/set methods with MethodInfo.CreateDelegate — no expression tree, no code emitted.
    /// Compiling an expression tree took ~0.35 ms each, and a view starting a thousand bindings of a thousand (type, property)
    /// pairs paid it a thousand times; a delegate to the method costs microseconds. For reference types the generic code is
    /// shared, so a new view-model type costs no JIT either. Null where it does not apply (a struct declaring type, a
    /// property type no generic argument can take): the caller compiles an expression tree as before.
    /// </summary>
    internal static class FastAccessors
    {
        public static Func<object, object?>? Getter(PropertyInfo property)
        {
            var get = property.GetGetMethod();
            if (get == null || get.IsStatic || get.GetParameters().Length != 0 || property.DeclaringType == null || property.DeclaringType.IsValueType)
                return null;
            try
            {
                return (Func<object, object?>)typeof(Typed<,>).MakeGenericType(property.DeclaringType, property.PropertyType)
                    .GetMethod(nameof(Typed<object, object>.Getter))!.Invoke(null, new object[] { get })!;
            }
            catch (Exception ex)
            {
                // the general (compiled) accessor is used instead
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.FastPathUnavailable, ex, "no delegate accessor");
                return null;
            }
        }

        public static Action<object, object?>? Setter(PropertyInfo property)
        {
            var set = property.GetSetMethod();
            if (set == null || set.IsStatic || set.GetParameters().Length != 1 || property.DeclaringType == null || property.DeclaringType.IsValueType)
                return null;
            try
            {
                return (Action<object, object?>)typeof(Typed<,>).MakeGenericType(property.DeclaringType, property.PropertyType)
                    .GetMethod(nameof(Typed<object, object>.Setter))!.Invoke(null, new object[] { set })!;
            }
            catch (Exception ex)
            {
                // the general (compiled) accessor is used instead
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.FastPathUnavailable, ex, "no delegate accessor");
                return null;
            }
        }

        /// <summary>
        /// The direct path's pusher: the source property read through a delegate to its getter, typed as the target property's
        /// type (a reference conversion by the delegate's covariant return), written with SetValue&lt;T&gt;. Null where that does not
        /// apply (a value-type mismatch such as T into T?, a struct declaring type).
        /// </summary>
        public static Action<AvaloniaObject, object>? Pusher(PropertyInfo source, AvaloniaProperty target)
        {
            var get = source.GetGetMethod();
            if (get == null || get.IsStatic || get.GetParameters().Length != 0 || source.DeclaringType == null || source.DeclaringType.IsValueType)
                return null;
            var to = target.PropertyType;
            if (source.PropertyType != to && (source.PropertyType.IsValueType || to.IsValueType))
                return null;   // only a reference conversion can be done by the delegate itself
            var kind = Find(target.GetType(), typeof(StyledProperty<>)) != null ? nameof(Typed<object, object>.StyledPusher)
                : Find(target.GetType(), typeof(DirectPropertyBase<>)) != null ? nameof(Typed<object, object>.DirectPusher) : null;
            if (kind == null)
                return null;
            try
            {
                return (Action<AvaloniaObject, object>)typeof(Typed<,>).MakeGenericType(source.DeclaringType, to)
                    .GetMethod(kind)!.Invoke(null, new object[] { get, target })!;
            }
            catch (Exception ex)
            {
                // the general (compiled) accessor is used instead
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.FastPathUnavailable, ex, "no delegate accessor");
                return null;
            }
        }

        private static Type? Find(Type type, Type definition)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == definition)
                    return t;
            return null;
        }

        private static class Typed<TSource, TValue> where TSource : class
        {
            public static Func<object, object?> Getter(MethodInfo get)
            {
                var read = (Func<TSource, TValue>)get.CreateDelegate(typeof(Func<TSource, TValue>));
                return source => read((TSource)source);
            }

            public static Action<object, object?> Setter(MethodInfo set)
            {
                var write = (Action<TSource, TValue>)set.CreateDelegate(typeof(Action<TSource, TValue>));
                return (source, value) => write((TSource)source, (TValue)value!);
            }

            public static Action<AvaloniaObject, object> StyledPusher(MethodInfo get, AvaloniaProperty property)
            {
                var read = (Func<TSource, TValue>)get.CreateDelegate(typeof(Func<TSource, TValue>));
                var styled = (StyledProperty<TValue>)property;
                return (target, source) => target.SetValue(styled, read((TSource)source), BindingPriority.LocalValue);
            }

            public static Action<AvaloniaObject, object> DirectPusher(MethodInfo get, AvaloniaProperty property)
            {
                var read = (Func<TSource, TValue>)get.CreateDelegate(typeof(Func<TSource, TValue>));
                var direct = (DirectPropertyBase<TValue>)property;
                return (target, source) => target.SetValue(direct, read((TSource)source));
            }
        }
    }
}
