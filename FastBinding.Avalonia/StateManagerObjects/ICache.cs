using System;
using FastBindings.Helpers;
using System.ComponentModel;

namespace FastBindings.StateManagerObjects
{
    internal interface ICache
    {
        bool TryGetCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, out object? result);
        void ApplyCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object? value);
        void Clear();
        void PrepareCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object? sender, PropertyChangedEventArgs args);
    }

    /// <summary>
    /// CacheStrategy.Simple: one PropertyChanged event can reach several bindings reading the same property of the same object;
    /// the first reads the value and the others take it, for that event only (the event's args are the session key). One
    /// instance is shared by every such binding, whatever thread a change arrives on, so each step takes a lock. The object and
    /// the value are held weakly: a shared static slot must not keep the last view-model alive (a collected one is a miss).
    /// </summary>
    internal class SourceViewModelCache : ICache
    {
        private readonly WeakReference _accessor = new WeakReference(null);
        private string? _name;

        private readonly WeakReference _value = new WeakReference(null);
        private bool _hasValue = false;
        private object? _sessionKey;
        private readonly object _gate = new object();

        public bool TryGetCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, out object? result)
        {
            lock (_gate)
            {
                result = null;
                if (!_hasValue || _sessionKey == null || !IsSame(finalPropertyInfo))
                    return false;
                result = _value.Target;
                // a null stored is a value; a collected one (Target null although something was stored) is a miss
                return result != null || _storedNull;
            }
        }

        private bool _storedNull;

        public void ApplyCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object? value)
        {
            lock (_gate)
            {
                if (!_hasValue && _sessionKey != null && IsSame(finalPropertyInfo))
                {
                    _value.Target = value;
                    _storedNull = value == null;
                    _hasValue = true;
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _name = null;
                _accessor.Target = null;
                _value.Target = null;
                _storedNull = false;
                _hasValue = false;
                _sessionKey = null;
            }
        }

        private bool IsSame<T>(ViewModelPropertyInfo<T> finalPropertyInfo) =>
            finalPropertyInfo != null && finalPropertyInfo.Accessor != null
            && ReferenceEquals(finalPropertyInfo.Accessor, _accessor.Target) && finalPropertyInfo.Name == _name;

        public void PrepareCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object? sender, PropertyChangedEventArgs args)
        {
            lock (_gate)
            {
                if (finalPropertyInfo == null || !ReferenceEquals(sender, finalPropertyInfo.Accessor))
                {
                    Clear();
                    return;
                }

                // A new event, or another object or property: the slot starts again for this one. It was reset to null here,
                // so IsSame never held for a real view-model, ApplyCache never stored and the cache never returned a value.
                if (!ReferenceEquals(args, _sessionKey) || !IsSame(finalPropertyInfo))
                {
                    Clear();
                    _sessionKey = args;
                    _accessor.Target = finalPropertyInfo.Accessor;
                    _name = finalPropertyInfo.Name;
                }
            }
        }
    }
}
