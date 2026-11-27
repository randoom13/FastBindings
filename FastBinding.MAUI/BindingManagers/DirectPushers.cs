using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using FastBindings.Helpers;

namespace FastBindings.BindingManagers
{
    /// <summary>
    /// The value change of a plain binding in one delegate, shared by every binding with the same key: it reads the source property
    /// through a delegate to its getter and writes the target, with no lookup by name and no checks per change —
    /// (target, source) =&gt; target.SetValue(Property, ((TSource)source).Name). Made only where the general path would write the
    /// value as it is (the target's type takes the source property's type); null otherwise, and the binding takes that path.
    /// </summary>
    internal static class DirectPushers
    {
        private static readonly ConcurrentDictionary<(Type Source, string Name, BindableProperty Property), Action<BindableObject, object>?> Pushers
            = new ConcurrentDictionary<(Type, string, BindableProperty), Action<BindableObject, object>?>();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static Action<BindableObject, object>? For(Type sourceType, string name, BindableProperty property)
        {
            var key = (sourceType, name, property);
            if (Pushers.TryGetValue(key, out var pusher))
                return pusher;
            return Pushers.GetOrAdd(key, Create);
        }

        private static Action<BindableObject, object>? Create((Type Source, string Name, BindableProperty Property) key)
        {
            try
            {
                var info = key.Source.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance);
                if (info == null || info.GetMethod == null || info.GetIndexParameters().Length != 0)
                    return null;
                var pusher = FastAccessors.Pusher(info, key.Property);
                if (pusher != null)
                    FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._delegates);
                return pusher;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] No direct path for {key.Source.Name}.{key.Name} -> {key.Property.PropertyName}: {ex.Message}");
                return null;
            }
        }
    }
}
