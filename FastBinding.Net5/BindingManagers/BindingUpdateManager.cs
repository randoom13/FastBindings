﻿using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

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

        public bool CanSubscribeOnDataContext(DependencyObject targetObject) 
        {
            var targetContextObject = _dataContextParams.GetDataContextTarget(targetObject);
            return VisualTreeHelperEx.CanSubscribeOnDataContext(targetContextObject);
        }

        public object? GetDefaultValue(DependencyProperty targetProperty) 
        {
            return TargetNullValue ?? targetProperty.DefaultMetadata.DefaultValue;
        }

        public void InitializeAndRefresh(DependencyObject targetObject, DependencyProperty targetProperty, CacheStrategy cacheStrategy,
            DependencyObject? dataContextObj)
        {
            _dataContextParams.AnchorObject = dataContextObj;
            var value = Initialize(targetObject, targetProperty,  cacheStrategy);
            value = CalculateValue(value);
            if (CanUpdateTarget(targetProperty, value))
            {
                var ars = NotificationArgs.CreateFromSource(targetProperty, value);
                UpdateTarget(targetObject, targetProperty, value);
            }
        }

        public object? Initialize(DependencyObject targetObject, DependencyProperty targetProperty, CacheStrategy cacheStrategy)
        {
            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper);
            if (!_sourceStateManagers.Any())
            {
                return DependencyProperty.UnsetValue;
            }
            _targetObjectRef.Target = targetObject;
            _targetPropertyRef.Target = targetProperty;
            var initialTarget = targetObject;

            if (CanTrackTargetChanges && ReflectionUtility.BildPropertyInfo(targetObject, targetProperty.Name).HasGetter)
            {
                DependencyPropertyDescriptor.FromProperty(targetProperty, initialTarget.GetType())
                .AddValueChanged(initialTarget, OnDependencyPropertyChanged);
            }

            var targetContextObject = _dataContextParams.GetDataContextTarget(targetObject);
            var dataContext = _dataContextParams.GetDataContext(targetContextObject);
            var applyOnTimeMode = false;
            var isOneTimeOnly = Mode == BindingMode.OneTime;
            var applyValue = true;
            if (targetContextObject != null)
            {
                if (isOneTimeOnly && dataContext != null)
                {
                    applyValue = false;
                    applyOnTimeMode = true;
                }
                else
                {
                    applyValue = !VisualTreeHelperEx.SubscribeOnDataContext(targetContextObject,
                    OnDataContextChanged);
                }
            }

            if (applyValue)
            {
                return TargetNullValue ?? targetProperty.DefaultMetadata.DefaultValue;
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
                return  TargetNullValue ?? targetProperty.DefaultMetadata.DefaultValue;
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
                    var values = _sourceStateManagers.Select(manager => manager.GetSourceProperty(dataContext, true)).ToArray();
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
            return CanUpdateTarget(value) ? value : (targetProperty.DefaultMetadata.DefaultValue ?? DependencyProperty.UnsetValue);
        }

        private object? CalculateValue(object? val)
        {
            if (!CanUpdateTarget(val))
                return val ?? TargetNullValue;

            return FallBackValue ?? val;
        }

        private void OnViewModelValueUpdated(object? sender, object value)
        {
            if (_updatingSource)
            {
                return;
            }
            var target = _targetObjectRef.Target as DependencyObject;
            var property = _targetPropertyRef.Target as DependencyProperty;
            if (target == null || property == null)
            {
                return;
            }
            var dispatcher = target.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                OnViewModelValueUpdated(target, property, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdated(target, property, sender, value);
                dispatcher.Invoke(DispatcherPriority.Render, action);
            }
        }

        private void OnViewModelValueUpdated(DependencyObject target, DependencyProperty property, object? sender, object? value)
        {
            var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
            var dataContext = _dataContextParams.GetDataContext(dataContextTarget);

            var converterFunc = _convertersProxy.FindForwardConverter(dataContext);
            if (converterFunc == null)
            {
                if (!ReferenceEquals(sender, _sourceStateManagers.First()))
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
            if (CanUpdateTarget(property, value))
            {
                var ars = NotificationArgs.CreateFromSource(sender, value);
                try
                {
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
        }


        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var target = _targetObjectRef.Target as DependencyObject;
            var property = _targetPropertyRef.Target as DependencyProperty;
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
                VisualTreeHelperEx.UnSubscribeOnDataContext(sender as DependencyObject, OnDataContextChanged);
                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            var converterFunc = _convertersProxy.FindForwardConverter(e.NewValue);
            ConverterArgs args = new ConverterArgs(new object[_sourceStateManagers.Length], property.PropertyType);
            var hasConverter = converterFunc != null;
            var canTrack = CanTrackViewModelChanges;
            if (e.NewValue != null)
            {
                for (int index = 0; index < _sourceStateManagers.Length; index++)
                {
                    var manager = _sourceStateManagers[index];
                    if (canTrack)
                    {
                        manager.Subscribe(e.NewValue);
                    }
                    if (hasConverter)
                    {
                        args.Values[index] = manager.GetSourceProperty(e.NewValue, false);
                    }
                    else if (index == 0)
                        args.Values[index] = manager.GetSourceProperty(e.NewValue, true);
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
                    if (NeedNotify(e.NewValue, ars))
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

        private static bool CanUpdateTarget(object? value)
           => !(value is ExceptionHolder);

        private static bool CanUpdateTarget(DependencyProperty property, object? value)
            => CanUpdateTarget(value) && ReflectionUtility.IsValidType(value, property.PropertyType);

        private void UpdateTarget(DependencyObject target, DependencyProperty property, object? value)
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

        private void OnDependencyPropertyChanged(object? sender, EventArgs e)
        {
            if (_updatingTarget)
                return;

            var target = _targetObjectRef.Target as DependencyObject;
            var property = _targetPropertyRef.Target as DependencyProperty;
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
            _notificationProxy.FindNotification(dataContext)?.Notify(args);
            return !args.Handled;
        }

        private void UpdateSource(params object?[] values)
        {
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
