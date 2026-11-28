using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FastBindings.Diagnostics;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.StateManagerObjects;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FastBindings.BindingManagers
{
    internal class BindingUpdateManager<T> : IDirectPush, IContextListener
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
        private AvaloniaProperty? _targetProperty;
        private readonly WeakReference _targetObjectRef = new WeakReference(null);

        public string Sources { get; set; } = string.Empty;

        /// <summary>What this binding reports its problems as (null while no logger is set).</summary>
        internal BindingDescription? Description { get; set; }

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
            _targetProperty = targetProperty;
            _dataContextParams.AnchorObject = dataContextObj;
            _dataContextParams.TargetRef = _targetObjectRef;
            _dataContextParams.FromParent = targetProperty == StyledElement.DataContextProperty;
        }

        // While Initialize runs, a change of the target is not written to the source: GetObservable(targetProperty) reports the
        // target's current value as soon as it is subscribed — its default (a ComboBox's SelectedIndex -1) — and a two-way binding
        // wrote that into the view-model before the view-model's own value reached the target.
        private bool _initializing;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Initialize(CacheStrategy cacheStrategy)
        {
            _initializing = true;
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
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
            var targetObject = _targetObjectRef.Target as AvaloniaObject;
            var targetProperty = _targetProperty;
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
            // A plain one-source binding of a CommonBinding pushes its value changes itself (DirectPush): nothing else to apply to
            // the value, so no lookups by name, no boxing, no publisher and no checks per change.
            if (_sourceStateManagers.Length >= 1 && _sourceStateManagers[0] is SourceViewModelStateManager<T> first
                && _treeHelper is LightViewModelTreeHelper && cacheStrategy == CacheStrategy.None
                && !HasConverter && !HasNotification && TargetNullValue == null && CanTrackViewModelChanges)
            {
                first.DirectPush = this;
                _direct = first;
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
                else ContextListeners.Add(frameworkElement, this);
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
                    stateManager.ListenTo(this, Updated, static self => self.OnViewModelValueUpdated);
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
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), initialTarget, dataContextType: dataContext.GetType(), binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(targetProperty, ref value))
                UpdateTarget(initialTarget, targetProperty, value);
        }


        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private object? CalculateValue(object? val)
        {
            if (!(val is ExceptionHolder))
            {
                return val ?? TargetNullValue;
            }
            return FallBackValue ?? val;
        }

        // The pusher for the last source type seen: a binding's source is nearly always one type, so no lookup per change.
        private Type? _pushType;
        private string? _pushName;
        private Action<AvaloniaObject, object>? _pusher;
        private SourceViewModelStateManager<T>? _direct;
        private int _refreshQueued;

        /// <summary>
        /// A value change on the direct path: the target written straight from the source object through one compiled, typed delegate.
        /// Returns false — the general path takes the change — off the UI thread (that path hands it to the dispatcher), where no
        /// typed delegate fits the types, or if reading or writing threw (that path turns it into the fallback value).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool PushDirect(object source, string name)
        {
            if (_updatingSource)
                return true;
            if (!(_targetObjectRef.Target is AvaloniaObject target) || !(_targetProperty is AvaloniaProperty property))
                return true;
            if (!Dispatcher.UIThread.CheckAccess())
            {
                // A change on another thread: what it read may be overtaken before the UI thread applies it (another worker, a
                // DataContext swap), so it only asks the UI thread to read the path's end again — the last read applied is the latest.
                // One request at a time; a change while one waits is covered by the read it will make.
                if (Interlocked.Exchange(ref _refreshQueued, 1) == 0)
                    Dispatcher.UIThread.Post(RefreshDirect);
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
            catch (Exception ex)
            {
                // the general path takes this change, and reports what it meets there
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.FastPathUnavailable, ex, $"the direct push of {name} threw", target, binding: Description);
                return false;
            }
            finally
            {
                _updatingTarget = false;
            }
        }

        private string ConverterDetail() => $"converter {(_convertersProxy.Converter?.GetType().Name ?? _convertersProxy.ConverterPath ?? "")}{(_convertersProxy.ConverterName != null ? " '" + _convertersProxy.ConverterName + "'" : "")}";

        private void RefreshDirect()
        {
            Volatile.Write(ref _refreshQueued, 0);   // before reading: a change from now on asks again
            _direct?.RefreshFromCurrent();
        }

        // the listener for the source state managers, made once for every binding (see WeakEventPublisher.Listen)
        private static readonly Action<object, object, object?> Updated = (manager, sender, value) => ((BindingUpdateManager<T>)manager).OnViewModelValueUpdated(sender, value);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void OnViewModelValueUpdated(object? sender, object? value)
        {
            FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._generalPushes);
            if (_updatingSource)
            {
                return;
            }
            var target = _targetObjectRef?.Target as AvaloniaObject;
            var property = _targetProperty;
            if (target == null || property == null)
            {
                return;
            }
            var dispatcher = Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
            {
                OnViewModelValueUpdated(target, property, sender, value);
            }
            else if (Interlocked.Exchange(ref _generalQueued, 1) == 0)
            {
                // A change on another thread: the UI thread reads the sources again when it gets to it, as the direct path does —
                // never Dispatcher.Invoke, which held the worker until the UI thread ran it (for ever, while the UI thread waited
                // for that worker) and made a closure per change. One request at a time: a change while one waits is covered by
                // the read it makes.
                dispatcher.Post(_refreshGeneral ??= RefreshGeneral);
            }
        }

        private int _generalQueued;
        private Action? _refreshGeneral;

        private void RefreshGeneral()
        {
            Volatile.Write(ref _generalQueued, 0);   // before reading: a change from now on asks again
            var target = _targetObjectRef.Target as AvaloniaObject;
            var property = _targetProperty;
            if (target == null || property == null || _sourceStateManagers.Length == 0 || _updatingSource)
                return;
            using var scope = BindingLog.Enter(Description, target);
            try
            {
                var dataContext = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
                if (dataContext == null)
                    return;
                var first = _sourceStateManagers[0];
                OnViewModelValueUpdated(target, property, first, first.GetSourceProperty(dataContext, true));
            }
            catch (Exception ex)
            {
                // no caller is left to catch it here: reported, never thrown into the dispatcher
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.UpdateFailed, ex, "a change from another thread, applied on the UI thread", target, binding: Description);
            }
        }

        private void OnViewModelValueUpdated(AvaloniaObject target, AvaloniaProperty property, object? sender, object? value)
        {
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
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
                var args = new ConverterArgs(values, property.PropertyType);
                try
                {
                    value = converterFunc(args) ?? TargetNullValue;
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, value, dataContext?.GetType(), Description);
                    value = new ExceptionHolder(ex);
                }
            }

            value = CalculateValue(value);
            if (!CanUpdateTarget(property, ref value))
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
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, value, dataContext?.GetType(), Description);
            }
        }


        // While its target's DataContext change goes through, a binding writes nothing back. Avalonia's selecting controls defer
        // their selection while the DataContext updates and, when it ends, clear it and then set the new view-model's value
        // (OnDataContextEndUpdate): both reached the source as writes, so a view moved to another view-model wrote null and then
        // its own value into it (a report's grouping rebuilt the report). The change ends with the dispatcher's turn, counted by
        // one number for the UI thread and one posted step per turn, not one per binding (a view of thousands of bindings
        // changes DataContext at once). Only the bindings whose DataContext changed in this turn are held back: one flag for the
        // whole thread also dropped what was typed into a box beside a list whose items changed (at a few frames a second the
        // turn's end came after the next key).
        [ThreadStatic] private static int s_turn;
        [ThreadStatic] private static bool s_turnEndPosted;
        [ThreadStatic] private static Action? s_endTurn;
        private int _contextTurn = -1;

        void IContextListener.DataContextChanged(object? sender, EventArgs e) => OnDataContextChanged(sender, e);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            _contextTurn = s_turn;
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
            if (!s_turnEndPosted)
            {
                s_turnEndPosted = true;
                Dispatcher.UIThread.Post(s_endTurn ??= () => { s_turn++; s_turnEndPosted = false; }, DispatcherPriority.Background);
            }
            FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._dataContextChanges);
            var target = _targetObjectRef?.Target as AvaloniaObject;
            var property = _targetProperty;
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
                    ContextListeners.Remove(element, this);
                }
                if (!ReferenceEquals(sender, dataContextTarget))
                    return;
            }

            var dataContext = dataContextTarget.GetValue(StyledElement.DataContextProperty);
            var converterFunc = _convertersProxy.FindForwardConverter(dataContext);
            var hasConverter = converterFunc != null;
            var args = hasConverter ? new ConverterArgs(new object[_sourceStateManagers.Length], property.PropertyType) : null;
            object? first = null;
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
                    if (args != null)
                    {
                        args.Values[index] = manager.GetSourceProperty(dataContext, false);
                    }
                    else if (index == 0)
                        first = manager.GetSourceProperty(dataContext, true);
                }
            }
            object? value;
            if (converterFunc == null)
            {
                value = first;
            }
            else
            {
                try
                {
                    value = converterFunc(args!);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ForwardConverterThrew, ex, ConverterDetail(), target, dataContextType: dataContext?.GetType(), binding: Description);
                    value = new ExceptionHolder(ex);
                }
            }
            value = CalculateValue(value);
            if (CanUpdateTarget(property, ref value))
            {
                try
                {
                    if (!HasNotification || NeedNotify(dataContext, NotificationArgs.CreateFromTarget(sender, value)))
                        UpdateTarget(target, property, value);
                }
                catch (Exception ex)
                {
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, value, dataContext?.GetType(), Description);
                }
            }
        }

        // Whether a converter or a notification filter is configured at all: when neither is, a value change needs neither the
        // DataContext nor a lookup nor NotificationArgs.
        private bool HasConverter => _convertersProxy.Converter != null || !string.IsNullOrEmpty(_convertersProxy.ConverterName);
        private bool HasNotification => _notificationProxy.Notification != null || !string.IsNullOrEmpty(_notificationProxy.NotificationName);

        private bool CanTrackViewModelChanges =>
            Mode == BindingMode.Default || Mode == BindingMode.OneWay
            || Mode == BindingMode.TwoWay;

        private bool CanTrackTargetChanges =>
            CanTrackOnlyTargetChanges || Mode == BindingMode.TwoWay;

        private bool CanTrackOnlyTargetChanges =>
            Mode == BindingMode.OneWayToSource;

        private static bool CanUpdateTarget(object? value) => !(value is ExceptionHolder);

        // The value type that last passed the type check for this binding's property: the same type passes again without
        // GetType().IsAssignableFrom on every change.
        private Type? _validValueType;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool CanUpdateTarget(AvaloniaProperty property, ref object? value)
        {
            if (!CanUpdateTarget(value))
                return false;
            if (value != null && ReferenceEquals(value.GetType(), _validValueType))
                return true;
            if (!ReflectionUtility.IsValidType(value, property.PropertyType))
            {
                if (ReferenceEquals(value, AvaloniaProperty.UnsetValue))
                    return true;   // what an Avalonia converter returns for "the property's default"
                if (ReferenceEquals(value, Avalonia.Data.BindingOperations.DoNothing))
                    return false;   // what it returns for "leave the property as it is"
                if (value == null)
                {
                    // no value for a property that cannot hold null (a null on the path, a DataContext going away): the property goes
                    // back to its default, as an Avalonia binding's does — nothing wrong with the binding, nothing reported
                    value = AvaloniaProperty.UnsetValue;
                    return true;
                }
                if (ReflectionUtility.TryConvert(property.PropertyType, ref value))
                    return true;   // as an Avalonia binding converts: an int into a double, "False" into a bool
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.ValueNotAssignable, detail: $"{property.Name} is {property.PropertyType.Name}, the value is {value?.GetType().Name}",
                    target: _targetObjectRef.Target, value: value, binding: Description);
                return false;
            }
            _validValueType = value?.GetType();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void UpdateTarget(AvaloniaObject target, AvaloniaProperty property, object? value)
        {
            _updatingTarget = true;
            try
            {
                FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._targetWrites);
                TargetSetters.For(property)(target, value);
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.TargetRefusedValue, ex, $"setting {target.GetType().Name}.{property.Name}", target, value, binding: Description);
            }
            finally
            {
                _updatingTarget = false;
            }
        }

        private void OnDependencyPropertyChanged(object? newValue)
        {
            if (_updatingTarget || _initializing || _contextTurn == s_turn)
            {
                return;
            }
            var target = _targetObjectRef?.Target as AvaloniaObject;
            var property = _targetProperty;
            if (target == null || property == null)
            {
                return;
            }
            using var scope = BindingLog.Enter(Description, _targetObjectRef?.Target);
            var dataContext = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
            var targetValue = target.GetValue(property);
            var val = targetValue;
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
            bool notify;
            try
            {
                notify = NeedNotify(dataContext, NotificationArgs.CreateFromTarget(target, val));
            }
            catch (Exception ex)
            {
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.NotificationFilterThrew, ex, "notification " + (_notificationProxy.NotificationName ?? ""), target, val, dataContext?.GetType(), Description);
                return;
            }
            if (notify)
            {
                FastBindingsDiagnostics.ObserveSourceWrite(target, property.Name, val);
                UpdateSource(val);
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
