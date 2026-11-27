using Avalonia;
using FastBindings.Diagnostics;
using FastBindings.Helpers;
using System;
using System.Reactive.Linq;

namespace FastBindings.StateManagerObjects
{
    internal class SourceDependencyObjectStateManager : ISourceStateManager, IListenable
    {
        private readonly WeakReference _sourceObjectRef;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private readonly WeakReference _subscriberRef = new WeakReference(null);
        private string? _optional;
        private bool _updatingDependObj = false;
        private Lazy<PropertyInfoResult> _lazyPropertyInfoResult;

        public void Listen(object target, Action<object, object, object?> open) => _propertyUpdatedPublisher.Listen(target, open);

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceDependencyObjectStateManager(AvaloniaProperty sourceProperty, AvaloniaObject targetObject, string? optional)
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
            var source = _sourceObjectRef.Target as AvaloniaObject;
            var property = _sourcePropertyRef.Target as AvaloniaProperty;
            if (source == null || property == null)
            {
                return new PropertyInfoResult();
            }

            return ReflectionUtility.BildPropertyInfo(source, property.Name);
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
                var result = source.GetValue(property);
                return (string.IsNullOrEmpty(_optional) ||
                    CommonViewModelTreeHelper.TryCalculateValue(_optional, ref result)) ? result : default;
            }, isWrapException, StateManagerFactory.ErrorMessage, binding: Description);
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

            if (source == null || property == null)
                return;
            if (!ReflectionUtility.IsValidType(value, property.PropertyType) && !ReflectionUtility.TryConvert(property.PropertyType, ref value))
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.WriteBackTypeMismatch, detail: $"{source.GetType().Name}.{property.Name} is {property.PropertyType.Name}, the value is {value?.GetType().Name ?? "null"}",
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
            }, true, $"writing back to {source.GetType().Name}.{property.Name}", BindingIssueKind.SetterThrew, Description);
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
                using var scope = BindingLog.Enter(Description, source);
                var ans = ExceptionUtility.Handle(() =>
                {
                    var result = source.GetValue(property!);
                    return string.IsNullOrEmpty(_optional) ||
                               CommonViewModelTreeHelper.TryCalculateValue(_optional, ref result) ? result : default;
                }, true, StateManagerFactory.ErrorMessage, binding: Description);
                _propertyUpdatedPublisher.RaiseEvent(this, ans);
            }
        }
    }
}
