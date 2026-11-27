using FastBindings.Helpers;
using System;
using System.Windows;

namespace FastBindings.StateManagerObjects
{
    internal class DataContextParams
    {
        private WeakReference _anchorObject = new WeakReference(null);
        public DependencyObject? AnchorObject
        {
            get => _anchorObject.Target as DependencyObject;
            set { _anchorObject.Target = value; }
        }

        public string? DataContextSource { get; set; }

        public DependencyObject? GetDataContextTarget(DependencyObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                return AnchorObject ?? target;
            }

            var source = PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource);
            // before the control is loaded its ancestors may not be there yet: the binding looks again on Loaded
            if (source == null && !(target is FrameworkElement { IsLoaded: false }))
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.AncestorNotFound, detail: $"DataContextSource '{DataContextSource}' from {target.GetType().Name}", target: target);
            return source;
        }

        public object? GetDataContext(DependencyObject? target)
        {
            return target?.GetValue(FrameworkElement.DataContextProperty);
        }
    }
}
