namespace FastBindings.Helpers
{
    internal static class VisualTreeHelperEx
    {
        public static bool IsAvailable(BindableObject obj) =>
            (obj as Element)?.Parent != null;

        public static T? GetParent<T>(BindableObject? obj) where T : BindableObject
        {          
            return (obj as Element)?.Parent as T;
        }

        public static BindableObject? GetParent(BindableObject? obj, int level, Type type)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = (item as Element)?.Parent;
                if (item?.GetType() == type)
                {
                    lev--;
                }
            }
            return item;
        }
 
        public static BindableObject? GetParent(BindableObject? obj, int level, string typeName)
        {
            var item = obj;
            var lev = level;
            while (lev > 0 && item != null)
            {
                item = (item as Element)?.Parent;
                if (item?.GetType()?.Name == typeName)
                {
                    lev--;
                }
            }
            return item;
        }

           public static T? FindChildByName<T>(BindableObject? parent, string childName) where T : BindableObject
        {
            if (parent == null) 
                return null;

            // Check if the parent itself has the name we're looking for
            if (parent is T && (parent as Element)?.StyleId == childName)
            {
                return parent as T;
            }

            // If the parent is a layout, check its children
            if (parent is Layout layout)
            {
                foreach (var child in layout.Children)
                {
                    var result = FindChildByName<T>(child as BindableObject, childName);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null; // No matching child found
        }
    }
}
