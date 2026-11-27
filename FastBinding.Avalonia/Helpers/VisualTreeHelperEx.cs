using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace FastBindings.Helpers
{
    internal class VisualTreeHelperEx
    {
        public static bool IsAvailable(AvaloniaObject obj) =>
            (obj as Control)?.GetVisualParent() != null || (obj as ILogical)?.LogicalParent != null;

        public static T? GetParent<T>(AvaloniaObject obj) where T : AvaloniaObject
        {
            return (obj as Control)?.GetVisualParent() as T;
        }

        public static AvaloniaObject? GetParent(AvaloniaObject obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = GetParent<AvaloniaObject>(item);
                if (item?.GetType() == type)
                {
                    lev--;
                }

            }
            return item;
        }

        public static AvaloniaObject? GetLogicalParent(AvaloniaObject? obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = (item as ILogical)?.LogicalParent as AvaloniaObject;
                if (item?.GetType() == type)
                {
                    lev--;
                }

            }
            return item;
        }

        /// <summary>
        /// The type, or one it derives from, has this name: an ancestor is found as Avalonia's $parent[T] finds it (a view
        /// derived from UserControl is a UserControl), not only by its own type's name.
        /// </summary>
        private static bool IsOrDerives(Type? type, string typeName)
        {
            for (; type != null; type = type.BaseType)
                if (type.Name == typeName)
                    return true;
            return false;
        }

        public static AvaloniaObject? GetParent(AvaloniaObject obj, int level, string typeName)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = (item as Control)?.GetVisualParent() as AvaloniaObject;
                if (IsOrDerives(item?.GetType(), typeName))
                {
                    lev--;
                }
            }
            return item;
        }
        public static AvaloniaObject? GetLogicalParent(AvaloniaObject obj, int level, string typeName)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = (item as ILogical)?.LogicalParent as AvaloniaObject;
                if (IsOrDerives(item?.GetType(), typeName))
                {
                    lev--;
                }
            }
            return item;
        }

        public static AvaloniaObject? FindChildByName(AvaloniaObject? parent, string childName)
        {
            if (parent == null) 
                return null;

            var children = (parent as Control)?.GetVisualChildren()?.OfType<AvaloniaObject>() ?? Enumerable.Empty<AvaloniaObject>();
            foreach (var child in children)
            {

                if (child is Control frameworkElement && frameworkElement.Name == childName)
                {
                    return child;
                }

                // Recursively search in the child
                var childFound = FindChildByName(child, childName);
                if (childFound != null)
                {
                    return childFound;
                }
            }

            return null;
        }
    }
}
