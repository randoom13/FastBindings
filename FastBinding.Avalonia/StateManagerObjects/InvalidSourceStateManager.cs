﻿using System;

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
        public object? GetSourceProperty(object? dataContext, bool isWrapException) => null;
        public void SetSourceProperty(object? value) { }
        public void Subscribe(object? dataContext) { }
        public void Unsubscribe() { }
    }
}