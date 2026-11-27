using FastBindings.Diagnostics;
using FastBindings.Helpers;
using System.ComponentModel;

namespace FastBindings.StateManagerObjects
{
    internal class SourceDependencyObjectStateManager : ISourceStateManager, IListenable
    {
        private string? _optional;
        private readonly WeakReference _sourceObjectRef;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private bool _isSubscribed = false;

        private bool _updatingDependObj = false;
        private Lazy<PropertyInfoResult> _lazyPropertyInfoResult;

        public void Listen(object target, Action<object, object, object?> open) => _propertyUpdatedPublisher.Listen(target, open);

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceDependencyObjectStateManager(BindableProperty sourceProperty, BindableObject targetObject, string? optional)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);

            _sourceObjectRef = new WeakReference(targetObject);
            _lazyPropertyInfoResult = new Lazy<PropertyInfoResult>(GetPropertyInfo, false);
            _optional = optional;
        }

        /// <summary>The binding this source belongs to (null while no logger is set).</summary>
        internal BindingDescription? Description { get; set; }

        private PropertyInfoResult GetPropertyInfo()
        {
            var source = _sourceObjectRef.Target as BindableObject;
            var property = _sourcePropertyRef.Target as BindableProperty;
            if (source == null || property == null)
            {
                return new PropertyInfoResult();
            }

            return ReflectionUtility.BildPropertyInfo(source, property.PropertyName);
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
                var result = source.GetValue(property!);
                return string.IsNullOrEmpty(_optional) ||
                      CommonViewModelTreeHelper.TryCalculateValue(_optional, ref result) ? result : null;
            }, isWrapException, StateManagerFactory.ErrorMessage, binding: Description);
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

            if (source == null || property == null)
                return;
            if (!ReflectionUtility.IsValidType(value, property.ReturnType))
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.WriteBackTypeMismatch, detail: $"{source.GetType().Name}.{property.PropertyName} is {property.ReturnType.Name}, the value is {value?.GetType().Name ?? "null"}",
                    target: source, value: value, binding: Description);
                return;
            }

            ExceptionUtility.Handle(() =>
            {
                _updatingDependObj = true;
                try
                {
                    if (string.IsNullOrEmpty(_optional))
                    {
                        source.SetValue(property!, value);
                        return true;
                    }
                    return CommonViewModelTreeHelper.TrySetValue(_optional, source.GetValue(property!), value);
                }
                finally
                {
                    _updatingDependObj = false;
                }
            }, true, $"writing back to {source.GetType().Name}.{property.PropertyName}", BindingIssueKind.SetterThrew, Description);
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
            using var scope = BindingLog.Enter(Description, source);
            var ans = ExceptionUtility.Handle(() =>
            {
                var result = source.GetValue(property!);
                return string.IsNullOrEmpty(_optional) ||
                      CommonViewModelTreeHelper.TryCalculateValue(_optional, ref result) ? result : null;
            }, true, StateManagerFactory.ErrorMessage, binding: Description);
            _propertyUpdatedPublisher.RaiseEvent(this, ans);
        }
    }
}
