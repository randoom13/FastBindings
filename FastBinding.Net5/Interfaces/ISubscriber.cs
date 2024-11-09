using System;

namespace FastBindings.Interfaces
{
    public interface ISubscriber<T>
    {
        void OnNext(T value);
        void OnError(Exception error);
        void OnCompleted();
    }
}
