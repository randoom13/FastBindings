using Avalonia;
using FastBindings.Diagnostics;
using FastBindings.Helpers;
using System;
using System.Reflection;

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
            SenderWeakRef = new WeakReference(sender);
            EventArgs = eventArgs;
            Ticks = CalculateTicks();
            EventName = eventName;
        }
    }

    internal class SourceEventStateManager : ISourceStateManager, IListenable
    {
        private readonly WeakReference _eventInfoRef;
        private string? _optional;
        private readonly WeakReference _sourcePropertyRef;
        private readonly WeakEventPublisher<object> _propertyUpdatedPublisher = new WeakEventPublisher<object>();
        private WeakReference? _subscribedDelegateRef;
        private object? _lastValue;

        /// <summary>The binding this source belongs to (null while no logger is set).</summary>
        internal BindingDescription? Description { get; set; }

        public void Listen(object target, Action<object, object, object?> open) => _propertyUpdatedPublisher.Listen(target, open);

        public event EventHandler<object> PropertyUpdated
        {
            add { _propertyUpdatedPublisher.Subscribe(value); }
            remove { _propertyUpdatedPublisher.Unsubscribe(value); }
        }

        public SourceEventStateManager(AvaloniaObject sourceProperty, EventInfo eventInfo, string? optional)
        {
            _sourcePropertyRef = new WeakReference(sourceProperty);
            _eventInfoRef = new WeakReference(eventInfo);
            _optional = optional;
        }

        private void OnPropertyChanged(object? sender, object? args)
        {
            using var scope = BindingLog.Enter(Description, sender);
            var copy = new EventInfoArgs(sender, args, (_eventInfoRef.Target as EventInfo)?.Name);
            _lastValue = copy;
            if (!string.IsNullOrEmpty(_optional))
            {
                try
                {
                    if (!CommonViewModelTreeHelper.TryCalculateValue(_optional, ref _lastValue))
                        _lastValue = copy;
                }
                catch (Exception ex)
                {
                    _lastValue = copy;
                    if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.EventSourceFailed, ex, $"reading [{_optional}] of the {copy.EventName} event's arguments",
                        target: sender, binding: Description);
                }
            }
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

            var source = _sourcePropertyRef.Target as AvaloniaObject;
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
                if (BindingLog.IsOn) BindingLog.Report(BindingIssueKind.EventSourceFailed, ex, $"subscribing to {source.GetType().Name}.{eventInfo.Name}",
                    target: source, binding: Description);
            }
        }

        public void Unsubscribe()
        {
        }
    }
}
