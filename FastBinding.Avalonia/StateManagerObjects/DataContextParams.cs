using Avalonia;
using Avalonia.VisualTree;
using FastBindings.Helpers;
using System;

namespace FastBindings.StateManagerObjects
{
    internal class DataContextParams
    {
        // made only for an anchor (a binding on an object that is no control, in a template): nearly every binding has none
        private WeakReference? _anchorObject;
        public AvaloniaObject? AnchorObject
        {
            get => _anchorObject?.Target as AvaloniaObject;
            set
            {
                if (_anchorObject != null)
                    _anchorObject.Target = value;
                else if (value != null)
                    _anchorObject = new WeakReference(value);
            }
        }

        /// <summary>The binding's own weak reference to its target, shared with its sources that hold the same object (one fewer each).</summary>
        internal WeakReference? TargetRef { get; set; }

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
                if (FromParent && element is StyledElement styled)
                {
                    // the visual parent, which the DataContext is inherited from (a data template's root inherits it from the
                    // presenter it is shown in, not from its logical parent, a dock's or items control's own control); the logical
                    // one only before the element is in the visual tree
                    var parent = (styled as Visual)?.GetVisualParent() ?? styled.Parent as AvaloniaObject;
                    if (parent != null)
                        return parent;
                }
                return element;
            }

            var source = PropertyPathParser.CalculateSource(AnchorObject ?? target, DataContextSource);
            if (source != null)
                return source;
            // reads the control's own DataContext instead (as before), and says so
            if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.AncestorNotFound, detail: $"DataContextSource '{DataContextSource}' from {target.GetType().Name}", target: target);
            return target;
        }

        public object? GetDataContext(AvaloniaObject target)
        {
            return target?.GetValue(StyledElement.DataContextProperty);
        }
    }
}
