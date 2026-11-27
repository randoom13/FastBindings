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

            return PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource);
        }

        public object? GetDataContext(DependencyObject? target)
        {
            return target?.GetValue(FrameworkElement.DataContextProperty);
        }
    }
}
