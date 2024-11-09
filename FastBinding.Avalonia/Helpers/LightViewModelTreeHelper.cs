using System;
using System.Collections.Generic;
using System.Linq;

namespace FastBindings.Helpers
{
    internal class LightViewModelTreeHelper : IViewModelTreeHelper<object>
    {
        public object? GetFinalViewModel(object? propertyAccessor, string fullPropertyPath)
        {
            var item = GetPropertyInfos(propertyAccessor, fullPropertyPath).LastOrDefault();
            if (item == null)
                return null;

            var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
            return info.GetProperty(item.Accessor, item.Name);
        }

        public object? GetFinalViewModelEx(object? propertyAccessor, string propertyPath)
        {
            if (propertyAccessor == null)
                return null;

            var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
            return info.GetProperty(propertyAccessor, propertyPath);
        }

        private static IEnumerable<ViewModelPropertyInfo<object>> GetPropertyInfos(object? propertyAccessor, string propertyPath)
        {
            if (propertyAccessor == null)
                yield break;

            var splittenPropertyPath = propertyPath.Split(new[] { CommonViewModelTreeHelper.PropertyLevelMark }, StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index < splittenPropertyPath.Length; index++)
            {
                string propertyName = splittenPropertyPath.ElementAt(index);
                yield return new ViewModelPropertyInfo<object>(propertyAccessor!, propertyName);
                if (propertyAccessor != null && index != splittenPropertyPath.Length - 1)
                {
                    var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
                    propertyAccessor = info.GetProperty(propertyAccessor, propertyName);
                }
            }
        }

        public IEnumerable<ViewModelPropertyInfo<object>> CalculateSubscribers(string propertyName, object? dataContext)
        {
            return GetPropertyInfos(dataContext, propertyName);
        }

        public object? GetViewModelProperty(string propertyName, object? dataContext)
        {
            var item = GetPropertyInfos(dataContext, propertyName).LastOrDefault();
            if (item == null || item.Name == null)
            {
                return null;
            }
            var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
            return info.GetProperty(item.Accessor, item.Name);
        }

        public void SetViewModelProperty(string propertyName, object? dataContext, object? value)
        {
            var propertyAccessor = dataContext;
            if (propertyAccessor == null)
                return;

            var item = GetPropertyInfos(propertyAccessor, propertyName).LastOrDefault();
            if (item != null)
            {
                if (!CommonViewModelTreeHelper.SetViewModelProperty(item.Name, item.Accessor, value))
                {
                    var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
                    info.SetProperty(item.Accessor, item.Name, value);
                }
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
