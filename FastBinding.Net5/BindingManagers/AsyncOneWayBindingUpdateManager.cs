using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
        private WeakReference _targetObjectRef = new(null);
        private readonly NotificationProxy<IBaseNotificationFilter, T> _notificationProxy;

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
        private bool _updatingSource = false;
        private bool _updatingTarget = false;

        private DataContextParams _dataContextParams = new();

        public void Initialize(DependencyObject targetObject, CacheStrategy cacheStrategy)
        {
            _targetObjectRef.Target = targetObject;
            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper);
            if (!_sourceStateManagers.Any())
            {
                System.Diagnostics.Debug.Write("[AsyncOneWayBinding] Could not create managers");
                return;
            }
            foreach (var stateManager in _sourceStateManagers.OfType<IAsyncSourceStateManager>())
            {
                stateManager.SupportAsync = true;
            }
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
                stateManager.PropertyUpdated += OnViewModelValueUpdated;
                stateManager.Subscribe(dataContext);
            }

            if (dataContext == null)
            {
                System.Diagnostics.Debug.Write("[AsyncFastBinding] No dataContext");
                UpdateTarget(initialTarget, TargetNullValue);
            }
            else UpdateTarget(dataContext, initialTarget);
        }

        private async void UpdateTarget(object? dataContext, DependencyObject target)
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
                    System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred on binding to {target} on " +
                        $"converter {_convertersProxy.ConverterName ?? "None"} during intialization");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
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
                OnViewModelValueUpdatedAsync(target, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdatedAsync(target, sender, value);
                dispatcher.Invoke(DispatcherPriority.Render, action);
            }
        }

        private async void OnViewModelValueUpdatedAsync(DependencyObject target, object? sender, object? value)
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
                    System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred while attempting to prepare data for update {target}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
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


        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var target = _targetObjectRef.Target as DependencyObject;
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
                VisualTreeHelperEx.UnSubscribeOnDataContext(target, OnDataContextChanged);
                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            UpdateTargetAsync(e.NewValue, sender, target);
        }

        private async void UpdateTargetAsync(object? dataContext, object? sender, DependencyObject target)
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
                    System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred while attempting to calculate value in converter for {target}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
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

        private async Task UpdateAsync(NotificationArgs ars, DependencyObject target, object? value,
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
                System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred while attempting to update {target}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }

        private bool CanUpdateTarget(object? value)  => !(value is ExceptionHolder) || _targetInfo.IsObserver;

        private void UpdateTarget(DependencyObject target, object? value)
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
                        if (getterType == null || (!ReflectionUtility.ContainsInterface(getterType,
                            new[] { typeof(IObserver<>), typeof(ISubscriber<>) })) || obj1 == null)
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
