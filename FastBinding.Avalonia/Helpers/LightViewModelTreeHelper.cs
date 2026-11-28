using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FastBindings.Helpers
{
    internal class LightViewModelTreeHelper : IViewModelTreeHelper<object>
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public object? GetFinalViewModel(object? propertyAccessor, string fullPropertyPath)
        {
            if (!TryOwnerOfLast(propertyAccessor, fullPropertyPath, out var owner, out var name) || owner == null)
                return null;   // a null object on the path: no value, as a platform binding has none
            return MembersHelper.GetPropertyAccessor(owner.GetType()).GetProperty(owner, name);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public object? GetFinalViewModelEx(object? propertyAccessor, string propertyPath)
        {
            if (propertyAccessor == null)
                return null;

            var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
            return info.GetProperty(propertyAccessor, propertyPath);
        }

        /// <summary>
        /// The object the path's last name is read from, and that name, the names before it read one after the other with nothing
        /// allocated — what <see cref="GetPropertyInfos"/>'s last entry holds; false where it has none (no object to start from).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static bool TryOwnerOfLast(object? propertyAccessor, string propertyPath, out object? owner, out string name)
        {
            owner = null;
            name = string.Empty;
            if (propertyAccessor == null)
                return false;
            var names = CommonViewModelTreeHelper.Segments(propertyPath);
            if (names.Length == 0)
                return false;
            for (int index = 0; index < names.Length - 1 && propertyAccessor != null; index++)
                propertyAccessor = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType()).GetProperty(propertyAccessor, names[index]);
            owner = propertyAccessor;
            name = names[names.Length - 1];
            return true;
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
            if (!TryOwnerOfLast(dataContext, propertyName, out var owner, out var name) || owner == null)
                return null;   // a null object on the path: no value, as a platform binding has none
            return MembersHelper.GetPropertyAccessor(owner.GetType()).GetProperty(owner, name);
        }

        public void SetViewModelProperty(string propertyName, object? dataContext, object? value)
        {
            if (!TryOwnerOfLast(dataContext, propertyName, out var owner, out var name) || owner == null)
                return;   // a null object on the path: nothing to write into
            if (!CommonViewModelTreeHelper.SetViewModelProperty(name, owner, value))
                MembersHelper.GetPropertyAccessor(owner.GetType()).SetProperty(owner, name, value);
        }

        public Type? GetViewModelPropertyType(string propertyName, object? dataContext)
        {
            if (!TryOwnerOfLast(dataContext, propertyName, out var owner, out var name) || owner == null)
                return null;   // a null object on the path: no value, as a platform binding has none
            return MembersHelper.GetPropertyAccessor(owner.GetType()).GetPropertyGetterType(name);
        }
    }
}
