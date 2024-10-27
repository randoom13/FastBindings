using System;
using System.Collections.Generic;
using System.Linq;
using FastBindings.Interfaces;

namespace FastBindings.Helpers
{
    public class ViewModelPropertyInfo
    { 
        public string Name { get; private set; }
        public IPropertyAccessor Accessor { get; private set; }

        internal ViewModelPropertyInfo(IPropertyAccessor accessor, string name)
        {
            Name = name;
            Accessor = accessor;
        }
    }

    internal static class ViewModelTreeHelper
    {
        public const string PropertyLevelMark = ".";

        public static bool Contains(IPropertyAccessor propertyAccessor, string fullPropertyPath, object checkingViewModel, string checkingPropertyName) 
        {
            return GetPropertyInfos(propertyAccessor, fullPropertyPath).Any(it => ReferenceEquals(checkingViewModel, it.Accessor)
                && checkingPropertyName == it.Name);
        }

        public static IPropertyAccessor GetFinalViewModel(IPropertyAccessor propertyAccessor, string fullPropertyPath) 
        {
            var item = GetPropertyInfos(propertyAccessor, fullPropertyPath).LastOrDefault();
            return item == null ? null : item.Accessor?.GetProperty(item.Name) as IPropertyAccessor;
        }

        public static IEnumerable<ViewModelPropertyInfo> GetPropertyInfos(IPropertyAccessor propertyAccessor, string propertyPath)
        {
            if (propertyAccessor == null)
                yield break;

            var splittenPropertyPath = propertyPath.Split(new[] { PropertyLevelMark }, StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index < splittenPropertyPath.Length; index++)
            {
                string propertyName = splittenPropertyPath.ElementAt(index);
                yield return new ViewModelPropertyInfo(propertyAccessor, propertyName);
                if (index != splittenPropertyPath.Length - 1)
                    propertyAccessor = propertyAccessor?.GetProperty(propertyName) as IPropertyAccessor;
            }
        }

        public static IEnumerable<ViewModelPropertyInfo> CalculateSubscribers(string propertyName, object dataContext)
        {
            return GetPropertyInfos(dataContext as IPropertyAccessor, propertyName);
        }

        public static object GetViewModelProperty(string propertyName, IPropertyAccessor propertyAccessor)
        {
            if (propertyAccessor == null)
                return null;

            var item = GetPropertyInfos(propertyAccessor, propertyName).LastOrDefault();
            return item == null ? null : item?.Accessor?.GetProperty(item.Name);
        }

        public static void SetViewModelProperty(string propertyName, IPropertyAccessor propertyAccessor, object value)
        {
            if (propertyAccessor == null)
                return;

            var item = GetPropertyInfos(propertyAccessor, propertyName).LastOrDefault();
            if (item != null)
            {
                item.Accessor?.SetProperty(item.Name, value);
            }
        }
    }
}
