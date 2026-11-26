using Avalonia;
using FastBindings.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace FastBindings.StateManagerObjects
{
    public interface ISourceStateManager
    {
        event EventHandler<object> PropertyUpdated;
        object? GetSourceProperty(object? dataContext, bool isWrapException);
        void SetSourceProperty(object? value);
        void Subscribe(object? dataContext);
        void Unsubscribe();
    }

    public interface IAsyncSourceStateManager
    {
        bool SupportAsync { get; set; }
        Task<object?> GetSourcePropertyAsync(object? dataContext, bool isWrapException);
    }

    internal class SourceViewModelStateManager<T> : ISourceStateManager, IAsyncSourceStateManager
    {
        private bool _updatingViewModel = false;

        private readonly DataContextParams _dataContextParams;
        private readonly WeakReference _targetObjectRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private readonly string _propertyNamePath;
        private IViewModelTreeHelper<T>? _viewModelTree;

        public bool SupportAsync { get; set; } = false;

        public SourceViewModelStateManager(string propertyName, AvaloniaObject targetObject,
            DataContextParams dataContextParms)
        {
            _propertyNamePath = propertyName;
            _targetObjectRef = new WeakReference(targetObject);
            _dataContextParams = dataContextParms;
        }

        internal void Initialize(IViewModelTreeHelper<T> tree) 
        {
            _viewModelTree = tree;
        }

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public object? GetSourceProperty(object? dataContext, bool isWrapException)
        {
            return ExceptionUtility.Handle(() =>
            _viewModelTree?.GetViewModelProperty(_propertyNamePath, dataContext),
            isWrapException, StateManagerFactory.ErrorMessage);
        }

        public Task<object?> GetSourcePropertyAsync(object? dataContext, bool isWrapException)
        {
            return ExceptionUtility.AsyncHandle(() =>
                  _viewModelTree?.GetViewModelProperty(_propertyNamePath, dataContext),
                isWrapException, StateManagerFactory.AsyncErrorMessage);
        }

        public void SetSourceProperty(object? value)
        {
            var target = _targetObjectRef.Target as AvaloniaObject;
            if (target == null)
                return;

            var initialAccessor = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
            _updatingViewModel = true;
            try
            {
                _viewModelTree?.SetViewModelProperty(_propertyNamePath, initialAccessor, value);
            }
            finally
            {
                _updatingViewModel = false;
            }
        }

        /// <summary>
        /// Set by a plain binding (no converter, notification, cache or async): told the object the path ends on and the last name,
        /// it writes the value into its target itself and returns true; false sends this change down the general path below.
        /// </summary>
        internal Func<object, string, bool>? DirectPush { get; set; }

        /// <summary>Tells the binding the value at the path's current end again, read now (on the UI thread, by the direct path).</summary>
        internal void RefreshFromCurrent()
        {
            ViewModelPropertyInfo<T>? info;
            lock (_gate)
            {
                info = _handlerArgs?.FinalPropertyInfo;
            }
            if (info != null)
                OnPropertyChanged(info);
        }

        private void OnPropertyChanged(ViewModelPropertyInfo<T> finalPropertyInfo)
        {
            if (_updatingViewModel || finalPropertyInfo == null)
                return;
            if (DirectPush != null && Cache == null && !SupportAsync && finalPropertyInfo.Accessor is object last
                && DirectPush(last, finalPropertyInfo.Name))
                return;

            object? value;
            try
            {
                if (Cache == null || !Cache.TryGetCache(finalPropertyInfo, out value))
                {
                    value = _viewModelTree?.GetFinalViewModelEx(finalPropertyInfo.Accessor, finalPropertyInfo.Name);
                    Cache?.ApplyCache(finalPropertyInfo, value);
                }
                if (!SupportAsync)
                {
                    _propertyUpdatedPublisher.RaiseEvent(this, value);
                }
                else
                    AwaitAndNotifyAsyncIfRequired(value, finalPropertyInfo);
            }
            catch (Exception ex)
            {
                var header = SupportAsync ? "[AsyncFastBinding]" : "[FastBinding]";
                System.Diagnostics.Debug.Write($"{header} An error occurred to {finalPropertyInfo.Name} from {finalPropertyInfo.Accessor}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                value = new ExceptionHolder(ex);
                _propertyUpdatedPublisher.RaiseEvent(this, value);
            }
        }

        private async void AwaitAndNotifyAsyncIfRequired(object? value, ViewModelPropertyInfo<T> finalPropertyInfo)
        {
            try
            {
                if (TasksHelper.IsTask(value))
                    value = await TasksHelper.GetResult((Task)value!);

                _propertyUpdatedPublisher.RaiseEvent(this, value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[AsyncFastBinding] An error occurred to {finalPropertyInfo.Name} from {finalPropertyInfo.Accessor}");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                value = new ExceptionHolder(ex);
                _propertyUpdatedPublisher.RaiseEvent(this, value);
            }
        }

        // Subscribe runs on the UI thread (DataContext changes) and, when an object in the middle of the path changes, on whatever
        // thread raised that change: the subscriptions and the handler args change only under this lock.
        private readonly object _gate = new object();

        public void Subscribe(object? dataContext)
        {
            lock (_gate)
            {
                SubscribeCore(dataContext);
            }
        }

        /// <summary>
        /// Subscribes again along the path from the same DataContext — only if <paramref name="current"/> is still this manager's
        /// subscription, checked and done under one lock. A change raised on a worker thread could otherwise pass the check, lose
        /// the race to a DataContext change on the UI thread, and then subscribe the binding to the view-model it had left.
        /// </summary>
        private bool ResubscribeIfCurrent(SubscriberHandlerArgs current, out ViewModelPropertyInfo<T> final)
        {
            lock (_gate)
            {
                final = null!;
                if (!ReferenceEquals(current, _handlerArgs))
                    return false;
                SubscribeCore(_dataContext);
                if (_handlerArgs == null)
                    return false;
                final = _handlerArgs.FinalPropertyInfo;
                return true;
            }
        }

        private void SubscribeCore(object? dataContext)
        {
            // Subscribe, then read the path again: an object in the middle of it may have been replaced on another thread after it
            // was read and before its handler was registered, and that change told nobody. If the path now reads other objects,
            // subscribe again (a few times at most; a change after the last check is told to the handlers registered by then).
            for (var attempt = 0; attempt < 8; attempt++)
            {
                RemoveSubscriptions();
                _dataContext = dataContext;
                var subscribers = _viewModelTree?.CalculateSubscribers(_propertyNamePath, dataContext)?.ToArray();
                if (subscribers == null || subscribers.Length == 0)
                {
                    return;
                }
                _handlerArgs = new SubscriberHandlerArgs(this, subscribers[subscribers.Length - 1]);
                for (var index = 0; index < subscribers.Length; index++)
                {
                    if (subscribers[index].Accessor is INotifyPropertyChanged source)
                    {
                        var subscription = new Subscription(_handlerArgs, subscribers[index].Name, isLast: index == subscribers.Length - 1);
                        SourceHub.Of(source).Add(subscribers[index].Name, subscription);
                        _subscriptions.Add((source, subscription));
                    }
                }
                if (subscribers.Length == 1 || SamePath(subscribers, _viewModelTree!.CalculateSubscribers(_propertyNamePath, dataContext)))
                {
                    return;
                }
            }
        }

        private static bool SamePath(ViewModelPropertyInfo<T>[] subscribed, IEnumerable<ViewModelPropertyInfo<T>> now)
        {
            var index = 0;
            foreach (var info in now)
            {
                if (index >= subscribed.Length || !ReferenceEquals(info.Accessor, subscribed[index].Accessor))
                    return false;
                index++;
            }
            return index == subscribed.Length;
        }

        protected class SubscriberHandlerArgs
        {
            public SubscriberHandlerArgs(SourceViewModelStateManager<T> manager, ViewModelPropertyInfo<T> finalPropertyInfo)
            {
                _managerRef = new WeakReference(manager);
                FinalPropertyInfo = finalPropertyInfo;
            }

            private readonly WeakReference _managerRef = new WeakReference(null);
            public SourceViewModelStateManager<T>? Manager => _managerRef.Target as SourceViewModelStateManager<T>;
            public ViewModelPropertyInfo<T> FinalPropertyInfo { get; private set; }
        }

        /// <summary>
        /// One PropertyChanged handler on one object of the path. It holds the manager weakly (as before, so a view-model never keeps a
        /// binding alive) and is removed by <see cref="Unsubscribe"/> at once, instead of on that object's next event.
        /// </summary>
        private sealed class Subscription : IHubSubscriber
        {
            private readonly SubscriberHandlerArgs _handlerArgs;
            private readonly string _name;
            private readonly bool _isLast;

            public Subscription(SubscriberHandlerArgs handlerArgs, string name, bool isLast)
            {
                _handlerArgs = handlerArgs;
                _name = name;
                _isLast = isLast;
            }

            public string Name => _name;

            public void OnSourceChanged(object? sender, PropertyChangedEventArgs args)
            {
                var manager = _handlerArgs.Manager;
                if (manager == null || !ReferenceEquals(_handlerArgs, manager._handlerArgs))
                {
                    // the binding is gone or subscribed elsewhere since: let go of this object
                    if (sender is INotifyPropertyChanged source)
                        SourceHub.Of(source).Remove(_name, this);
                    return;
                }
                manager.Cache?.PrepareCache(_handlerArgs.FinalPropertyInfo, sender, args);
                if (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != _name)
                {
                    return;
                }
                if (_isLast)
                {
                    manager.OnPropertyChanged(_handlerArgs.FinalPropertyInfo);
                    return;
                }
                // An object in the middle of the path changed (vm.Child = new Leaf()): what the path reads is another object now.
                // Subscribe again along the whole path and read from the new last object.
                if (manager.ResubscribeIfCurrent(_handlerArgs, out var final))
                    manager.OnPropertyChanged(final);
            }
        }

        private SubscriberHandlerArgs? _handlerArgs;
        private object? _dataContext;
        private readonly List<(INotifyPropertyChanged Source, Subscription Subscription)> _subscriptions =
            new List<(INotifyPropertyChanged, Subscription)>();

        private void RemoveSubscriptions()
        {
            foreach (var (source, subscription) in _subscriptions)
            {
                SourceHub.Of(source).Remove(subscription.Name, subscription);
            }
            _subscriptions.Clear();
        }

        public void Unsubscribe()
        {
            lock (_gate)
            {
                RemoveSubscriptions();
                _handlerArgs = default;
                _dataContext = null;
            }

            Cache?.Clear();
        }
        internal ICache? Cache { get; set; }
    }
}
