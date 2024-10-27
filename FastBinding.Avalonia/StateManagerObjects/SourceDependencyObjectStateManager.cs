using Avalonia;
using FastBindings.Helpers;
using System;
using System.Reactive.Linq;

namespace FastBindings.StateManagerObjects
{
    internal class SourceDependencyObjectStateManager : ISourceStateManager
    {
        private readonly WeakReference _sourceObjectRef;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private readonly WeakReference _subscriberRef = new WeakReference(null);

        private bool _updatingDependObj = false;
        private Lazy<PropertyInfoResult> _lazyPropertyInfoResult;

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceDependencyObjectStateManager(AvaloniaProperty sourceProperty, AvaloniaObject targetObject)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);
            _sourceObjectRef = new WeakReference(targetObject);
            _lazyPropertyInfoResult = new Lazy<PropertyInfoResult>(GetPropertyInfo, false);
        }

        private PropertyInfoResult GetPropertyInfo()
        {
            var source = _sourceObjectRef.Target as AvaloniaObject;
            var property = _sourcePropertyRef.Target as AvaloniaProperty;
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
                return AvaloniaProperty.UnsetValue;
            }
            var source = _sourceObjectRef.Target as AvaloniaObject;
            var property = _sourcePropertyRef.Target as AvaloniaProperty;
            if (source == null || property == null)
            {
                return AvaloniaProperty.UnsetValue;
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
            var source = _sourceObjectRef.Target as AvaloniaObject;
            var property = _sourcePropertyRef.Target as AvaloniaProperty;

            if (source == null || property == null || property.PropertyType != value?.GetType())
            {
                return;
            }

            _updatingDependObj = true;
            try
            {
                source.SetValue(property!, value);
            }
            finally
            {
                _updatingDependObj = false;
            }
        }

        public void Subscribe(object? dataContext)
        {
            if (dataContext == null || _subscriberRef.IsAlive || !_lazyPropertyInfoResult.Value.IsValid
                || !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return;
            }
            var source = _sourceObjectRef.Target as AvaloniaObject;
            var property = _sourcePropertyRef.Target as AvaloniaProperty;
            if (source != null && property != null)
                _subscriberRef.Target = source.GetObservable(property).Subscribe(OnDependencyPropertyChanged);
        }

        public void Unsubscribe()
        {
        }

        private void OnDependencyPropertyChanged(object? sender)
        {
            if (_updatingDependObj)
            {
                return;
            }
            var source = _sourceObjectRef.Target as AvaloniaObject;
            var property = _sourcePropertyRef.Target as AvaloniaProperty;
            if (source != null && property != null)
            {
                _propertyUpdatedPublisher.RaiseEvent(this, source.GetValue(property));
            }
        }
    }
}
