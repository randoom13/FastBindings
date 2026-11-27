using FastBindings.Diagnostics;
using System;
using System.Linq;
using FastBindings.StateManagerObjects;
using FastBindings.Interfaces;
using FastBindings.Helpers;
using System.Windows;
using System.Windows.Threading;

namespace FastBindings.BindingManagers
{
    internal class OneWayBindingUpdateManager<T>
    {
        public OneWayBindingUpdateManager(IViewModelTreeHelper<T> treeHelper)
        {
            _treeHelper = treeHelper;
            _convertersProxy = new ConvertersProxy<T>(_treeHelper);
            _notificationProxy = new NotificationProxy<INotificationFilter, T>(_treeHelper);
        }
        private readonly TargetInfo _targetInfo = new TargetInfo();
        private readonly IViewModelTreeHelper<T> _treeHelper;
        private ISourceStateManager[] _sourceStateManagers = new ISourceStateManager[0];
        private readonly ConvertersProxy<T> _convertersProxy;
        private WeakReference _targetObjectRef = new(null);
        private readonly NotificationProxy<INotificationFilter, T> _notificationProxy;

        public string Target
        {
            get => _targetInfo.Target;
            set { _targetInfo.Target = value; }
        }
        public string Sources { get; set; } = string.Empty;

        /// <summary>What this binding reports its problems as (null while no logger is set).</summary>
        internal BindingDescription? Description { get; set; }

        private string ConverterDetail() => $"converter {(_convertersProxy.Converter?.GetType().Name ?? _convertersProxy.ConverterPath ?? "")}{(_convertersProxy.ConverterName != null ? " '" + _convertersProxy.ConverterName + "'" : "")}";

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

        private DataContextParams _dataContextParams = new();

