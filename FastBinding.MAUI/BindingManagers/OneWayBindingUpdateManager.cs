using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;

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
        private ISourceStateManager[] _sourceStateManagers = Array.Empty<ISourceStateManager>();
        private readonly ConvertersProxy<T> _convertersProxy;
        private WeakReference _targetObjectRef = new(null);
        private readonly NotificationProxy<INotificationFilter, T> _notificationProxy;

        public string Target
        {
            get => _targetInfo.Target;
            set { _targetInfo.Target = value; }
        }
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

        private DataContextParams _dataContextParams = new();

        public void Initialize(VisualElement targetObject, CacheStrategy cacheStrategy)
        {
            _targetObjectRef.Target = targetObject;
            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper);
            if (!_sourceStateManagers.Any())
            {
                System.Diagnostics.Debug.Write("[FastBinding] Could not create managers");
                return;
            }

            var initialTarget = targetObject;
            var targetObject1 = _dataContextParams.GetDataContextTarget(targetObject);
            var dataContext = _dataContextParams.GetDataContext(targetObject1);

            if (targetObject1 != null)
                targetObject1.BindingContextChanged += OnDataContextChanged;

            foreach (var stateManager in _sourceStateManagers)
            {
                stateManager.PropertyUpdated += OnViewModelValueUpdated;
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
                    var values = _sourceStateManagers.Select(manager => manager.GetSourceProperty(dataContext, false)).ToArray();
                    var args = new ConverterArgs(values, null);
                    value = converterFunc(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred on binding to {initialTarget} on " +
                        $"converter {_convertersProxy.ConverterName ?? "None"} during intialization");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
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
                return val ?? TargetNullValue;

            return FallBackValue ?? val;
        }

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
                OnViewModelValueUpdated(target, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdated(target, sender, value);
                dispatcher.Dispatch(action);
            }
        }

        private void OnViewModelValueUpdated(BindableObject target, object? sender, object? value)
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
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to prepare data for update {target}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
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
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update {target}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
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
            var converterFunc = _convertersProxy.FindForwardConverter(newValue);
            ConverterArgs args = new ConverterArgs(new object[_sourceStateManagers.Length], null);
            var hasConverter = converterFunc != null;
            if (newValue != null)
            {
                for (int index = 0; index < _sourceStateManagers.Length; index++)
                {
                    var manager = _sourceStateManagers[index];
                    manager.Subscribe(newValue);
                    if (hasConverter)
                    {
                        args.Values[index] = manager.GetSourceProperty(newValue, false);
                    }
                    else if (index == 0)
                        args.Values[index] = manager.GetSourceProperty(newValue, true);
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
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to calculate value in converter for {target}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(value))
            {
                var ars = NotificationArgs.CreateFromTarget(sender, value);
                try
                {
                    if (NeedNotify(newValue, ars))
                        UpdateTarget(target, value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update for {target}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }

        }

        private bool CanUpdateTarget(object? value)
            => !(value is ExceptionHolder) || _targetInfo.IsObserver;

        private void UpdateTarget(BindableObject target, object? value)
        {
            _updatingTarget = true;
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
                        if (getterType == null || !ReflectionUtility.ContainsInterface(getterType,
                            new[] { typeof(IObserver<>), typeof(ISubscriber<>) }) || obj1 == null)
                        {
                            System.Diagnostics.Debug.Write($"[FastBinding] Could not find target observer {getterType?.ToString() ?? "none"} from" +
                                $"{_targetInfo.ObserverTarget}");
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
                    var initialAccessor = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                    _treeHelper.SetViewModelProperty(Target, initialAccessor, value);
                    return;
                }

                var obj = _targetInfo.TargetObj;
                var property = _targetInfo.TargetObjProperty;
                if (obj != null && property != null)
                {
                    obj.SetValue(property, value);
                }
                else
                {
                    if (obj == null)
                    {
                        System.Diagnostics.Debug.Write($"[FastBinding] Could not find target from {Target}");
                    }
                    else if (property == null)
                    {
                        System.Diagnostics.Debug.Write($"[FastBinding] Could not find target property from {Target}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to target {Target}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
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
