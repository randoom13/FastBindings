using FastBindings.Helpers;
using System.ComponentModel;

namespace FastBindings.StateManagerObjects
{
    internal interface ICache
    {
        bool TryGetCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, out object result);
        void ApplyCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object value);
        void Clear();
        void PrepareCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object sender, PropertyChangedEventArgs args);
    }

    internal class SourceViewModelCache : ICache
    {
        private object _accessor;
        private string _name;

        private object _value;
        private bool _hasValue = false;
        private object _sessionKey;

        public bool TryGetCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, out object result)
        {
            var hasValue = _hasValue && _sessionKey != null && IsSame(finalPropertyInfo);
            result = hasValue ? _value : null;
            return hasValue;
        }

        public void ApplyCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object value)
        {
            if (!_hasValue && _sessionKey != null && IsSame(finalPropertyInfo))
            {
                _value = value;
                _hasValue = true;
            }
        }

        public void Clear()
        {
            _name = null;
            _accessor = null;
            _value = null;
            _hasValue = false;
            _sessionKey = null;
        }

        private bool IsSame<T>(ViewModelPropertyInfo<T> finalPropertyInfo) =>
         ReferenceEquals(finalPropertyInfo.Accessor, _accessor)
               && finalPropertyInfo.Name == _name;

        public void PrepareCache<T>(ViewModelPropertyInfo<T> finalPropertyInfo, object sender, PropertyChangedEventArgs args)
        {
            if (!ReferenceEquals(sender, finalPropertyInfo.Accessor))
            {
                Clear();
                return;
            }

            if (!ReferenceEquals(args, _sessionKey))
            {
                Clear();
                _sessionKey = args;
            }
            if (finalPropertyInfo == null || !IsSame(finalPropertyInfo))
            {
                Clear();
                _sessionKey = args;
                _name = null;
                _accessor = null;
            }
        }
    }
}
