using FastBindings.BindingManagers;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace FastBindings
{
    public abstract class BaseBinding : IMarkupExtension
    {
        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

        private static VisualElement? FastCalculateParent(IProvideValueTarget? valueTarget)
        {
            try
            {
                var methodInfo = valueTarget?.GetType()?.GetRuntimeMethods()?.Where(it => it.Name.Contains("Parent")).FirstOrDefault();
                var parents = methodInfo?.Invoke(valueTarget, null) as IEnumerable<object>;
                return parents?.OfType<VisualElement>().FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to calculate parent");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }

        public virtual object? ProvideValue(IServiceProvider serviceProvider)
        {
            var valueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

            var targetProperty = valueTarget?.TargetProperty as BindableProperty;
            if (targetProperty == null)
                return BindableProperty.UnsetValue;

            var targetObject = valueTarget?.TargetObject as BindableObject;
            if (targetObject == null)
                return BindableProperty.UnsetValue;

            // Unfortunately, any binding to another dependency object without it does not work.
            var visualElement = targetObject as VisualElement;
            var anchor = (BindableObject?)null;

            if (visualElement == null) 
            {
                // dirty and hack way to get info
                visualElement = FastCalculateParent(valueTarget);
                anchor = visualElement;
            }
            if (visualElement == null)
            {
                var rootProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
                visualElement = rootProvider?.RootObject as VisualElement;
                if (visualElement != null)
                {
                    EventHandler? loaded = null;
                    loaded +=(top,_) => 
                    {
                        if (loaded == null)
                            return;

                        visualElement.Loaded -= loaded;
                        loaded = null;
                        var ob = targetObject as Element;
                        var root = (VisualElement?)null;
                        while (ob != null && root == null && !ReferenceEquals(ob, top))
                        {
                            ob = ob?.Parent;
                            root = ob as VisualElement;
                        }
                        if (root != null)
                            ApplyTargets(targetObject, targetProperty, root);
                    };
                    visualElement.Loaded += loaded;
                }
            }

            if (visualElement != null)
            {
                ApplyTargets(targetObject, targetProperty, anchor);
                visualElement.Loaded += OnTargetLoaded;
                // A plain binding (a path from the BindingContext, on the element itself) starts as soon as the element has a
                // BindingContext, like a compiled binding: in an app Loaded comes only once the platform view is attached, so a
                // value set then is a second layout pass on a view already measured with the defaults. Not inside ProvideValue:
                // what it returns is assigned to the property after it, over anything set there.
                if (anchor == null && ReferenceEquals(visualElement, targetObject) && StartsWithDataContext)
                    visualElement.BindingContextChanged += OnTargetBindingContext;
            }

            return targetProperty.DefaultValue;
        }

        /// <summary>Whether this binding may start on its element's first BindingContext change, before Loaded.</summary>
        internal virtual bool StartsWithDataContext => false;

        private bool _started;

        private void OnTargetBindingContext(object? sender, EventArgs e)
        {
            if (sender is VisualElement element && element.BindingContext != null)
            {
                element.BindingContextChanged -= OnTargetBindingContext;
                element.Loaded -= OnTargetLoaded;
                Start(element);
            }
        }

        /// <summary>Starts the binding once, whichever comes first: the BindingContext or Loaded.</summary>
        internal bool TryStart(VisualElement element)
        {
            element.BindingContextChanged -= OnTargetBindingContext;
            if (_started)
                return false;
            _started = true;
            return true;
        }

        private void Start(VisualElement element)
        {
            OnTargetLoaded(element, EventArgs.Empty);
        }

        internal abstract void ApplyTargets(BindableObject targetObject, BindableProperty targetProperty, BindableObject? anchorObject);

        internal abstract void OnTargetLoaded(object? sender, EventArgs? args);
    }

    [ContentProperty(nameof(Sources))]
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
        public object? TargetNullValue
        {
            get => _updateManager.TargetNullValue;
            set => _updateManager.TargetNullValue = value;
        }

        [DefaultValue(null)]
        public object? FallbackValue
        {
            get => _updateManager.FallBackValue;
            set => _updateManager.FallBackValue = value;
        }

        [DefaultValue(null)]
        public IValueConverterBase? Converter
        {
            get => _updateManager.Converter;
            set => _updateManager.Converter = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Sources))
                return BindableProperty.UnsetValue;

            return base.ProvideValue(serviceProvider);
        }

        // a path from the BindingContext and nothing that reads the tree: no $[[...]] source, no DataContextSource
        internal override bool StartsWithDataContext =>
            !PropertyPathParser.NeedApply(Sources) && string.IsNullOrEmpty(DataContextSource);

        internal override void ApplyTargets(BindableObject targetObject, BindableProperty targetProperty,
            BindableObject? anchorObject)
        {
            _updateManager.ApplyTargets(targetObject, targetProperty, anchorObject);
        }

        internal override void OnTargetLoaded(object? sender, EventArgs? args)
        {
            var element = sender as VisualElement;
            if (element != null)
            {
                element.Loaded -= OnTargetLoaded;
                if (TryStart(element))
                    _updateManager.Initialize(CacheStrategy);
            }
        }
    }
}
