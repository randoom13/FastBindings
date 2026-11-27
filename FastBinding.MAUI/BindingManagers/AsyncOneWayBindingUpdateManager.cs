using FastBindings.Diagnostics;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;

namespace FastBindings.BindingManagers
{
    internal class AsyncOneWayBindingUpdateManager<T>
    {
        public AsyncOneWayBindingUpdateManager(IViewModelTreeHelper<T> treeHelper)
        {
            _treeHelper = treeHelper;
            _convertersProxy = new ConvertersProxy<T>(_treeHelper);
            _notificationProxy = new NotificationProxy<IBaseNotificationFilter, T>(_treeHelper);
        }
        private readonly TargetInfo _targetInfo = new TargetInfo();
        private readonly IViewModelTreeHelper<T> _treeHelper;
        private ISourceStateManager[] _sourceStateManagers = Array.Empty<ISourceStateManager>();
        private readonly ConvertersProxy<T> _convertersProxy;
        private readonly WeakReference _targetObjectRef = new(null);
        private readonly NotificationProxy<IBaseNotificationFilter, T> _notificationProxy;

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

        public IBaseNotificationFilter? Notification
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
        private DataContextParams _dataContextParams = new();

        public void Initialize(VisualElement targetObject, CacheStrategy cacheStrategy)
        {
            _targetObjectRef.Target = targetObject;
            using var scope = BindingLog.Enter(Description, targetObject);
            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper, Description);
            if (!_sourceStateManagers.Any())
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.SourcesInvalid, detail: "no source in '" + Sources + "'", target: targetObject, binding: Description);
                return;
            }
            foreach (var stateManager in _sourceStateManagers.OfType<IAsyncSourceStateManager>())
            {
                stateManager.SupportAsync = true;
            }
            var initialTarget = targetObject;
            var targetObject1 = _dataContextParams.GetDataContextTarget(targetObject);
            if (targetObject1 != null)
                targetObject1.BindingContextChanged += OnDataContextChanged;
            var dataContext = _dataContextParams.GetDataContext(targetObject1);

            foreach (var stateManager in _sourceStateManagers)
            {
                stateManager.ListenTo(this, Updated, OnViewModelValueUpdated);
                stateManager.Subscribe(dataContext);
            }

            if (dataContext == null)
            {
                UpdateTargetEx(TargetNullValue, initialTarget);
            }
            else UpdateTargetEx(dataContext, initialTarget);
        }

        private async void UpdateTargetEx(object? dataContext, BindableObject target)
        {
            var converterFunc = _convertersProxy.FindAsyncForwardConverter(dataContext);
            object? value;
            if (converterFunc == null)
            {
                var manager = _sourceStateManagers.First();
                value = manager is IAsyncSourceStateManager ? await ((IAsyncSourceStateManager)manager).GetSourcePropertyAsync(dataContext, true) :
                    manager.GetSourceProperty(dataContext, true);
            }
            else
            {
                try
                {
                    var values = _sourceStateManagers.Select(manager => manager.GetSourceProperty(dataContext, false)).ToArray();
                    var args = new ConverterArgs(values, null);
                    value = await converterFunc(args);
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
                var ars = NotificationArgs.CreateFromSource(target, value);
                await UpdateAsync(ars, target, value, dataContext);
            }
        }

        private object? CalculateValue(object? val)
        {
            if (!(val is ExceptionHolder))
                return val ?? TargetNullValue;

            return FallBackValue ?? val;
        }

        // the listener for the source state managers, made once for every binding (see WeakEventPublisher.Listen)
        private static readonly Action<object, object, object?> Updated = (manager, sender, value) => ((AsyncOneWayBindingUpdateManager<T>)manager).OnViewModelValueUpdated(sender, value!);

        private void OnViewModelValueUpdated(object? sender, object value)
        {
            var target = _targetObjectRef.Target as BindableObject;
            if (target == null)
            {
                return;
            }
            var dispatcher = target.Dispatcher;
            if (!dispatcher.IsDispatchRequired)
            {
                using var scope = BindingLog.Enter(Description, target);
                OnViewModelValueUpdatedAsync(target, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdatedAsync(target, sender, value);
                dispatcher.Dispatch(action);
            }
        }

        private async void OnViewModelValueUpdatedAsync(BindableObject target, object? sender, object? value)
        {
            var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
            var dataContext = _dataContextParams.GetDataContext(dataContextTarget);

            var converterFunc = _convertersProxy.FindAsyncForwardConverter(dataContext);

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
                var ars = NotificationArgs.CreateFromSource(sender, value);
                await UpdateAsync(ars, target, value, dataContext);
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            var target = _targetObjectRef.Target as BindableObject;
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
                if (dataContextTarget != null)
                    dataContextTarget.BindingContextChanged -= OnDataContextChanged;

                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            var newValue = dataContextTarget?.BindingContext;
            using var scope = BindingLog.Enter(Description, target);
            UpdateTargetAsync(newValue, sender, target);
        }

        private async void UpdateTargetAsync(object? dataContext, object? sender, BindableObject target)
        {
            var converterFunc = _convertersProxy.FindAsyncForwardConverter(dataContext);
            var args = new ConverterArgs(new object[_sourceStateManagers.Length], null);
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
                    {
                        args.Values[index] = manager is IAsyncSourceStateManager ?
                            await ((IAsyncSourceStateManager)manager).GetSourcePropertyAsync(dataContext, true) :
                             manager.GetSourceProperty(dataContext, true);
                    }
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
                    value = await converterFunc(args);
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
                await UpdateAsync(ars, target, value, dataContext);
            }
        }

        private async Task UpdateAsync(NotificationArgs ars, BindableObject target, object? value,
object? dataContext)
        {
            try
            {
                if (VisualTreeHelperEx.IsAvailable(target) && await NeedNotifyAsync(dataContext, ars, true)
                    && VisualTreeHelperEx.IsAvailable(target))
                {
                    UpdateTarget(target, value);
                    await NeedNotifyAsync(dataContext, ars, false);
                }
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, value, dataContext?.GetType(), Description);
            }
        }

        private bool CanUpdateTarget(object? value)
            => !(value is ExceptionHolder) || _targetInfo.IsObserver;
        private void UpdateTarget(BindableObject target, object? value)
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

                var obj = _targetInfo.TargetObj;
                var property = _targetInfo.TargetObjProperty;
                if (obj == null)
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
                obj.SetValue(property, value);
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

        private async Task<bool> NeedNotifyAsync(object? dataContext, NotificationArgs args, bool isUpdating)
        {
            args.IsUpdating = isUpdating;
            args.Name = _notificationProxy.NotificationName;
            var filter = _notificationProxy.FindNotification(dataContext);
            if (filter != null)
            {
                INotificationFilter? simpleFilter = filter as INotificationFilter;
                if (simpleFilter != null)
                {
                    simpleFilter.Notify(args);
                }
                IAsyncNotificationFilter? asyncFilter = filter as IAsyncNotificationFilter;
                if (asyncFilter != null)
                {
                    await asyncFilter.NotifyAsync(args);
                }
            }
            return !args.Handled;
        }
    }
}
