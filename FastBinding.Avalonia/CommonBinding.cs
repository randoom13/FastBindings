using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using Avalonia.VisualTree;
using FastBindings.BindingManagers;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Linq;

namespace FastBindings
{
    public abstract class BaseBinding : AvaloniaObject
    {
        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public virtual object? ProvideValue(IServiceProvider serviceProvider)
        {
            var valueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetProperty = valueTarget?.TargetProperty as AvaloniaProperty;
            if (targetProperty == null)
                return AvaloniaProperty.UnsetValue;

            var targetObject = valueTarget?.TargetObject as AvaloniaObject;
            if (targetObject == null)
                return AvaloniaProperty.UnsetValue;

            var visualElement = targetObject as Control;
            var anchorObj = (Visual?)null;
            if (visualElement == null)
            {
                IAvaloniaXamlIlParentStackProvider? parentStackProvider = serviceProvider.GetService(typeof(IAvaloniaXamlIlParentStackProvider)) as IAvaloniaXamlIlParentStackProvider;
                if (parentStackProvider != null)
                {
                    var items = parentStackProvider.Parents.OfType<object>();
                    visualElement = items.OfType<Control>().FirstOrDefault();
                    anchorObj = visualElement;
                }
            }
            if (visualElement != null)
            {
                ApplyTarget(targetObject, targetProperty, anchorObj);
                // Start when the element joins the visual tree, before its first layout — not on Loaded, which comes after it:
                // until then IsVisible bindings still read true, so a view's hidden parts were measured once and their item
                // lists made containers that then stayed (the Inspector: 9 244 controls instead of 3 693).
                if (visualElement.IsAttachedToVisualTree())
                    OnTargetLoaded(visualElement, EventArgs.Empty);
                else
                {
                    ElementStarter.OnAttached(visualElement, this);
                    // A plain binding (a path from the DataContext, on the control itself) starts as soon as the control has a
                    // DataContext, like a compiled binding: a view whose DataContext is set before it joins the window gets its
                    // values while nothing is attached yet, when a change invalidates nothing. Not inside ProvideValue: what it
                    // returns is assigned to the property after it, over anything set there.
                    // Not a binding of DataContext itself: it reads its parent's, which a data template's root has only once it
                    // joins the tree (started earlier, it read the DataContext it was about to set).
                    if (anchorObj == null && StartsWithDataContext && targetProperty != StyledElement.DataContextProperty)
                        ElementStarter.OnDataContext(visualElement, this);
                }
            }
            return AvaloniaProperty.UnsetValue;
        }

        /// <summary>Whether this binding may start on its control's first DataContext change, before it joins the visual tree.</summary>
        internal virtual bool StartsWithDataContext => false;

        private bool _started;

        /// <summary>Starts the binding once, whichever comes first: the DataContext or joining the visual tree (told by <see cref="ElementStarter"/>).</summary>
        internal void Start(Control control)
        {
            if (_started)
                return;
            _started = true;
            OnTargetLoaded(control, EventArgs.Empty);
        }

        // Switch to lazy loading to avoid issues with binding to a ListBox from any object in the DataTemplate
        internal abstract void OnTargetLoaded(object? sender, EventArgs? args);

        internal abstract void ApplyTarget(AvaloniaObject targetObject, AvaloniaProperty targetProperty, AvaloniaObject? dataContextObj);
    }

    /// <summary>
    /// One AttachedToVisualTree handler and one DataContextChanged handler per control, for all of its bindings, each removed once
    /// by the control's own event. Before, every binding added and then removed its own two handlers: each removal from a
    /// multicast delegate allocates a new one, and a binding's start paid for both. A binding that started on the DataContext
    /// stays on the attach list until the control joins the tree and is skipped there (<see cref="BaseBinding.Start"/>).
    /// </summary>
    internal sealed class ElementStarter
    {
        private static readonly ConditionalWeakTable<Control, ElementStarter> Starters = new ConditionalWeakTable<Control, ElementStarter>();

        private List<BaseBinding>? _onAttached;
        private List<BaseBinding>? _onDataContext;

