using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FastBindings.BindingManagers
{
    internal class BindingUpdateManager<T>
    {
        public BindingUpdateManager(IViewModelTreeHelper<T> treeHelper)
        {
            _treeHelper = treeHelper;
            _convertersProxy = new ConvertersProxy<T>(_treeHelper);
            _notificationProxy = new NotificationProxy<INotificationFilter, T>(_treeHelper);
        }
        private readonly IViewModelTreeHelper<T> _treeHelper;

        private ISourceStateManager[] _sourceStateManagers = new ISourceStateManager[0];
        private readonly ConvertersProxy<T> _convertersProxy;
        private readonly NotificationProxy<INotificationFilter, T> _notificationProxy;
        private readonly WeakReference _targetPropertyRef = new WeakReference(null);
        private readonly WeakReference _targetObjectRef = new WeakReference(null);

        public string Sources { get; set; } = string.Empty;

        public string? NotificationPath
        {
            get => _notificationProxy.NotificationPath;
            set => _notificationProxy.NotificationPath = value;
        }

        public string? NotificationName
        {
            get => _notificationProxy.NotificationName;
            set => _notificationProxy.NotificationName = value;
        }

        public INotificationFilter? Notification
        {
            get => _notificationProxy.Notification;
            set => _notificationProxy.Notification = value;
        }

        public string? DataContextSource
        {
            get => _dataContextParams.DataContextSource;
            set => _dataContextParams.DataContextSource = value;
        }

        public string? ConverterPath
        {
            get => _convertersProxy.ConverterPath;
            set => _convertersProxy.ConverterPath = value;
        }

        public string? ConverterName
        {
            get => _convertersProxy.ConverterName;
            set => _convertersProxy.ConverterName = value;
        }

        public IValueConverterBase? Converter
        {
            get => _convertersProxy.Converter;
            set => _convertersProxy.Converter = value;
        }

        public object? TargetNullValue { get; set; }
        public object? FallBackValue { get; set; }

        private bool _updatingTarget = false;
        private bool _updatingSource = false;

        public BindingMode Mode { get; set; }

        private DataContextParams _dataContextParams = new DataContextParams();

        public void ApplyTargets(AvaloniaObject targetObject, AvaloniaProperty targetProperty, AvaloniaObject? dataContextObj)
        {
            _targetObjectRef.Target = targetObject;
            _targetPropertyRef.Target = targetProperty;
            _dataContextParams.AnchorObject = dataContextObj;
        }

        public void Initialize(CacheStrategy cacheStrategy)
        {
            var targetObject = _targetObjectRef.Target as AvaloniaObject;
            var targetProperty = _targetPropertyRef.Target as AvaloniaProperty;
            if (targetObject == null || targetProperty == null)
            {
                return;
            }

            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper);
            if (!_sourceStateManagers.Any())
            {
                System.Diagnostics.Debug.Write("[FastBinding] Could not create managers");
                return;
            }
            var initialTarget = targetObject;

            if (CanTrackTargetChanges && ReflectionUtility.BildPropertyInfo(targetObject, targetProperty.Name).HasGetter)
            {

                initialTarget.GetObservable(targetProperty).Subscribe(OnDependencyPropertyChanged);
            }

            targetObject = _dataContextParams.GetDataContextTarget(targetObject);
            var dataContext = _dataContextParams.GetDataContext(targetObject);
            var frameworkElement = targetObject as Control;

            var applyOnTimeMode = false;
            var isOneTimeOnly = Mode == BindingMode.OneTime;

            if (frameworkElement != null)
            {
                if (isOneTimeOnly && dataContext != null)
                {
                    applyOnTimeMode = true;
                }
                else frameworkElement.DataContextChanged += OnDataContextChanged;
            }
            else
            {
                UpdateTarget(initialTarget, targetProperty, TargetNullValue ?? AvaloniaProperty.UnsetValue);
                return;
            }

            if (!applyOnTimeMode && !CanTrackOnlyTargetChanges)
            {
                foreach (var stateManager in _sourceStateManagers)
                {
                    stateManager.PropertyUpdated += OnViewModelValueUpdated;
                    stateManager.Subscribe(dataContext);
                }
            }
            if (dataContext == null)
            {
                UpdateTarget(initialTarget, targetProperty, TargetNullValue ?? AvaloniaProperty.UnsetValue);
                return;
            }

            var converterFunc = _convertersProxy.FindForwardConverter(dataContext);
            object? value;
            if (converterFunc == null)
            {
                value = _sourceStateManagers.First().GetSourceProperty(dataContext, true);
            }
            else
            {
                try
                {
                    var values = _sourceStateManagers.Select(manager => manager.GetSourceProperty(dataContext, false)).ToArray();
                    var args = new ConverterArgs(values, targetProperty.PropertyType);
                    value = converterFunc(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred on binding to {initialTarget}.{targetProperty} on " +
                        $"converter {_convertersProxy.ConverterName ?? "None"} during intialization");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(value))
                UpdateTarget(initialTarget, targetProperty, value);
        }


        private object? CalculateValue(object? val)
        {
            if (!(val is ExceptionHolder))
            {
                return val ?? TargetNullValue;
            }
            return FallBackValue ?? val;
        }

        private void OnViewModelValueUpdated(object? sender, object? value)
        {
            if (_updatingSource)
            {
                return;
            }
            var target = _targetObjectRef?.Target as AvaloniaObject;
            var property = _targetPropertyRef?.Target as AvaloniaProperty;
            if (target == null || property == null)
            {
                return;
            }
            var dispatcher = Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
            {
                OnViewModelValueUpdated(target, property, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdated(target, property, sender, value);
                dispatcher.Invoke(action);
            }
        }

        private void OnViewModelValueUpdated(AvaloniaObject target, AvaloniaProperty property, object? sender, object? value)
        {
            var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
            var dataContext = _dataContextParams.GetDataContext(dataContextTarget);
            var converterFunc = _convertersProxy.FindForwardConverter(dataContext);
            if (converterFunc == null)
            {
                if (!ReferenceEquals(sender, _sourceStateManagers.FirstOrDefault()))
                    return;
            }
            else
            {
                if (value is ExceptionHolder)
                    value = (value as ExceptionHolder)?.Exception;

                var values = _sourceStateManagers.Select(man => ReferenceEquals(sender, man) ?
                value : man.GetSourceProperty(dataContext, false)).ToArray();
                var args = new ConverterArgs(values, property.PropertyType);
                try
                {
                    value = converterFunc(args) ?? TargetNullValue;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to prepare data for update {target}.{property}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }

            value = CalculateValue(value);
            if (!CanUpdateTarget(property, value))
            {
                return;
            }
            try
            {
                var ars = NotificationArgs.CreateFromSource(sender, value);
                if (NeedNotify(dataContext, ars))
                    UpdateTarget(target, property, value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update {target}.{property}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }


        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            var target = _targetObjectRef?.Target as AvaloniaObject;
            var property = _targetPropertyRef?.Target as AvaloniaProperty;
            if (target == null || property == null)
            {
                return;
            }

            var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
            foreach (var manager in _sourceStateManagers)
            {
                manager.Unsubscribe();
            }
            if (!ReferenceEquals(sender, dataContextTarget) || Mode == BindingMode.OneTime)
            {
                var element = sender as Control;
                if (element != null)
                {
                    element.DataContextChanged -= OnDataContextChanged;
                }
                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            var dataContext = dataContextTarget.GetValue(StyledElement.DataContextProperty);
            var converterFunc = _convertersProxy.FindForwardConverter(dataContext);
            ConverterArgs args = new ConverterArgs(new object[_sourceStateManagers.Length], property.PropertyType);
            var hasConverter = converterFunc != null;
            var canTrack = CanTrackViewModelChanges;
            if (dataContext != null)
            {
                for (int index = 0; index < _sourceStateManagers.Length; index++)
                {
                    var manager = _sourceStateManagers[index];
                    if (canTrack)
                    {
                        manager.Subscribe(dataContext);
                    }
                    if (hasConverter)
                    {
                        args.Values[index] = manager.GetSourceProperty(dataContext, false);
                    }
                    else if (index == 0)
                        args.Values[index] = manager.GetSourceProperty(dataContext, true);
                }
            }
            object? value;
            if (converterFunc == null)
            {
                value = args.Values.FirstOrDefault();
            }
            else
            {
                try
                {
                    value = converterFunc(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to calculate value in converter for {target}.{property}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(property, value))
            {
                var ars = NotificationArgs.CreateFromTarget(sender, value);
                try
                {
                    if (NeedNotify(dataContext, ars))
                        UpdateTarget(target, property, value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update for {target}.{property}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                }
            }
        }

        private bool CanTrackViewModelChanges =>
            Mode == BindingMode.Default || Mode == BindingMode.OneWay
            || Mode == BindingMode.TwoWay;

        private bool CanTrackTargetChanges =>
            CanTrackOnlyTargetChanges || Mode == BindingMode.TwoWay;

        private bool CanTrackOnlyTargetChanges =>
            Mode == BindingMode.OneWayToSource;

        private static bool CanUpdateTarget(object? value) => !(value is ExceptionHolder);

        private static bool CanUpdateTarget(AvaloniaProperty property, object? value)
            => CanUpdateTarget(value) && ReflectionUtility.IsValidType(value, property.PropertyType);

        private void UpdateTarget(AvaloniaObject target, AvaloniaProperty property, object? value)
        {
            _updatingTarget = true;
            try
            {
                target.SetValue(property, value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update for {target}.{property}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
            finally
            {
                _updatingTarget = false;
            }
        }

        private void OnDependencyPropertyChanged(object? newValue)
        {
            if (_updatingTarget)
            {
                return;
            }
            var target = _targetObjectRef?.Target as AvaloniaObject;
            var property = _targetPropertyRef?.Target as AvaloniaProperty;
            if (target == null || property == null)
            {
                return;
            }
            try
            {
                var dataContext = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                var backConverterFunc = _convertersProxy.FindBackConverter(dataContext);
                var targetValue = target.GetValue(property);
                var val = targetValue;
                if (backConverterFunc != null)
                {
                    var args = new ConverterBackArgs()
                    {
                        Value = targetValue
                    };
                    val = backConverterFunc(args);
                }
                var ars = NotificationArgs.CreateFromTarget(target, val);
                if (NeedNotify(dataContext, ars))
                    UpdateSource(val);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update for {target}.{property} during dependency property changing");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }

        private bool NeedNotify(object? dataContext, NotificationArgs args)
        {
            args.Name = _notificationProxy.NotificationName;
            _notificationProxy.FindNotification(dataContext)?.Notify(args);
            return !args.Handled;
        }

        private void UpdateSource(params object?[] values)
        {
            _updatingSource = true;
            for (int index = 0; index < _sourceStateManagers.Length; index++)
            {
                var manager = _sourceStateManagers[index];
                try
                {
                    manager.SetSourceProperty(values.ElementAtOrDefault(index));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to set source property");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                }
                finally
                {
                    _updatingSource = false;
                }
            }
        }
    }
}
