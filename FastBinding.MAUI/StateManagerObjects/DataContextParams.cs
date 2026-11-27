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

            var source = PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource);
            // no BindingContext from it: the binding reads nothing (as before), and says so
            if (source == null && Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.AncestorNotFound, detail: $"DataContextSource '{DataContextSource}' from {target.GetType().Name}", target: target);
            return source;
        }

        public object? GetDataContext(BindableObject? target)
        {
            return target?.BindingContext;
        }
    }
}
