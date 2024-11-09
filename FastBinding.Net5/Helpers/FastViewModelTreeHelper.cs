using FastBindings.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FastBindings.Helpers
{
    internal class FastViewModelTreeHelper : IViewModelTreeHelper<IPropertyAccessor>
    {
        private const string PropertyLevelMark = ".";

        public object? GetFinalViewModel(object? propertyAccessor, string fullPropertyPath)
        {
            var item = GetPropertyInfos(propertyAccessor, fullPropertyPath).LastOrDefault();
            if (item == null)
            {
                return null;
            }
            return item.Accessor?.GetProperty(item.Name);
        }

        public object? GetFinalViewModelEx(object? propertyAccessor, string propertyPath)
        {
            return (propertyAccessor as IPropertyAccessor)?.GetProperty(propertyPath);
        }

        private static IEnumerable<ViewModelPropertyInfo<IPropertyAccessor>> GetPropertyInfos(object? dataContext, string propertyPath)
        {
            var propertyAccessor = dataContext as IPropertyAccessor;
            if (propertyAccessor == null)
                yield break;

            var splittenPropertyPath = propertyPath.Split(new[] { PropertyLevelMark }, StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index < splittenPropertyPath.Length; index++)
            {
                string propertyName = splittenPropertyPath.ElementAt(index);
                yield return new ViewModelPropertyInfo<IPropertyAccessor>(propertyAccessor!, propertyName);
                if (index != splittenPropertyPath.Length - 1)
                    propertyAccessor = propertyAccessor?.GetProperty(propertyName) as IPropertyAccessor;
            }
        }

        public IEnumerable<ViewModelPropertyInfo<IPropertyAccessor>> CalculateSubscribers(string propertyName, object? dataContext)
        {
            return GetPropertyInfos(dataContext as IPropertyAccessor, propertyName);
        }

        public object? GetViewModelProperty(string propertyName, object? dataContext)
        {
            var item = GetPropertyInfos(dataContext, propertyName).LastOrDefault();
            if (item == null)
            {
                return null;
            }
            return item?.Accessor?.GetProperty(item.Name);
        }

        public void SetViewModelProperty(string propertyName, object? dataContext, object? value)
        {
            var item = GetPropertyInfos(dataContext, propertyName).LastOrDefault();
            if (item != null)
            {
                if (!CommonViewModelTreeHelper.SetViewModelProperty(item.Name, item.Accessor, value))
                  item.Accessor?.SetProperty(item.Name, value);
            }
        }

        public Type? GetViewModelPropertyType(string propertyName, object? dataContext)
        {
            var item = GetPropertyInfos(dataContext, propertyName).LastOrDefault();
            if (item == null)
                return null;

            var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
            return info.GetPropertyGetterType(item.Name);
        }
    }


}
