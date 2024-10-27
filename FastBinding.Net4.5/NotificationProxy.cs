using FastBindings.Helpers;
using FastBindings.Interfaces;

namespace FastBindings
{
    internal class NotificationProxy<T> where T : IBaseNotificationFilter
    {
        public string NotificationPath { get; set; }
        public string NotificationName { get; set; }

        public T Notification { get; set; }

        private TNotification CalculateNotification<TNotification>(object dataContext)
    where TNotification : IBaseNotificationFilter
        {
            if (dataContext == null || string.IsNullOrEmpty(NotificationName))
                return default;

            if (string.IsNullOrEmpty(NotificationPath))
                return dataContext is TNotification ? (TNotification)dataContext : default;

            var result = ViewModelTreeHelper.GetFinalViewModel(dataContext as IPropertyAccessor, NotificationPath);
            return result is TNotification ? (TNotification)result : default;
        }

        internal T FindNotification(object dataContext)
        {
            if (Notification != null)
                return Notification;

            return CalculateNotification<T>(dataContext);
        }
    }
}
