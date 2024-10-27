using System;
using System.Reflection;
using System.Windows;

namespace FastBindings.StateManagerObjects
{
    public class EventInfoArgs
    {
        public WeakReference SenderWeakRef { get; private set; }
        public object? EventArgs { get; private set; }
        public long Ticks { get; private set; }
        public string? EventName { get; private set; }

        public static long CalculateTicks()
        {
            return DateTime.UtcNow.Ticks;
        }

        public EventInfoArgs(object? sender, object? eventArgs, string? eventName)
        {
            SenderWeakRef =new WeakReference(sender);
            EventArgs = eventArgs;
            Ticks = CalculateTicks();
            EventName = eventName;
        }
    }

    internal class SourceEventStateManager : ISourceStateManager
    {
        private readonly WeakReference _eventInfoRef;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private WeakReference? _subscribedDelegateRef;
        private EventInfoArgs? _lastValue;

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceEventStateManager(DependencyObject sourceProperty, EventInfo eventInfo)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);
            _eventInfoRef = new WeakReference(eventInfo);
        }

        private void OnPropertyChanged(object sender, object args)
        {
            _lastValue = new EventInfoArgs(sender, args, (_eventInfoRef.Target as EventInfo)?.Name);
            _propertyUpdatedPublisher.RaiseEvent(this, _lastValue);
        }

        public object? GetSourceProperty(object? dataContext, bool isWrapException) => _lastValue;

        public void SetSourceProperty(object? value)
        {
            // work only as OneWayToSource binding
        }

        public void Subscribe(object? dataContext)
        {
            if (_subscribedDelegateRef?.IsAlive == true)
                return;

            var source = _sourcePropertyRef.Target as DependencyObject;
            var eventInfo = _eventInfoRef.Target as EventInfo;
            if (source == null || eventInfo == null)
            {
                return;
            }
            try
            {
                var method = GetType().GetMethod(nameof(OnPropertyChanged), BindingFlags.NonPublic | BindingFlags.Instance);
                var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType!, this, method!);
                eventInfo.AddEventHandler(source, handler);
                _subscribedDelegateRef = new WeakReference(handler);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FastBinding] Failed to subscribe to event {eventInfo.Name}");
                System.Diagnostics.Debug.WriteLine($"[FastBinding] {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FastBinding] {ex.StackTrace}");
            }
        }

        public void Unsubscribe()
        {
        }
    }
}
