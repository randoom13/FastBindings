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

        public string? NotificationPath { get; set; }
        public string? NotificationName { get; set; }

        public T? Notification { get; set; }

        private TNotification? CalculateNotification<TNotification>(object? dataContext)
    where TNotification : IBaseNotificationFilter
        {
            if (dataContext == null || string.IsNullOrEmpty(NotificationName))
                return default;

            if (string.IsNullOrEmpty(NotificationPath))
            {
                if (!(dataContext is IBaseNotificationFilter))
                    if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound,
                        detail: $"NotificationName '{NotificationName}' without NotificationPath or Notification: the DataContext ({dataContext.GetType().Name}) is no notification filter");
                return dataContext is TNotification ? (TNotification)dataContext : default;
            }

            var result = _treeHelper.GetFinalViewModel(dataContext, NotificationPath);
            if (!(result is IBaseNotificationFilter))
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound,
                    detail: $"NotificationPath '{NotificationPath}' on {dataContext.GetType().Name} is {(result == null ? "null" : result.GetType().Name + ", no notification filter")}");
            return result is TNotification ? (TNotification)result : default;
        }

        internal T? FindNotification(object? dataContext)
        {
            return Notification ?? CalculateNotification<T>(dataContext);
        }
    }
}
