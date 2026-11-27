using System.ComponentModel;
using FastBindings.StateManagerObjects;
using FastBindings.Interfaces;
using FastBindings.Helpers;
using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls.Platform;
using System.Reflection;
using System.Runtime.CompilerServices;

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

        public void ApplyTargets(BindableObject targetObject, BindableProperty targetProperty, BindableObject? anchorObject)
        {
            _targetObjectRef.Target = targetObject;
            _targetPropertyRef.Target = targetProperty;
            _dataContextParams.AnchorObject = anchorObject;
        }

        // While Initialize runs, a change of the target is not written to the source: a two-way binding could write a value the
        // target took while starting (another binding of the control setting its items first) into the view-model before the
        // view-model's own value reached the target.
        private bool _initializing;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Initialize(CacheStrategy cacheStrategy)
        {
            _initializing = true;
            try
            {
                InitializeCore(cacheStrategy);
            }
            finally
            {
                _initializing = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void InitializeCore(CacheStrategy cacheStrategy)
        {
            FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._started);
            var targetObject = _targetObjectRef.Target as BindableObject;
            var targetProperty = _targetPropertyRef.Target as BindableProperty;
            
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
            // A plain one-source binding of a CommonBinding pushes its value changes itself (DirectPush): nothing else to apply to
            // the value, so no lookups by name, no publisher and no checks per change.
            if (_sourceStateManagers.Length >= 1 && _sourceStateManagers[0] is SourceViewModelStateManager<T> first
                && _treeHelper is LightViewModelTreeHelper && cacheStrategy == CacheStrategy.None && _dataContextParams.AnchorObject == null
                && !HasConverter && !HasNotification && TargetNullValue == null && CanTrackViewModelChanges)
            {
                first.DirectPush = PushDirect;
                _direct = first;
            }

            if (CanTrackTargetChanges &&
                ReflectionUtility.BildPropertyInfo(targetObject, targetProperty.PropertyName).HasGetter)
            {
                targetObject.PropertyChanged += OnDependencyPropertyChanged;
            }
            var initialTarget = targetObject;
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
                    stateManager.PropertyUpdated += OnViewModelValueUpdated;
                    stateManager.Subscribe(dataContext);
                }
            }
            if (dataContext == null)
            {
                UpdateTarget(initialTarget, targetProperty, TargetNullValue);
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
                    var args = new ConverterArgs(values, targetProperty.ReturnType);
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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private object? CalculateValue(object? val)
        {
            if (!(val is ExceptionHolder))
                return val ?? TargetNullValue;

            return FallBackValue ?? val;
        }

        // The pusher for the last source type seen: a binding's source is nearly always one type, so no lookup per change.
        private Type? _pushType;
        private string? _pushName;
        private Action<BindableObject, object>? _pusher;
        private SourceViewModelStateManager<T>? _direct;
        private int _refreshQueued;

        /// <summary>
        /// A value change on the direct path: the target written straight from the source object through one delegate. Returns
        /// false — the general path takes the change — where no delegate fits the types, or if reading or writing threw (that path
        /// turns it into the fallback value). Off the target's dispatcher it only asks that thread to read the path's end again.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool PushDirect(object source, string name)
        {
            if (_updatingSource)
                return true;
            if (!(_targetObjectRef.Target is BindableObject target) || !(_targetPropertyRef.Target is BindableProperty property))
                return true;
            var dispatcher = target.Dispatcher;
            if (dispatcher != null && dispatcher.IsDispatchRequired)
            {
                // A change on another thread: what it read may be overtaken before the UI thread applies it (another worker, a
                // BindingContext swap), so it only asks the UI thread to read the path's end again — the last read applied is the
                // latest. One request at a time; a change while one waits is covered by the read it will make.
                if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
                    dispatcher.Dispatch(RefreshDirect);
                return true;
            }
            var type = source.GetType();
            if (!ReferenceEquals(type, _pushType) || !ReferenceEquals(name, _pushName))
            {
                _pusher = DirectPushers.For(type, name, property);
                _pushType = type;
                _pushName = name;
            }
            var pusher = _pusher;
            if (pusher == null)
                return false;
            _updatingTarget = true;
            try
            {
                FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._directPushes);
                pusher(target, source);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _updatingTarget = false;
            }
        }

        private void RefreshDirect()
        {
            Volatile.Write(ref _refreshQueued, 0);   // before reading: a change from now on asks again
            _direct?.RefreshFromCurrent();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void OnViewModelValueUpdated(object? sender, object value)
        {
            FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._generalPushes);
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
                OnViewModelValueUpdated(target, property, sender, value);
            }
            else
            {
                Action action = () => OnViewModelValueUpdated(target, property, sender, value);
                dispatcher.Dispatch(action);
            }
        }

        private void OnViewModelValueUpdated(BindableObject target, BindableProperty property, object? sender, object? value)
        {
            var hasConverter = HasConverter;
            var hasNotification = HasNotification;
            object? dataContext = null;
            if (hasConverter || hasNotification)
            {
                var dataContextTarget = _dataContextParams.GetDataContextTarget(target);
                dataContext = _dataContextParams.GetDataContext(dataContextTarget);
            }
            var converterFunc = hasConverter ? _convertersProxy.FindForwardConverter(dataContext) : null;

            if (converterFunc == null)
            {
                if (_sourceStateManagers.Length == 0 || !ReferenceEquals(sender, _sourceStateManagers[0]))
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
                if (!hasNotification || NeedNotify(dataContext, NotificationArgs.CreateFromSource(sender, value)))
                    UpdateTarget(target, property, value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update {target}.{property}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._dataContextChanges);
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
            var converterFunc = _convertersProxy.FindForwardConverter(newValue);
            ConverterArgs args = new ConverterArgs(new object[_sourceStateManagers.Length], property.ReturnType);
            var hasConverter = converterFunc != null;
            var canTrack = CanTrackViewModelChanges;
            if (newValue != null)
            {
                for (int index = 0; index < _sourceStateManagers.Length; index++)
                {
                    var manager = _sourceStateManagers[index];
                    if (canTrack)
                    {
                        manager.Subscribe(newValue);
                    }
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
                    if (NeedNotify(newValue, ars))
                        UpdateTarget(target, property, value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write($"[FastBinding] An error occurred while attempting to update for {target}.{property}");
                    System.Diagnostics.Debug.Write(ex);
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    value = new ExceptionHolder(ex);
                }
            }

        }

        // Whether a converter or a notification filter is configured at all: when neither is, a value change needs neither the
        // BindingContext nor a lookup nor NotificationArgs.
        private bool HasConverter => _convertersProxy.Converter != null || !string.IsNullOrEmpty(_convertersProxy.ConverterName);
        private bool HasNotification => _notificationProxy.Notification != null || !string.IsNullOrEmpty(_notificationProxy.NotificationName);

        private bool CanTrackViewModelChanges =>
            Mode == BindingMode.Default || Mode == BindingMode.OneWay
            || Mode == BindingMode.TwoWay;

        private bool CanTrackTargetChanges =>
            CanTrackOnlyTargetChanges || Mode == BindingMode.TwoWay;

        private bool CanTrackOnlyTargetChanges =>
            Mode == BindingMode.OneWayToSource;

        private static bool CanUpdateTarget(object? value)
            => !(value is ExceptionHolder);

        // The value type that last passed the type check for this binding's property: the same type passes again without
        // GetType().IsAssignableFrom on every change.
        private Type? _validValueType;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool CanUpdateTarget(BindableProperty property, object? value)
        {
            if (!CanUpdateTarget(value))
                return false;
            if (value != null && ReferenceEquals(value.GetType(), _validValueType))
                return true;
            if (!ReflectionUtility.IsValidType(value, property.ReturnType))
                return false;
            _validValueType = value?.GetType();
            return true;
        }


        private object? _bindableProperty;
        private void UpdateTarget(BindableObject target, BindableProperty property, object? value)
        {
            _updatingTarget = true;
            try
            {
                FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._targetWrites);
                target.SetValue(property, value);
                var temp = _dataContextParams.AnchorObject;
                if (temp == null)
                    return;

                _bindableProperty = _bindableProperty ?? ReflectionUtility.FindBindableProperty(temp,target);
                var prop = _bindableProperty as BindableProperty;
                if (prop != null)
                {
                    temp.SetValue(prop, prop.DefaultValue);
                    temp.SetValue(prop, target);
                }                
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

        private void OnDependencyPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
                {
                    FastBindingsDiagnostics.ObserveSourceWrite(target, property.PropertyName, val);
                    UpdateSource(val);
                }
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
            FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._sourceWrites);
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
