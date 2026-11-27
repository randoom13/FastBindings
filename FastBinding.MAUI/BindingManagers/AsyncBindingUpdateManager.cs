using System.ComponentModel;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using FastBindings.Diagnostics;
using FastBindings.Helpers;
using Microsoft.Maui.Controls;

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
        private ISourceStateManager[] _sourceStateManagers = Array.Empty<ISourceStateManager>();
        private readonly ConvertersProxy<T> _convertersProxy;
        private readonly WeakReference _targetPropertyRef = new(null);
        private readonly WeakReference _targetObjectRef = new(null);
        private readonly NotificationProxy<IBaseNotificationFilter, T> _notificationProxy;

        public string Sources { get; set; } = string.Empty;

        /// <summary>What this binding reports its problems as (null while no logger is set). Passed explicitly in the async methods:
        /// a thread's scope would end on whatever thread an await resumes on.</summary>
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
        private bool _updatingSource = false;

        public BindingMode Mode { get; set; }

        private DataContextParams _dataContextParams = new();

        public void ApplyTargets(BindableObject targetObject, BindableProperty targetProperty, BindableObject? anchorObject)
        {
            _targetObjectRef.Target = targetObject;
            _targetPropertyRef.Target = targetProperty;
            _dataContextParams.AnchorObject = anchorObject;
        }

        // While Initialize runs, a change of the target is not written to the source: GetObservable(targetProperty) reports the
        // target's current value — its default — as soon as it is subscribed, and a two-way binding wrote that into the
        // view-model before the view-model's value reached the target (as BindingUpdateManager did before).
        private bool _initializing;

        public void Initialize(object sender, CacheStrategy cacheStrategy)
        {
            _initializing = true;
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);   // a sync method: the async ones it starts run their first part inside it
            try
            {
                InitializeCore(sender, cacheStrategy);
            }
            finally
            {
                _initializing = false;
            }
        }

        private void InitializeCore(object sender, CacheStrategy cacheStrategy)
        {
            var targetObject = _targetObjectRef.Target as BindableObject;
            var targetProperty = _targetPropertyRef.Target as BindableProperty;
            if (targetObject == null || targetProperty == null)
            {
                return;
            }

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

            if (CanTrackTargetChanges && ReflectionUtility.BildPropertyInfo(targetObject, targetProperty.PropertyName)
                .HasGetter)
            {
                targetObject.PropertyChanged += OnDependencyPropertyChangedAsync;
            }

            var targetObject1 = _dataContextParams.GetDataContextTarget(targetObject);
            var dataContext = _dataContextParams.GetDataContext(targetObject1);

            var applyOnTimeMode = false;
            var isOneTimeOnly = Mode == BindingMode.OneTime;
            if (isOneTimeOnly && dataContext != null)
            {
                applyOnTimeMode = true;
            }
            else if (targetObject1 != null)
                targetObject1.BindingContextChanged += OnDataContextChanged;

            if (!applyOnTimeMode && !CanTrackOnlyTargetChanges)
            {
                foreach (var stateManager in _sourceStateManagers)
                {
                    stateManager.ListenTo(this, Updated, OnViewModelValueUpdated);
                    stateManager.Subscribe(dataContext);
                }
            }
            if (dataContext == null)
            {
                UpdateTarget(initialTarget, targetProperty, TargetNullValue);
                return;
            }
            else UpdateTarget(dataContext, sender, targetProperty, initialTarget);
        }

        private async void UpdateTarget(object? dataContext, object sender, BindableProperty targetProperty, BindableObject target)
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
                    var args = new ConverterArgs(values, targetProperty.ReturnType);
                    value = await converterFunc(args);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, dataContextType: dataContext?.GetType(), binding: Description);
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

        // the listener for the source state managers, made once for every binding (see WeakEventPublisher.Listen)
        private static readonly Action<object, object, object?> Updated = (manager, sender, value) => ((AsyncBindingUpdateManager<T>)manager).OnViewModelValueUpdated(sender, value!);

        private void OnViewModelValueUpdated(object? sender, object value)
        {
            if (_updatingSource)
            {
                return;
            }
            var target = _targetObjectRef.Target as BindableObject;
            var property = _targetPropertyRef.Target as BindableProperty;
            if (target == null || property == null)
            {
                return;
            }
            var dispatcher = target.Dispatcher;
            if (!dispatcher.IsDispatchRequired)
            {
                using var scope = BindingLog.Enter(Description, target);
                OnViewModelValueUpdatedAsync(target, property, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdatedAsync(target, property, sender, value);
                dispatcher.Dispatch(action);
            }
        }

        private async void OnViewModelValueUpdatedAsync(BindableObject target, BindableProperty property, object? sender, object? value)
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
                var args = new ConverterArgs(values, property.ReturnType);
                try
                {
                    value = converterFunc(args);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, value, dataContext?.GetType(), Description);
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

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            var target = _targetObjectRef.Target as BindableObject;
            var property = _targetPropertyRef.Target as BindableProperty;
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
                if (dataContextTarget != null)
                    dataContextTarget.BindingContextChanged -= OnDataContextChanged;

                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            var newValue = dataContextTarget?.BindingContext;
            using var scope = BindingLog.Enter(Description, target);
            UpdateTargetAsync(newValue, sender, target, property);
        }

        private async void UpdateTargetAsync(object? dataContext, object? sender, BindableObject target, BindableProperty property)
        {
            var converterFunc = _convertersProxy.FindAsyncForwardConverter(dataContext);
            var args = new ConverterArgs(new object[_sourceStateManagers.Length], property.ReturnType);
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
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, dataContextType: dataContext?.GetType(), binding: Description);
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

        private async Task UpdateAsync(NotificationArgs ars, BindableObject target, BindableProperty property, object? value,
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
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, value, dataContext?.GetType(), Description);
            }
        }

        private bool CanTrackViewModelChanges =>
            Mode == BindingMode.Default || Mode == BindingMode.OneWay
            || Mode == BindingMode.TwoWay;

        private bool CanTrackTargetChanges =>
            CanTrackOnlyTargetChanges || Mode == BindingMode.TwoWay;

        private bool CanTrackOnlyTargetChanges =>
            Mode == BindingMode.OneWayToSource;

        private bool CanUpdateTarget(BindableProperty property, object? value)
        {
            if (value is ExceptionHolder)
                return false;
            if (ReflectionUtility.IsValidType(value, property.ReturnType))
                return true;
            if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ValueNotAssignable, detail: $"{property.PropertyName} is {property.ReturnType.Name}, the value is {value?.GetType().Name}",
                target: _targetObjectRef.Target, value: value, binding: Description);
            return false;
        }

        private object? _bindableProperty;
        private void UpdateTarget(BindableObject target, BindableProperty property, object? value)
        {
            _updatingTarget = true;
            try
            {
                target.SetValue(property, value);
                var temp = _dataContextParams.AnchorObject;
                if (temp == null)
                    return;

                _bindableProperty = _bindableProperty ?? ReflectionUtility.FindBindableProperty(temp, target);
                var prop = _bindableProperty as BindableProperty;
                if (prop != null)
                {
                    temp.SetValue(prop, prop.DefaultValue);
                    temp.SetValue(prop, target);
                }
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.TargetRefusedValue, ex, $"setting {target.GetType().Name}.{property.PropertyName}", target, value, binding: Description);
            }
            finally
            {
                _updatingTarget = false;
            }
        }

        private async void OnDependencyPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
        {
            if (_updatingTarget || _initializing)
            {
                return;
            }
            var target = _targetObjectRef.Target as BindableObject;
            var property = _targetPropertyRef.Target as BindableProperty;
            if (target == null || property == null || e.PropertyName != property.PropertyName)
            {
                return;
            }
            object? dataContext;
            object? targetValue;
            object? val;
            using (BindingLog.Enter(Description, target))   // the part before the first await
            {
                dataContext = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                targetValue = target.GetValue(property);
                val = targetValue;
                var backConverterFunc = _convertersProxy.FindBackConverter(dataContext);
                if (backConverterFunc != null)
                {
                    try
                    {
                        val = backConverterFunc(new ConverterBackArgs() { Value = targetValue });
                    }
                    catch (Exception ex)
                    {
                        if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.BackConverterThrew, ex, ConverterDetail(), target, targetValue, dataContext?.GetType(), Description);
                        return;
                    }
                }
            }
            var ars = NotificationArgs.CreateFromTarget(target, val);
            try
            {
                if (!await NeedNotifyAsync(dataContext, ars, true))
                    return;
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, val, dataContext?.GetType(), Description);
                return;
            }
            using (BindingLog.Enter(Description, target))
                UpdateSource(val);
            try
            {
                await NeedNotifyAsync(dataContext, ars, false);
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, val, dataContext?.GetType(), Description);
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
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.SetterThrew, ex, "writing back to the source", _targetObjectRef.Target, values.ElementAtOrDefault(index), binding: Description);
                }
                finally
                {
                    _updatingSource = false;
                }
            }
        }
    }
}
