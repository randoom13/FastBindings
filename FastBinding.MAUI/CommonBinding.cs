using FastBindings.BindingManagers;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using System.ComponentModel;
using System.Reflection;
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
            }

            return targetProperty.DefaultValue;
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

        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Sources))
                return BindableProperty.UnsetValue;

            return base.ProvideValue(serviceProvider);
        }

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
                _updateManager.Initialize(CacheStrategy);
            }
        }
    }
}
