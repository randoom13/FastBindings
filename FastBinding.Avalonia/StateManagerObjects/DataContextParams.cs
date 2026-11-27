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

        /// <summary>
        /// A binding of the DataContext property itself reads its path from the parent's DataContext, as Avalonia's own binding does;
        /// read from the control's own DataContext it would read the value it has just set.
        /// </summary>
        public bool FromParent { get; set; }

        public AvaloniaObject GetDataContextTarget(AvaloniaObject target)
        {
            if (string.IsNullOrEmpty(DataContextSource))
            {
                var element = AnchorObject ?? target;
                if (FromParent && element is StyledElement styled && styled.Parent is AvaloniaObject parent)
                    return parent;
                return element;
            }

            return PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource) ?? target;
        }

        public object? GetDataContext(AvaloniaObject target)
        {
            return target?.GetValue(StyledElement.DataContextProperty);
        }
    }
}
