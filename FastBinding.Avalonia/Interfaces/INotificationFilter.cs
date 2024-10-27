using System.Threading.Tasks;

namespace FastBindings.Interfaces
{
    public class NotificationArgs 
    {
        public object? Target { get; private set; }
        public object? Source { get; private set; }
        public string? Name { get; internal set; }
        public bool Handled { get; set; } = false;

        public bool IsUpdating { get; internal set; }
        private object? _value;

        public static NotificationArgs CreateFromSource(object? source, object? value) 
        {
            return new NotificationArgs(null, source, value);
        }

        public static NotificationArgs CreateFromTarget(object? target, object? value)
        {
            return new NotificationArgs(target, null, value);
        }

        private NotificationArgs(object? target, object? source, object? value)
        {
            Target = target;
            Source = source;
            _value = value;
        }
    }
    public interface IBaseNotificationFilter
    { }

    public interface INotificationFilter : IBaseNotificationFilter
    {
        void Notify(NotificationArgs args); 
    }

    public interface IAsyncNotificationFilter : IBaseNotificationFilter
    {
        Task NotifyAsync(NotificationArgs args);
    }
}
