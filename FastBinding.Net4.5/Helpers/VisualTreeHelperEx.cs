using System;
using System.Windows;
using System.Windows.Media;
using System.Linq;
namespace FastBindings.Helpers
{
    internal class VisualTreeHelperEx
    {
        public static bool CanSubscribeOnDataContext(DependencyObject? obj) =>
           obj != null &&  (obj is FrameworkElement || obj is FrameworkContentElement);

        public static bool SubscribeOnDataContext(DependencyObject? obj, DependencyPropertyChangedEventHandler dataContextChanged)
        {
            var frameworkElement = obj as FrameworkElement;
            if (frameworkElement != null)
            {
                frameworkElement.DataContextChanged += dataContextChanged;
                return true;
            }
            else
            {
                var frameworkContentElement = obj as FrameworkContentElement;
                if (frameworkContentElement != null)
                {
                    frameworkContentElement.DataContextChanged += dataContextChanged;
                    return true;
                }
            }
            return false;
        }

        public static void UnSubscribeOnDataContext(DependencyObject? obj, DependencyPropertyChangedEventHandler dataContextChanged)
        {
            var frameworkElement = obj as FrameworkElement;
            if (frameworkElement != null)
            {
                frameworkElement.DataContextChanged -= dataContextChanged;
            }
            else
            {
                var frameworkContentElement = obj as FrameworkContentElement;
                if (frameworkContentElement != null)
                    frameworkContentElement.DataContextChanged -= dataContextChanged;
            }
        }

        public static bool SubscribeOnLoaded(DependencyObject? obj, RoutedEventHandler onLoaded)
        {
            var frameworkElement = obj as FrameworkElement;
            if (frameworkElement != null)
            {
                frameworkElement.Loaded += onLoaded;
                return true;
            }
            else
            {
                var frameworkContentElement = obj as FrameworkContentElement;
                if (frameworkContentElement != null)
                {
                    frameworkContentElement.Loaded += onLoaded;
                    return true;
                }
            }
            return false;
        }

        public static bool UnSubscribeOnLoaded(DependencyObject? obj, RoutedEventHandler onLoaded)
        {
            var frameworkElement = obj as FrameworkElement;
            if (frameworkElement != null)
            {
                frameworkElement.Loaded -= onLoaded;
                return true;
            }
            else
            {
                var frameworkContentElement = obj as FrameworkContentElement;
                if (frameworkContentElement != null)
                {
                    frameworkContentElement.Loaded -= onLoaded;
                    return true;
                }
            }
            return false;
        }

        public static bool IsAvailable(DependencyObject obj) =>
            VisualTreeHelper.GetParent(obj) != null || LogicalTreeHelper.GetParent(obj) != null;

        public static T? GetParent<T>(DependencyObject obj) where T : DependencyObject
        {
            return VisualTreeHelper.GetParent(obj) as T;
        }

        public static DependencyObject? GetParent(DependencyObject? obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = VisualTreeHelper.GetParent(item);
                if (item?.GetType() == type)
                {
                    lev--;
                }

            }
            return item;
        }

        public static DependencyObject? GetLogicalParent(DependencyObject? obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = LogicalTreeHelper.GetParent(item);
                if (item?.GetType() == type)
                {
                    lev--;
                }

            }
            return item;
        }

        public static DependencyObject? GetParent(DependencyObject? obj, int level, string typeName)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = VisualTreeHelper.GetParent(item);
                if (item?.GetType()?.Name == typeName)
                {
                    lev--;
                }
            }
            return item;
        }

        public static DependencyObject? GetLogicalParent(DependencyObject? obj, int level, string typeName)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = LogicalTreeHelper.GetParent(item);
                if (item?.GetType()?.Name == typeName)
                {
                    lev--;
                }
            }
            return item;
        }

        private static bool ContainsObject(DependencyObject? child, DependencyObject? obj) 
        {
            if (child == null)
                return false;

            LocalValueEnumerator localValueEnumerator = child.GetLocalValueEnumerator();

            while (localValueEnumerator.MoveNext())
            {
                var value = localValueEnumerator.Current.Value;
                if (ReferenceEquals(value, obj))
                    return true;
            }
            return false;
        }

        public static DependencyObject? FindParentByValue(DependencyObject? topParent, DependencyObject? ch)
        {
            if (topParent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(topParent); i++)
            {
                var child = VisualTreeHelper.GetChild(topParent, i);

                if (ContainsObject(child, ch))
                {
                    return child;
                }

                var childFound = FindParentByValue(child, ch);
                if (childFound != null)
                {
                    return childFound;
                }
            }

            return null;
        }

        public static DependencyObject? FindChildByName(DependencyObject? parent, string childName)
        {
            if (parent == null) 
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
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
