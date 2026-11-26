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
    /// The value change of a plain binding in one compiled delegate, shared by every binding with the same key: it reads the source
    /// property typed and writes the target with the typed SetValue&lt;T&gt;, so the value never becomes an object on its way —
    /// (target, source) =&gt; target.SetValue&lt;TProp&gt;(Property, ((TSource)source).Name, LocalValue).
    /// Made only where the slow path would write the value as it is (the target's type takes the source property's type, its
    /// IsValidType rule); null otherwise, and the binding takes the slow path.
    /// </summary>
    internal static class DirectPushers
    {
        private static readonly ConcurrentDictionary<(Type Source, string Name, AvaloniaProperty Property), Action<AvaloniaObject, object>?> Pushers
            = new ConcurrentDictionary<(Type, string, AvaloniaProperty), Action<AvaloniaObject, object>?>();

        private static readonly MethodInfo? SetStyled = typeof(AvaloniaObject).GetMethods().FirstOrDefault(m =>
            m.Name == nameof(AvaloniaObject.SetValue) && m.IsGenericMethodDefinition && m.GetParameters().Length == 3
            && m.GetParameters()[0].ParameterType.IsGenericType
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(StyledProperty<>));

        private static readonly MethodInfo? SetDirect = typeof(AvaloniaObject).GetMethods().FirstOrDefault(m =>
            m.Name == nameof(AvaloniaObject.SetValue) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2
            && m.GetParameters()[0].ParameterType.IsGenericType
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(DirectPropertyBase<>));

        public static Action<AvaloniaObject, object>? For(Type sourceType, string name, AvaloniaProperty property)
        {
            var key = (sourceType, name, property);
            if (Pushers.TryGetValue(key, out var pusher))
                return pusher;
            return Pushers.GetOrAdd(key, Create);
        }

        private static Action<AvaloniaObject, object>? Create((Type Source, string Name, AvaloniaProperty Property) key)
        {
            try
            {
                var info = key.Source.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance);
                if (info == null || info.GetMethod == null || info.GetIndexParameters().Length != 0)
                    return null;
                var to = key.Property.PropertyType;
                if (!to.IsAssignableFrom(info.PropertyType))
                    return null;   // the slow path's IsValidType would refuse or decide per value: keep it there
                var target = Expression.Parameter(typeof(AvaloniaObject), "target");
                var source = Expression.Parameter(typeof(object), "source");
                Expression value = Expression.Property(Expression.Convert(source, key.Source), info);
                if (value.Type != to)
                    value = Expression.Convert(value, to);   // a reference conversion, or T to T?
                var propertyType = key.Property.GetType();
                Expression call;
                if (SetStyled != null && Find(propertyType, typeof(StyledProperty<>)) is Type styled)
                    call = Expression.Call(target, SetStyled.MakeGenericMethod(to), Expression.Constant(key.Property, styled), value,
                        Expression.Constant(BindingPriority.LocalValue));
                else if (SetDirect != null && Find(propertyType, typeof(DirectPropertyBase<>)) is Type direct)
                    call = Expression.Call(target, SetDirect.MakeGenericMethod(to), Expression.Constant(key.Property, direct), value);
                else
                    return null;
                return Expression.Lambda<Action<AvaloniaObject, object>>(Expression.Block(typeof(void), call), target, source).Compile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] No direct path for {key.Source.Name}.{key.Name} -> {key.Property.Name}: {ex.Message}");
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
    }
}