        public static void OnAttached(Control control, BaseBinding binding)
        {
            var starter = Starters.GetValue(control, _ => new ElementStarter());
            if (starter._onAttached == null)
            {
                starter._onAttached = new List<BaseBinding>(4);
                control.AttachedToVisualTree += starter.Attached;
            }
            starter._onAttached.Add(binding);
        }

        public static void OnDataContext(Control control, BaseBinding binding)
        {
            var starter = Starters.GetValue(control, _ => new ElementStarter());
            if (starter._onDataContext == null)
            {
                starter._onDataContext = new List<BaseBinding>(4);
                control.DataContextChanged += starter.DataContextChanged;
            }
            starter._onDataContext.Add(binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Attached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (!(sender is Control control))
                return;
            control.AttachedToVisualTree -= Attached;
            var bindings = _onAttached;
            _onAttached = null;
            if (bindings == null)
                return;
            foreach (var binding in bindings)
                binding.Start(control);
        }

        private void DataContextChanged(object? sender, EventArgs e)
        {
            if (!(sender is Control control) || control.DataContext == null)
                return;
            control.DataContextChanged -= DataContextChanged;
            var bindings = _onDataContext;
            _onDataContext = null;
            if (bindings == null)
                return;
            foreach (var binding in bindings)
                binding.Start(control);
        }
    }


    public class CommonBinding : BaseBinding
    {
        private readonly BindingUpdateManager<object> _updateManager
            = new BindingUpdateManager<object>(new LightViewModelTreeHelper());

        [DefaultValue(null)]
        public string? NotificationPath
        {
            get => _updateManager.NotificationPath;
            set => _updateManager.NotificationPath = value;
        }

        [DefaultValue(null)]
        public string? NotificationName
        {
            get => _updateManager.NotificationName;
            set => _updateManager.NotificationName = value;
        }

        [DefaultValue(null)]
        public INotificationFilter? Notification
        {
            get => _updateManager.Notification;
            set => _updateManager.Notification = value;
        }

        public string Sources
        {
            get => _updateManager.Sources;
            set => _updateManager.Sources = value;
        }

        [DefaultValue(null)]
        public string? DataContextSource
        {
            get => _updateManager.DataContextSource;
            set => _updateManager.DataContextSource = value;
        }

        [DefaultValue(null)]
        public string? ConverterPath
        {
            get => _updateManager.ConverterPath;
            set => _updateManager.ConverterPath = value;
        }

        [DefaultValue(null)]
        public string? ConverterName
        {
            get => _updateManager.ConverterName;
            set => _updateManager.ConverterName = value;
        }

        [DefaultValue(BindingMode.Default)]
        public BindingMode Mode
        {
            get => _updateManager.Mode;
            set => _updateManager.Mode = value;
        }

        [DefaultValue(null)]
        public object? FallBackValue
        {
            get => _updateManager.FallBackValue;
            set => _updateManager.FallBackValue = value;
        }

        [DefaultValue(null)]
        public object? TargetNullValue
        {
            get => _updateManager.TargetNullValue;
            set => _updateManager.TargetNullValue = value;
        }

        [DefaultValue(null)]
        public IValueConverterBase? Converter
        {
            get => _updateManager.Converter;
            set => _updateManager.Converter = value;
        }


        // Constructor
        public CommonBinding()
        {
        }

        // Constructor with parameter
        public CommonBinding(string sources)
        {
            Sources = sources?.Trim() ?? string.Empty;
        }


        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Sources))
                return AvaloniaProperty.UnsetValue;

            return base.ProvideValue(serviceProvider);
        }

        // a path from the DataContext and nothing that reads the tree: no $[[...]] source, no DataContextSource
        internal override bool StartsWithDataContext =>
            !PropertyPathParser.NeedApply(Sources) && string.IsNullOrEmpty(DataContextSource);

        internal override void ApplyTarget(AvaloniaObject targetObject, AvaloniaProperty targetProperty, AvaloniaObject? dataContextObj)
        {
            _updateManager.ApplyTargets(targetObject, targetProperty, dataContextObj);
        }

        // Switch to lazy loading to avoid issues with binding to a ListBox from any object in the DataTemplate
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal override void OnTargetLoaded(object? sender, EventArgs? args)
        {
            var element = sender as Control;
            if (element != null)
            {
                _updateManager.Initialize(CacheStrategy);
            }
        }
    }
}
