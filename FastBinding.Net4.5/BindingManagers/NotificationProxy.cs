using FastBindings.Helpers;
using FastBindings.Interfaces;
using System;

namespace FastBindings.BindingManagers
{
    internal class NotificationProxy<T, I> where T : IBaseNotificationFilter
    {
        public NotificationProxy(IViewModelTreeHelper<I> treeHelper)
        {
            _treeHelper = treeHelper ?? throw new ArgumentException(nameof(treeHelper));
        }
        private readonly IViewModelTreeHelper<I> _treeHelper;

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

            var result = _treeHelper.GetFinalViewModel(dataContext, NotificationPath);
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
