using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace FastBindings.Helpers
{
    internal class LightViewModelTreeHelper : IViewModelTreeHelper<object>
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public object? GetFinalViewModel(object? propertyAccessor, string fullPropertyPath)
        {
            var item = GetPropertyInfos(propertyAccessor, fullPropertyPath).LastOrDefault();
            if (item == null)
                return null;

            if (item.Accessor == null)   // a null object on the path: no value, as a platform binding has none
                return null;

            var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
            return info.GetProperty(item.Accessor, item.Name);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public object? GetFinalViewModelEx(object? propertyAccessor, string propertyPath)
        {
            if (propertyAccessor == null)
                return null;

            var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
            return info.GetProperty(propertyAccessor, propertyPath);
        }

        private static readonly ViewModelPropertyInfo<object>[] NoInfos = new ViewModelPropertyInfo<object>[0];

        // The same entries the earlier iterator yielded (an entry per name, its accessor null after a null object on the path),
        // built straight into an array: no iterator, no second copy by ToArray, the path split once per path string.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static ViewModelPropertyInfo<object>[] GetPropertyInfos(object? propertyAccessor, string propertyPath)
        {
            if (propertyAccessor == null)
                return NoInfos;

            var names = CommonViewModelTreeHelper.Segments(propertyPath);
            var infos = new ViewModelPropertyInfo<object>[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                string propertyName = names[index];
                infos[index] = new ViewModelPropertyInfo<object>(propertyAccessor!, propertyName);
                if (propertyAccessor != null && index != names.Length - 1)
                {
                    var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
                    propertyAccessor = info.GetProperty(propertyAccessor, propertyName);
                }
            }
            return infos;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public IEnumerable<ViewModelPropertyInfo<object>> CalculateSubscribers(string propertyName, object? dataContext)
        {
            return GetPropertyInfos(dataContext, propertyName);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public object? GetViewModelProperty(string propertyName, object? dataContext)
        {
            var item = GetPropertyInfos(dataContext, propertyName).LastOrDefault();
            if (item == null || item.Name == null)
            {
                return null;
            }
            if (item.Accessor == null)   // a null object on the path: no value, as a platform binding has none
                return null;
            var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
            return info.GetProperty(item.Accessor, item.Name);
        }

        public void SetViewModelProperty(string propertyName, object? dataContext, object? value)
        {
            var propertyAccessor = dataContext;
            if (propertyAccessor == null)
                return;

            var item = GetPropertyInfos(propertyAccessor, propertyName).LastOrDefault();
            if (item?.Accessor != null)   // a null object on the path: nothing to write into
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

            if (item.Accessor == null)   // a null object on the path: no value, as a platform binding has none
                return null;

            var info = MembersHelper.GetPropertyAccessor(item.Accessor.GetType());
            return info.GetPropertyGetterType(item.Name);
        }
    }
}
