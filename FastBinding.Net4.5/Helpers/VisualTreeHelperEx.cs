using System;
using System.Windows;
using System.Windows.Media;

namespace FastBindings.Helpers
{
    internal class VisualTreeHelperEx
    {
        public static bool IsAvailable(DependencyObject obj) =>
            VisualTreeHelper.GetParent(obj) != null ||  LogicalTreeHelper.GetParent(obj) != null;

        public static T GetParent<T>(DependencyObject obj) where T : DependencyObject
        {
            return VisualTreeHelper.GetParent(obj) as T;
        }

        public static DependencyObject GetParent(DependencyObject obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = VisualTreeHelper.GetParent(item);
                if (item?.GetType() == type)
                    lev--;

            }
            return item;
        }

        public static DependencyObject GetLogicalParent(DependencyObject obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = LogicalTreeHelper.GetParent(item);
                if (item?.GetType() == type)
                    lev--;
            }
            return item;
        }

        public static DependencyObject GetParent(DependencyObject obj, int level, string typeName)
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
        public static DependencyObject GetLogicalParent(DependencyObject obj, int level, string typeName)
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


        public static DependencyObject FindChildByName(DependencyObject parent, string childName)
        {
            if (parent == null) 
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i) as DependencyObject;
                if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                    return child;

                // Recursively search in the child
                var childFound = FindChildByName(child, childName);
                if (childFound != null)
                    return childFound;
            }
            return null;
        }
    }
}