        public void Initialize(DependencyObject targetObject, CacheStrategy cacheStrategy)
        {
            using var scope = BindingLog.Enter(Description, targetObject);
            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper, Description);
            if (!_sourceStateManagers.Any())
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.SourcesInvalid, detail: "no source in '" + Sources + "'", target: targetObject, binding: Description);
                return;
            }
            _targetObjectRef.Target = targetObject;

            var initialTarget = targetObject;
            var targetContextObject = _dataContextParams.GetDataContextTarget(targetObject);
            var dataContext = _dataContextParams.GetDataContext(targetContextObject);
            if (!VisualTreeHelperEx.SubscribeOnDataContext(targetContextObject, OnDataContextChanged))
            {
                UpdateTarget(initialTarget, TargetNullValue);
                return;
            }


            foreach (var stateManager in _sourceStateManagers)
            {
                stateManager.ListenTo(this, Updated, OnViewModelValueUpdated);
                stateManager.Subscribe(dataContext);
            }

            if (dataContext == null)
            {
                UpdateTarget(initialTarget, TargetNullValue);
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
                    var values = _sourceStateManagers.Select(manager => manager.GetSourceProperty(dataContext, true)).ToArray();
                    var args = new ConverterArgs(values, null);
                    value = converterFunc(args);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), initialTarget, binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }

            value = CalculateValue(value);
            if (CanUpdateTarget(value))
                UpdateTarget(initialTarget, value);
        }

        // only a failure shows FallBackValue (the test was inverted)
        private object? CalculateValue(object? val)
        {
            if (CanUpdateTarget(val))
                return val ?? TargetNullValue;

            return FallBackValue ?? val;
        }

        // the listener for the source state managers, made once for every binding (see WeakEventPublisher.Listen)
        private static readonly Action<object, object, object?> Updated = (manager, sender, value) => ((OneWayBindingUpdateManager<T>)manager).OnViewModelValueUpdated(sender, value!);

        private void OnViewModelValueUpdated(object? sender, object value)
        {
            if (_updatingSource)
            {
                return;
            }
            var target = _targetObjectRef.Target as DependencyObject;
            if (target == null)
            {
                return;
            }
            var dispatcher = target.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                using var scope = BindingLog.Enter(Description, target);
                OnViewModelValueUpdated(target, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdated(target, sender, value);
                dispatcher.BeginInvoke(DispatcherPriority.Render, action);   // never waited for (Invoke could deadlock)
            }
        }

        private void OnViewModelValueUpdated(DependencyObject target, object? sender, object? value)
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
                var args = new ConverterArgs(values, null);
                try
                {
                    value = converterFunc(args) ?? TargetNullValue;
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }

            value = CalculateValue(value);
            if (CanUpdateTarget(value))
            {
                var ars = NotificationArgs.CreateFromSource(sender, value);
                try
                {
                    if (NeedNotify(dataContext, ars))
                        UpdateTarget(target, value);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, binding: Description);
                }
            }
        }


        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var target = _targetObjectRef.Target as DependencyObject;
            if (target == null)
            {
                return;
            }
            using var scope = BindingLog.Enter(Description, target);

            var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
            foreach (var manager in _sourceStateManagers)
            {
                manager.Unsubscribe();
            }
            if (!ReferenceEquals(sender, dataContextTarget))
            {
                VisualTreeHelperEx.UnSubscribeOnDataContext(sender as DependencyObject, OnDataContextChanged);
                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            var converterFunc = _convertersProxy.FindForwardConverter(e.NewValue);
            ConverterArgs args = new ConverterArgs(new object[_sourceStateManagers.Length], null);
            var hasConverter = converterFunc != null;

            if (e.NewValue != null)
            {
                for (int index = 0; index < _sourceStateManagers.Length; index++)
                {
                    var manager = _sourceStateManagers[index];
                    manager.Subscribe(e.NewValue);
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
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(value))
            {
                var ars = NotificationArgs.CreateFromTarget(sender, value);
                try
                {
                    if (NeedNotify(e.NewValue, ars))
                        UpdateTarget(target, value);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, binding: Description);
                }
            }
        }

        private bool CanUpdateTarget(object? value)
           => !(value is ExceptionHolder) || _targetInfo.IsObserver;

        private void UpdateTarget(DependencyObject target, object? value)
        {
            _updatingTarget = true;
            var stage = BindingIssueKind.MethodTargetThrew;   // what the catch reports: the observer, the view-model's setter or method, the control
            try
            {
                if (!_targetInfo.IsIniailized)
                {
                    _targetInfo.Initialize(target);
                }
                if (_targetInfo.IsObserver)
                {
                    var initialAccessor = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                    var obj1 = _treeHelper.GetViewModelProperty(_targetInfo.ObserverTarget, initialAccessor);
                    if (_targetInfo.Proxy == null || !ReferenceEquals(obj1, _targetInfo.Proxy.Target))
                    {
                        var getterType = _treeHelper.GetViewModelPropertyType(_targetInfo.ObserverTarget, initialAccessor);
                        if (getterType == null || (!ReflectionUtility.ContainsInterface(getterType,
                            new[] { typeof(IObserver<>), typeof(ISubscriber<>) })) || obj1 == null)
                        {
                            if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.PathNotFound, null, $"Target '{_targetInfo.ObserverTarget}' is no IObserver<T> or ISubscriber<T> (its type: {getterType?.Name ?? "none"})", target, value, binding: Description);
                            return;
                        }
                        if (_targetInfo.Proxy == null || !ReferenceEquals(_targetInfo.Proxy?.Target, obj1))
                        {
                            _targetInfo.Proxy?.OnCompleted();
                            _targetInfo.Proxy = new SubscriberProxy(obj1);
                        }
                    }
                    var holder = value as ExceptionHolder;
                    if (holder != null)
                        _targetInfo.Proxy.OnError(holder.Exception);
                    else
                        _targetInfo.Proxy.OnNext(value);

                    return;
                }

                if (!_targetInfo.IsDependencyObject)
                {
                    stage = Target.Contains("(") ? BindingIssueKind.MethodTargetThrew : BindingIssueKind.SetterThrew;
                    var initialAccessor = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                    _treeHelper.SetViewModelProperty(Target, initialAccessor, value);
                    return;
                }

                var obj = _targetInfo.TargetObj;
                var property = _targetInfo.TargetObjProperty;
                if (obj != null && property != null)
                {
                    stage = BindingIssueKind.TargetRefusedValue;   // the property's validation, or a control owned by another thread
                    obj.SetValue(property, value);
                }
                else
                {
                    if (obj == null)
                    {
                        if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.AncestorNotFound, null, $"no control in Target '{Target}'", target, value, binding: Description);
                    }
                    else if (property == null)
                    {
                        if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.PathNotFound, null, $"no property in Target '{Target}'", target, value, binding: Description);
                    }
                }
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(stage, ex, $"Target '{Target}'", target, value, binding: Description);
            }
            finally
            {
                _updatingTarget = false;
            }
        }


        private bool NeedNotify(object? dataContext, NotificationArgs args)
        {
            _notificationProxy.FindNotification(dataContext)?.Notify(args);
            return !args.Handled;
        }
    }
}
