using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace FastBindings.BindingManagers
{
    internal class AsyncBindingUpdateManager<T>
    {
        public AsyncBindingUpdateManager(IViewModelTreeHelper<T> treeHelper)
        {
            _treeHelper = treeHelper;
            _convertersProxy = new ConvertersProxy<T>(_treeHelper);
            _notificationProxy = new NotificationProxy<IBaseNotificationFilter, T>(_treeHelper);
        }

        private readonly IViewModelTreeHelper<T> _treeHelper;
        private ISourceStateManager[] _sourceStateManagers = new ISourceStateManager[0];
        private readonly ConvertersProxy<T> _convertersProxy;
        private readonly WeakReference _targetPropertyRef = new WeakReference(null);
        private readonly WeakReference _targetObjectRef = new WeakReference(null);
        private readonly NotificationProxy<IBaseNotificationFilter, T> _notificationProxy;

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

        private bool _updatingTarget = false;
        private bool _updatingSource = false;

        public BindingMode Mode { get; set; }

        private readonly DataContextParams _dataContextParams = new DataContextParams();
      
        public bool CanSubscribeOnDataContext(DependencyObject targetObject)
        {
            var targetContextObject = _dataContextParams.GetDataContextTarget(targetObject);
            return VisualTreeHelperEx.CanSubscribeOnDataContext(targetContextObject);
        }

        public void ApplyTargets(DependencyObject targetObject, DependencyProperty targetProperty, DependencyObject? dataContextObj)
        {
            _targetObjectRef.Target = targetObject;
            _targetPropertyRef.Target = targetProperty;
            _dataContextParams.AnchorObject = dataContextObj;
        }

        public void Initialize(object sender, CacheStrategy cacheStrategy)
        {
            var targetObject = _targetObjectRef.Target as DependencyObject;
            var targetProperty = _targetPropertyRef.Target as DependencyProperty;
            if (targetObject == null || targetProperty == null)
            {
                return;
            }

            _sourceStateManagers = StateManagerFactory.Build(Sources, targetObject, _dataContextParams, cacheStrategy, _treeHelper);
            if (!_sourceStateManagers.Any())
            {
                System.Diagnostics.Debug.Write("[AsyncFastBinding] Could not create managers");
                return;
            }
            foreach (var stateManager in _sourceStateManagers.OfType<IAsyncSourceStateManager>())
            {
                stateManager.SupportAsync = true;
            }
            var initialTarget = targetObject;

            if (CanTrackTargetChanges && ReflectionUtility.BildPropertyInfo(targetObject, targetProperty.Name).HasGetter)
            {
                DependencyPropertyDescriptor.FromProperty(targetProperty, initialTarget.GetType())
                .AddValueChanged(initialTarget, OnDependencyPropertyChangedAsync);
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
                    applyOnTimeMode = true;
                    applyValue = false;
                }
                else applyValue = !VisualTreeHelperEx.SubscribeOnDataContext(targetContextObject, OnDataContextChanged);
            }
            if (applyValue)
            {
                UpdateTarget(initialTarget, targetProperty, TargetNullValue);
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
                System.Diagnostics.Debug.Write("[AsyncFastBinding] No dataContext");
                UpdateTarget(initialTarget, targetProperty, TargetNullValue);
            }
            else UpdateTarget(dataContext, sender, targetProperty, initialTarget);
        }

        private async void UpdateTarget(object? dataContext, object sender, DependencyProperty targetProperty, DependencyObject target)
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
                    var args = new ConverterArgs(values, targetProperty.PropertyType);
                    value = await converterFunc(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred on binding to {target}.{targetProperty} on " +
                        $"converter {_convertersProxy.ConverterName ?? "None"} during intialization");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(targetProperty, value))
            {
                var ars = NotificationArgs.CreateFromSource(sender, value);
                await UpdateAsync(ars, target, targetProperty, value, dataContext);
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
            var property = _targetPropertyRef.Target as DependencyProperty;
            if (target == null || property == null)
            {
                return;
            }
            var dispatcher = target.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                OnViewModelValueUpdatedAsync(target, property, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdatedAsync(target, property, sender, value);
                dispatcher.Invoke(DispatcherPriority.Render, action);
            }
        }

        private async void OnViewModelValueUpdatedAsync(DependencyObject target, DependencyProperty property, object? sender, object? value)
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
                    System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred while attempting to prepare data for update {target}.{property}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }

            value = CalculateValue(value);
            if (CanUpdateTarget(property, value))
            {
                var ars = NotificationArgs.CreateFromSource(sender, value);
                await UpdateAsync(ars, target, property, value, dataContext);
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

            UpdateTargetAsync(e.NewValue, sender, target, property);
        }

        private async void UpdateTargetAsync(object? dataContext, object? sender, DependencyObject target, DependencyProperty property)
        {
            var converterFunc = _convertersProxy.FindAsyncForwardConverter(dataContext);
            var args = new ConverterArgs(new object[_sourceStateManagers.Length], property.PropertyType);
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
                    System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred while attempting to calculate value in converter for {target}.{property}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(property, value))
            {
                var ars = NotificationArgs.CreateFromTarget(sender, value);
                await UpdateAsync(ars, target, property, value, dataContext);
            }
        }

        private async Task UpdateAsync(NotificationArgs ars, DependencyObject target, DependencyProperty property, object? value,
object? dataContext)
        {
            try
            {
                if (VisualTreeHelperEx.IsAvailable(target) && await NeedNotifyAsync(dataContext, ars, true)
                    && VisualTreeHelperEx.IsAvailable(target))
                {
                    UpdateTarget(target, property, value);
                    await NeedNotifyAsync(dataContext, ars, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred while attempting to update {target}.{property}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }

        private bool CanTrackViewModelChanges =>
            Mode == BindingMode.Default || Mode == BindingMode.OneWay
            || Mode == BindingMode.TwoWay;

        private bool CanTrackTargetChanges =>
            CanTrackOnlyTargetChanges || Mode == BindingMode.TwoWay;

        private bool CanTrackOnlyTargetChanges =>
            Mode == BindingMode.OneWayToSource;

        private static bool CanUpdateTarget(DependencyProperty property, object? value)
            => !(value is ExceptionHolder) && ReflectionUtility.IsValidType(value, property.PropertyType);

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

        private async void OnDependencyPropertyChangedAsync(object? sender, EventArgs e)
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
                if (await NeedNotifyAsync(dataContext, ars, true))
                {
                    UpdateSource(val);
                    await NeedNotifyAsync(dataContext, ars, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update for {target}.{property} during dependency property changing");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
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
