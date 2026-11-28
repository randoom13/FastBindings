using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FastBindings.Diagnostics;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.Threading;
using System.Linq;
using System.Reactive.Linq;

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
        private WeakReference _targetObjectRef = new WeakReference(null);
        private readonly NotificationProxy<INotificationFilter, T> _notificationProxy;

        public string Sources { get; set; } = string.Empty;

        /// <summary>What this binding reports its problems as (null while no logger is set).</summary>
        internal BindingDescription? Description { get; set; }

        private string ConverterDetail() => $"converter {(_convertersProxy.Converter?.GetType().Name ?? _convertersProxy.ConverterPath ?? "")}{(_convertersProxy.ConverterName != null ? " '" + _convertersProxy.ConverterName + "'" : "")}";

        public string Target
        {
            get => _targetInfo.Target;
            set { _targetInfo.Target = value; }
        }

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

        private DataContextParams _dataContextParams = new DataContextParams();

        public void Initialize(AvaloniaObject targetObject, CacheStrategy cacheStrategy)
        {
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
            _targetObjectRef.Target = targetObject;
            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper, Description);
            if (!_sourceStateManagers.Any())
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.SourcesInvalid, detail: "no source in '" + Sources + "'", target: targetObject, binding: Description);
                return;
            }

            var context = _dataContextParams.GetDataContextTarget(targetObject);
            var dataContext = _dataContextParams.GetDataContext(context);
            var initialTarget = targetObject;
            var frameworkElement = context as Control;
            if (frameworkElement != null)
            {
                frameworkElement.DataContextChanged += OnDataContextChanged;
            }
            else
            {
                UpdateTarget(initialTarget,  TargetNullValue ?? AvaloniaProperty.UnsetValue);
                return;
            }

            foreach (var stateManager in _sourceStateManagers)
            {
                stateManager.ListenTo(this, Updated, static self => self.OnViewModelValueUpdated);
                stateManager.Subscribe(dataContext);
            }
            if (dataContext == null)
            {
                UpdateTarget(initialTarget, TargetNullValue ?? AvaloniaProperty.UnsetValue);
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
                    var args = new ConverterArgs(values, null);
                    value = converterFunc(args);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), initialTarget, dataContextType: dataContext?.GetType(), binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(value))
                UpdateTarget(initialTarget, value);
        }


        private object? CalculateValue(object? val)
        {
            if (!(val is ExceptionHolder))
            {
                return val ?? TargetNullValue;
            }
            return FallBackValue ?? val;
        }

        // the listener for the source state managers, made once for every binding (see WeakEventPublisher.Listen)
        private static readonly Action<object, object, object?> Updated = (manager, sender, value) => ((OneWayBindingUpdateManager<T>)manager).OnViewModelValueUpdated(sender, value);

        private void OnViewModelValueUpdated(object? sender, object? value)
        {
            var target = _targetObjectRef?.Target as AvaloniaObject;
            if (target == null)
            {
                return;
            }
            var dispatcher = Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
            {
                using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
                OnViewModelValueUpdated(target, sender, value);
            }
            else if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
            {
                // A change on another thread: the UI thread reads the sources again when it gets to it — never Dispatcher.Invoke,
                // which held the worker until the UI thread ran it (for ever, while the UI thread waited for that worker). One
                // request at a time: a change while one waits is covered by the read it makes.
                dispatcher.Post(_refresh ??= Refresh);
            }
        }

        private int _refreshQueued;
        private Action? _refresh;

        private void Refresh()
        {
            Volatile.Write(ref _refreshQueued, 0);   // before reading: a change from now on asks again
            var target = _targetObjectRef?.Target as AvaloniaObject;
            if (target == null || _sourceStateManagers.Length == 0)
                return;
            using var scope = BindingLog.Enter(Description, target);
            try
            {
                TestFaults.ThrowIfArmed(target);
                var dataContext = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                if (dataContext == null)
                    return;
                var first = _sourceStateManagers[0];
                OnViewModelValueUpdated(target, first, first.GetSourceProperty(dataContext, true));
            }
            catch (Exception ex)
            {
                // no caller is left to catch it here: reported, never thrown into the dispatcher
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.UpdateFailed, ex, "a change from another thread, applied on the UI thread", target, binding: Description);
            }
        }

        private void OnViewModelValueUpdated(AvaloniaObject target, object? sender, object? value)
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
                var args = new ConverterArgs(values, null);
                try
                {
                    value = converterFunc(args) ?? TargetNullValue;
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, dataContextType: dataContext?.GetType(), binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }

            value = CalculateValue(value);
            if (!CanUpdateTarget(value))
            {
                return;
            }
            try
            {
                var ars = NotificationArgs.CreateFromSource(sender, value);
                if (NeedNotify(dataContext, ars))
                    UpdateTarget(target, value);
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, value, dataContext?.GetType(), Description);
            }
        }


        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
            var target = _targetObjectRef?.Target as AvaloniaObject;
            if (target == null)
            {
                return;
            }

            var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
            foreach (var manager in _sourceStateManagers)
            {
                manager.Unsubscribe();
            }
            if (!ReferenceEquals(sender, dataContextTarget))
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
            ConverterArgs args = new ConverterArgs(new object[_sourceStateManagers.Length], null);
            var hasConverter = converterFunc != null;
            if (dataContext != null)
            {
                for (int index = 0; index < _sourceStateManagers.Length; index++)
                {
                    var manager = _sourceStateManagers[index];
                    manager.Subscribe(dataContext);
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
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, dataContextType: dataContext?.GetType(), binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(value))
            {
                var ars = NotificationArgs.CreateFromTarget(sender, value);
                try
                {
                    if (NeedNotify(dataContext, ars))
                        UpdateTarget(target, value);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, value, dataContext?.GetType(), Description);
                }
            }
        }

        private bool CanUpdateTarget(object? value) => !(value is ExceptionHolder) || _targetInfo.IsObserver;

        private void UpdateTarget(AvaloniaObject target, object? value)
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
                    if (_targetInfo.Proxy == null || !ReferenceEquals(obj1, _targetInfo.Proxy?.Target))
                    {
                        var getterType = _treeHelper.GetViewModelPropertyType(_targetInfo.ObserverTarget, initialAccessor);
                        if (getterType == null || !ReflectionUtility.ContainsInterface(getterType,
                            new[] { typeof(IObserver<>), typeof(ISubscriber<>) }) || obj1 == null)
                        {
                            if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.PathNotFound, detail: $"Target '{_targetInfo.ObserverTarget}' is no IObserver<T> or ISubscriber<T> (its type: {getterType?.Name ?? "none"})", target: target, value: value, binding: Description);
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

                var targetObject = _targetInfo.TargetObj;
                var property = _targetInfo.TargetObjProperty;
                if (targetObject == null)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.AncestorNotFound, detail: $"no control in Target '{Target}'", target: target, value: value, binding: Description);
                    return;
                }
                else if (property == null)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.PathNotFound, detail: $"no property in Target '{Target}'", target: target, value: value, binding: Description);
                    return;
                }
                stage = BindingIssueKind.TargetRefusedValue;
                targetObject.SetValue(property, value);
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
            args.Name = _notificationProxy.NotificationName;
            _notificationProxy.FindNotification(dataContext)?.Notify(args);
            return !args.Handled;
        }
    }
}
