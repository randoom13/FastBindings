using System;
using System.ComponentModel;
using System.Windows;
using FastBindings.Helpers;

namespace FastBindings.StateManagerObjects
{
    internal class SourceDependencyObjectStateManager : ISourceStateManager
    {
        private readonly WeakReference _sourceObjectRef;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private bool _isSubscribed;

        private bool _updatingDependObj = false;
        private Lazy<PropertyInfoResult> _lazyPropertyInfoResult;

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceDependencyObjectStateManager(DependencyProperty sourceProperty, DependencyObject targetObject)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);
            _sourceObjectRef = new WeakReference(targetObject);
            _lazyPropertyInfoResult = new Lazy<PropertyInfoResult>(GetPropertyInfo, false);
        }

        private PropertyInfoResult GetPropertyInfo()
        {
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source == null || property == null)
            {
                return new PropertyInfoResult();
            }

            return PropertyUtility.BildPropertyInfo(source, property.Name);
        }

        public object? GetSourceProperty(object? dataContext, bool isWrapException)
        {
            if (!_lazyPropertyInfoResult.Value.IsValid ||
                 !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return DependencyProperty.UnsetValue;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source == null || property == null)
            {
                return DependencyProperty.UnsetValue;
            }
            return ExceptionUtility.Handle(() =>
            {
                _updatingDependObj = true;
                try
                {
                    return source.GetValue(property);
                }
                finally
                {
                    _updatingDependObj = false;
                }
            }, isWrapException, StateManagerFactory.ErrorMessage);
        }


        public void SetSourceProperty(object? value)
        {
            if (!_lazyPropertyInfoResult.Value.IsValid ||
                 !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;

            if (source == null || property == null || property.PropertyType != value?.GetType())
            {
                return;
            }

            _updatingDependObj = true;
            try
            {
                source.SetValue(property, value);
            }
            finally
            {
                _updatingDependObj = false;
            }
        }

        public void Subscribe(object? dataContext)
        {
            if (!_lazyPropertyInfoResult.Value.IsValid
                || !_lazyPropertyInfoResult.Value.HasGetter || dataContext == null || _isSubscribed)
            {
                return;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source == null || property == null)
            {
                return;
            }
            _isSubscribed = true;
            DependencyPropertyDescriptor.FromProperty(property, source.GetType())
     .AddValueChanged(source, OnDependencyPropertyChanged);
        }

        public void Unsubscribe()
        {
        }

        private void OnDependencyPropertyChanged(object? sender, EventArgs e)
        {
            if (_updatingDependObj)
            {
                return;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source != null && property != null)
            {
                _propertyUpdatedPublisher.RaiseEvent(this, source.GetValue(property));
            }
        }
    }
}
