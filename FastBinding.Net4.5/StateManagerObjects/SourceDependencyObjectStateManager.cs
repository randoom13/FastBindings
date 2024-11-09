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
        private readonly string _optionalPath;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private bool _isSubscribed;

        private bool _updatingDependObj = false;
        private Lazy<PropertyInfoResult> _lazyPropertyInfoResult;

        public event EventHandlerEx<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceDependencyObjectStateManager(DependencyProperty sourceProperty, DependencyObject targetObject, string optional)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);
            _sourceObjectRef = new WeakReference(targetObject);
            _lazyPropertyInfoResult = new Lazy<PropertyInfoResult>(GetPropertyInfo, false);
            _optionalPath = optional;
        }

        private PropertyInfoResult GetPropertyInfo()
        {
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source == null || property == null)
            {
                return new PropertyInfoResult();
            }

            return ReflectionUtility.BildPropertyInfo(source, property.Name);
        }

        public object GetSourceProperty(object dataContext, bool isWrapException)
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
                var result = source.GetValue(property);
                return string.IsNullOrEmpty(_optionalPath) ||
                      CommonViewModelTreeHelper.TryCalculateValue(_optionalPath, ref result) ? result : null;
            }, isWrapException, StateManagerFactory.ErrorMessage);
        }


        public void SetSourceProperty(object value)
        {
            if (!_lazyPropertyInfoResult.Value.IsValid ||
                 !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;

            if (source == null || property == null || !ReflectionUtility.IsValidType(value, property.PropertyType))
            {
                return;
            }

            ExceptionUtility.Handle(() =>
            {
                _updatingDependObj = true;
                try
                {
                    if (string.IsNullOrEmpty(_optionalPath))
                    {
                        source.SetValue(property, value);
                        return true;
                    }
                    return CommonViewModelTreeHelper.TrySetValue(_optionalPath, source.GetValue(property), value);
                }
                finally
                {
                    _updatingDependObj = false;
                }
            }, true, StateManagerFactory.ErrorMessage);
        }

        public void Subscribe(object dataContext)
        {
            if (dataContext == null || _isSubscribed || !_lazyPropertyInfoResult.Value.IsValid
                || !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source == null || property == null)
            {
                return;
            }
            DependencyPropertyDescriptor.FromProperty(property, source.GetType())
   .AddValueChanged(source, OnDependencyPropertyChanged);
            _isSubscribed = true;
        }

        public void Unsubscribe()
        {
        }

        private void OnDependencyPropertyChanged(object sender, EventArgs e)
        {
            if (_updatingDependObj)
            {
                return;
            }
            var source = _sourceObjectRef.Target as DependencyObject;
            var property = _sourcePropertyRef.Target as DependencyProperty;
            if (source == null || property == null)
            {
                return;
            }
            if (source != null && property != null)
            {
                var ans = ExceptionUtility.Handle(() =>
                {
                    var result = source.GetValue(property);
                    return string.IsNullOrEmpty(_optionalPath) ||
                          CommonViewModelTreeHelper.TryCalculateValue(_optionalPath, ref result) ? result : null;
                }, true, StateManagerFactory.ErrorMessage);
                _propertyUpdatedPublisher.RaiseEvent(this, ans);
            }
        }
    }
}
