using FastBindings.Helpers;

namespace FastBindings.StateManagerObjects
{
    internal class InvalidSourceStateManager : ISourceStateManager
    {
        private readonly Exception _ex;
        public InvalidSourceStateManager(Exception ex)
        {
            _ex = ex;
        }
        public event EventHandler<object> PropertyUpdated
        { add { } remove { } }

        public object? GetSourceProperty(object? dataContext, bool isWrapException) =>
            isWrapException ? new ExceptionHolder(_ex) : _ex;

        public void SetSourceProperty(object? value) { }
        public void Subscribe(object? dataContext) { }
        public void Unsubscribe() { }
    }
}
