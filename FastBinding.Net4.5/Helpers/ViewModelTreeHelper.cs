using System;
using System.Collections.Generic;
using System.Linq;

namespace FastBindings.Helpers
{
    public class ViewModelPropertyInfo<T>
    {
        public string Name { get; private set; }
        public T Accessor { get; private set; }

        internal ViewModelPropertyInfo(T accessor, string name)
        {
            Name = name;
            Accessor = accessor;
        }
    }

    public interface IViewModelTreeHelper<T>
    {
        object GetFinalViewModel(object propertyAccessor, string fullPropertyPath);
        object GetFinalViewModelEx(object propertyAccessor, string fullPropertyPath);
        IEnumerable<ViewModelPropertyInfo<T>> CalculateSubscribers(string propertyName, object dataContext);
        object GetViewModelProperty(string propertyName, object dataContext);
        Type GetViewModelPropertyType(string propertyName, object dataContext);
        void SetViewModelProperty(string propertyName, object dataContext, object value);
    }

    internal static class CommonViewModelTreeHelper
    {
        public static bool SetViewModelProperty(string propertyName, object dataContext, object value)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName[propertyName.Length - 1] != MethodMark || dataContext == null)
                return false;

            var acccesor = MembersHelper.GetMethodAccessor(dataContext.GetType());
            acccesor.InvokeMethod(dataContext, propertyName.Substring(0, propertyName.Length - 1), value);
            return true;
        }

        public const string PropertyLevelMark = ".";

        private const char MethodMark = '(';

        public static bool TryCalculateValue(string propertyPath, ref object target)
        {
            object propertyAccessor = target;
            foreach (var propertyName in propertyPath.Split(new[] { PropertyLevelMark }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (propertyAccessor == null)
                {
                    target = null;
                    return false;
                }

                var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
                propertyAccessor = info.GetProperty(propertyAccessor, propertyName);
            }
            target = propertyAccessor;
            return true;
        }

        public static bool TrySetValue(string propertyPath, object target, object value)
        {
            object propertyAccessor = target;
            var properties = propertyPath.Split(new[] { PropertyLevelMark }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < properties.Length; index++)
            {
                if (propertyAccessor == null)
                {
                    target = null;
                    return false;
                }
                var propertyName = properties.ElementAt(index);
                var info = MembersHelper.GetPropertyAccessor(propertyAccessor.GetType());
                if (index == properties.Length - 1)
                {
                    info.SetProperty(propertyAccessor, propertyName, value);
                    return true;
                }
                propertyAccessor = info.GetProperty(propertyAccessor, propertyName);
            }
            return false;
        }
    }
}
