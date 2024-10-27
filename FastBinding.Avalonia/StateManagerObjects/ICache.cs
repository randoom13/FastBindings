using FastBindings.Helpers;
using System.ComponentModel;

namespace FastBindings.StateManagerObjects
{
    internal interface ICache
    {
        bool TryGetCache(ViewModelPropertyInfo finalPropertyInfo, out object? result);
        void ApplyCache(ViewModelPropertyInfo finalPropertyInfo, object? value);
        void Clear();
        void PrepareCache(ViewModelPropertyInfo finalPropertyInfo, object? sender, PropertyChangedEventArgs args);
    }

    internal class SourceViewModelCache : ICache
    {
        private ViewModelPropertyInfo? _finalPropertyInfo;
        private object? _value;
        private bool _hasValue = false;
        private object? _sessionKey;

        public bool TryGetCache(ViewModelPropertyInfo finalPropertyInfo, out object? result)
        {
            var hasValue = _hasValue && _sessionKey != null && IsSame(finalPropertyInfo);
            result = hasValue ? _value : null;
            return hasValue;
        }

        public void ApplyCache(ViewModelPropertyInfo finalPropertyInfo, object? value)
        {
            if (!_hasValue && _sessionKey != null && IsSame(finalPropertyInfo))
            {
                _value = value;
                _hasValue = true;
            }
        }

        public void Clear()
        {
            _finalPropertyInfo = null;
            _value = null;
            _hasValue = false;
            _sessionKey = null;
        }

        private bool IsSame(ViewModelPropertyInfo finalPropertyInfo) =>
         ReferenceEquals(finalPropertyInfo.Accessor, _finalPropertyInfo?.Accessor)
               && finalPropertyInfo.Name == _finalPropertyInfo?.Name;

        public void PrepareCache(ViewModelPropertyInfo finalPropertyInfo, object? sender, PropertyChangedEventArgs args)
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
                _finalPropertyInfo = finalPropertyInfo;
            }
        }
    }
}
