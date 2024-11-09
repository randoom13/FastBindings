using FastBindings.Interfaces;
using System;

namespace AvaloniaExample.ViewModels
{
    public class SubscriberExample : ISubscriber<object>
    {
        public void OnNext(object value)
        {
            System.Diagnostics.Debug.WriteLine(value);
        }
        public void OnError(Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error.ToString());
        }
        public void OnCompleted()
        {
            System.Diagnostics.Debug.WriteLine("OnCompleted()");
        }
    }
}
