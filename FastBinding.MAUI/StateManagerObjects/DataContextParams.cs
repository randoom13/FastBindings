using FastBindings.Helpers;

namespace FastBindings.StateManagerObjects
{
    internal class DataContextParams
    {
        private WeakReference _anchorObject = new WeakReference(null);
        public BindableObject? AnchorObject
        {
            get => _anchorObject.Target as BindableObject;
            set { _anchorObject.Target = value; }
        }

        public string? DataContextSource { get; set; }

        public BindableObject? GetDataContextTarget(BindableObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                return AnchorObject ?? target;
            }

            return PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource);
        }

        public object? GetDataContext(BindableObject? target)
        {
            return target?.BindingContext;
        }
    }
}
