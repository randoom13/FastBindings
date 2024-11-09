using FastBindings.Helpers;
using FastBindings.BindingManagers;
using System.ComponentModel;

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
        private IViewModelTreeHelper<T>? _treeHelper;

        public SourceViewModelStateManager(string propertyName, BindableObject targetObject,
            DataContextParams dataContextParms)
        {
            _propertyNamePath = propertyName;
            _targetObjectRef = new WeakReference(targetObject);
            _dataContextParams = dataContextParms;
        }

        public bool SupportAsync { get; set; } = false;

        internal void Initialize(IViewModelTreeHelper<T> treeHelper)
        {
            _treeHelper = treeHelper;
        }

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public object? GetSourceProperty(object? dataContext, bool isWrapException)
        {
            return ExceptionUtility.Handle(() => _treeHelper?.GetViewModelProperty(_propertyNamePath, dataContext),
                                            isWrapException, StateManagerFactory.ErrorMessage);
        }

        public Task<object?> GetSourcePropertyAsync(object? dataContext, bool isWrapException)
        {
            return ExceptionUtility.AsyncHandle(() => _treeHelper?.GetViewModelProperty(_propertyNamePath, dataContext),
                                            isWrapException, StateManagerFactory.AsyncErrorMessage);
        }

        public void SetSourceProperty(object? value)
        {
            var target = _targetObjectRef.Target as BindableObject;
            if (target == null)
            {
                return;
            }

            var initialAccessor = _dataContextParams.GetDataContext(_dataContextParams.GetDataContextTarget(target));
            _updatingViewModel = true;
            try
            {
                _treeHelper?.SetViewModelProperty(_propertyNamePath, initialAccessor, value);
            }
            finally
            {
                _updatingViewModel = false;
            }
        }

        private void OnPropertyChanged(ViewModelPropertyInfo<T> finalPropertyInfo)
        {
            if (_updatingViewModel || finalPropertyInfo == null)
                return;

            object? value;
            try
            {
                if (Cache == null || !Cache.TryGetCache(finalPropertyInfo, out value))
                {
                    value = _treeHelper?.GetFinalViewModelEx(finalPropertyInfo.Accessor, finalPropertyInfo.Name);
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

        public void Subscribe(object? dataContext)
        {
            var subscribers = _treeHelper?.CalculateSubscribers(_propertyNamePath, dataContext).ToArray();
            if (subscribers == null || !subscribers.Any() || !subscribers.Any(it => it.Accessor is INotifyPropertyChanged))
            {
                return;
            }
            _handlerArgs = new SubcriberHandlerArgs(this, subscribers.Last());
            foreach (var propertyInfo in subscribers.Where(it => it.Accessor is INotifyPropertyChanged))
            {
                Subscribe(propertyInfo, _handlerArgs);
            }
        }

        protected class SubcriberHandlerArgs
        {
            public SubcriberHandlerArgs(SourceViewModelStateManager<T> manager, ViewModelPropertyInfo<T> finalPropertyInfo)
            {
                _managerRef = new WeakReference(manager);
                FinalPropertyInfo = finalPropertyInfo;
            }

            private readonly WeakReference _managerRef = new WeakReference(null);
            public SourceViewModelStateManager<T>? Manager => _managerRef.Target as SourceViewModelStateManager<T>;
            public ViewModelPropertyInfo<T> FinalPropertyInfo { get; private set; }
        }

        private SubcriberHandlerArgs? _handlerArgs;

        private static void Subscribe(ViewModelPropertyInfo<T> currentPropertyInfo, SubcriberHandlerArgs handlerArgs)
        {
            var currentName = currentPropertyInfo.Name;
            PropertyChangedEventHandler? propertyChangedHandler = null;
            propertyChangedHandler = (sender, args) =>
            {
                var handler = propertyChangedHandler;
                if (handler == null)
                {
                    return;
                }
                if (!ReferenceEquals(handlerArgs, handlerArgs.Manager?._handlerArgs))
                {
                    propertyChangedHandler = null;
                    var viewModel = sender as INotifyPropertyChanged;
                    if (viewModel != null)
                    {
                        viewModel.PropertyChanged -= handler;
                    }
                }
                if (propertyChangedHandler == null)
                {
                    return;
                }
                handlerArgs?.Manager?.Cache?.PrepareCache(handlerArgs.FinalPropertyInfo, sender, args);
                if (args.PropertyName == currentName)
                {
                    handlerArgs?.Manager?.OnPropertyChanged(handlerArgs.FinalPropertyInfo);
                }
            };
            INotifyPropertyChanged? changed = currentPropertyInfo.Accessor as INotifyPropertyChanged;
            if (changed != null)
            {
                changed.PropertyChanged += propertyChangedHandler;
            }
        }

        public void Unsubscribe()
        {
            _handlerArgs = default;
            Cache?.Clear();
        }
        internal ICache? Cache { get; set; }
    }
}
