using System;
using System.Windows;

namespace FastBindings.StateManagerObjects
{
    internal class InvalidSourceStateManager : ISourceStateManager
    {
        private Exception _ex;
        public InvalidSourceStateManager(Exception ex)
        {
            _ex = ex;
        }
        public event EventHandler<object> PropertyUpdated
        { add { } remove { } }
        public object? GetSourceProperty(object? dataContext, bool isWrapException) => DependencyProperty.UnsetValue;
        public void SetSourceProperty(object? value) { }
        public void Subscribe(object? dataContext) { }
        public void Unsubscribe() { }
    }
}
