using System.ComponentModel;
using FastBindings.Interfaces;
using FastBindings.Helpers;

namespace FastBindings.Common
{
    public abstract class BaseViewModel : INotifyPropertyChanged, IPropertyAccessor
    {
        public abstract object? GetProperty(string propertyName);
        public abstract void SetProperty(string propertyName, object? value);
        // This event is part of the INotifyPropertyChanged interface
        public event PropertyChangedEventHandler? PropertyChanged;

        // Method to raise the PropertyChanged event
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PropertyHolder<T> : IPropertyAccessor
    {
        public object? GetProperty(string propertyName)
        {
            _propertyAccessor = _propertyAccessor ?? AccessorHelper.GetPropertyAccessor<T>();
            return _propertyAccessor.GetProperty(this, propertyName);
        }

        public void SetProperty(string propertyName, object? value)
        {
            _propertyAccessor = _propertyAccessor ?? AccessorHelper.GetPropertyAccessor<T>();
            _propertyAccessor.SetProperty(this, propertyName, value);
        }

        private PropertyAccessor<T>? _propertyAccessor;
    }

    public abstract class CommonBaseViewModel<T> : BaseViewModel
    {
        public override object? GetProperty(string propertyName)
        {
            _propertyAccessor = _propertyAccessor ?? AccessorHelper.GetPropertyAccessor<T>();
            return _propertyAccessor.GetProperty(this, propertyName);
        }

        public override void SetProperty(string propertyName, object? value)
        {
            _propertyAccessor = _propertyAccessor ?? AccessorHelper.GetPropertyAccessor<T>();
            _propertyAccessor.SetProperty(this, propertyName, value);
        }

       private PropertyAccessor<T>? _propertyAccessor;
    }
}
