using System.ComponentModel;
using FastBindings.Helpers;

namespace FastBindings.StateManagerObjects
{
    internal class SourceDependencyObjectStateManager : ISourceStateManager
    {
        private readonly WeakReference _sourceObjectRef;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private bool _isSubscribed = false;

        private bool _updatingDependObj = false;
        private Lazy<PropertyInfoResult> _lazyPropertyInfoResult;

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceDependencyObjectStateManager(BindableProperty sourceProperty, BindableObject targetObject)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);
            
            _sourceObjectRef = new WeakReference(targetObject);
            _lazyPropertyInfoResult = new Lazy<PropertyInfoResult>(GetPropertyInfo, false);
        }

        private PropertyInfoResult GetPropertyInfo()
        {
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;
            if (source == null || property == null)
            {
                return new PropertyInfoResult();
            }

            return PropertyUtility.BildPropertyInfo(source, property.PropertyName);
        }

        public object? GetSourceProperty(object? dataContext)
        {
            if (!_lazyPropertyInfoResult.Value.IsValid ||
                 !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return null;
            }
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;
            if (source == null || property == null)
            {
                return null;
            }
            return source.GetValue(property);
        }
        public object? GetSourceProperty(object? dataContext, bool isWrapException)
        {
            if (!_lazyPropertyInfoResult.Value.IsValid ||
                 !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return null;
            }
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;
            if (source == null || property == null)
            {
                return property?.DefaultValue;
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
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;

            if (source == null || property == null || property.ReturnType != value?.GetType())
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
            if (dataContext == null || _isSubscribed || !_lazyPropertyInfoResult.Value.IsValid
                || !_lazyPropertyInfoResult.Value.HasGetter)
            {
                return;
            }
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;
            if (source == null || property == null)
            {
                return;
            }
            _isSubscribed = true;

            source.PropertyChanged += OnDependencyPropertyChanged;
        }

        public void Unsubscribe()
        {
        }

        private void OnDependencyPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_updatingDependObj)
            {
                return;
            }
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;
            if (source == null || property == null || e.PropertyName != property.PropertyName)
            {
                return;
            }
            _propertyUpdatedPublisher.RaiseEvent(this, source.GetValue(property));
        }
    }
}
