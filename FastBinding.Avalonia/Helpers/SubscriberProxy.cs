using FastBindings.Interfaces;
using System;

namespace FastBindings.Helpers
{
    internal class SubscriberProxy : ISubscriber<object>
    {
        public object Target { get; set; }
        private Action<object, object?>? _onCompletedAction;
        private Action<object, object?>? _errorAction;
        private Action<object, object?>? _onNextAction;
        private bool _hasError = false;
        public SubscriberProxy(object target)
        {
            Target = target;
        }

        public void OnCompleted()
        {
            if (!_hasError)
            {
                _onCompletedAction = _onCompletedAction ?? ExpressionTreeHelper.CreateMethodAction(Target.GetType(), nameof(OnCompleted));
                _onCompletedAction?.Invoke(Target, null);
            }
        }

        public void OnError(Exception ex)
        {
            _hasError = true;
            _errorAction = _errorAction ?? ExpressionTreeHelper.CreateMethodAction(Target.GetType(), nameof(OnError));
            _errorAction?.Invoke(Target, ex);
        }

        public void OnNext(object? val)
        {
            if (!_hasError)
            {
                _onNextAction = _onNextAction ?? ExpressionTreeHelper.CreateMethodAction(Target.GetType(), nameof(OnNext));
                _onNextAction?.Invoke(Target, val);
            }
        }
    }
}
