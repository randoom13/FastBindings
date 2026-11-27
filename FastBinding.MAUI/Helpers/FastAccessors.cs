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
        /// The direct path's pusher: the source property read through a delegate to its getter and written into the target
        /// property. MAUI's SetValue takes an object, so a value type is boxed here as by any binding; what the direct path saves is
        /// the lookups and checks between the change and the write. Null where the value cannot go straight in (a value-type
        /// mismatch such as T into T?, a struct declaring type, a type the target does not take).
        /// </summary>
        public static Action<BindableObject, object>? Pusher(PropertyInfo source, BindableProperty target)
        {
            var get = source.GetGetMethod();
            if (get == null || get.IsStatic || get.GetParameters().Length != 0 || source.DeclaringType == null || source.DeclaringType.IsValueType)
                return null;
            var to = target.ReturnType;
            if (source.PropertyType != to && (source.PropertyType.IsValueType || to.IsValueType || !to.IsAssignableFrom(source.PropertyType)))
                return null;   // only a value the target takes as it is
            try
            {
                return (Action<BindableObject, object>)typeof(Typed<,>).MakeGenericType(source.DeclaringType, source.PropertyType)
                    .GetMethod(nameof(Typed<object, object>.Pusher))!.Invoke(null, new object[] { get, target })!;
            }
            catch (Exception ex)
            {
                // the general (compiled) accessor is used instead
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.FastPathUnavailable, ex, "no delegate accessor");
                return null;
            }
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

            public static Action<BindableObject, object> Pusher(MethodInfo get, BindableProperty property)
            {
                var read = (Func<TSource, TValue>)get.CreateDelegate(typeof(Func<TSource, TValue>));
                return (target, source) => target.SetValue(property, read((TSource)source));
            }
        }
    }
}
