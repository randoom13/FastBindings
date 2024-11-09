using Avalonia;
using FastBindings.Helpers;
using System;

namespace FastBindings.StateManagerObjects
{
    internal class DataContextParams
    {
        private WeakReference _anchorObject = new WeakReference(null);
        public AvaloniaObject? AnchorObject
        { 
            get => _anchorObject.Target as AvaloniaObject;
            set { _anchorObject.Target = value; }
        }

        public string? DataContextSource { get; set; }

        public AvaloniaObject GetDataContextTarget(AvaloniaObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                return AnchorObject ?? target;
            }

            return PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource) ?? target;
        }

        public object? GetDataContext(AvaloniaObject target)
        {
            return target?.GetValue(StyledElement.DataContextProperty);
        }
    }
}
