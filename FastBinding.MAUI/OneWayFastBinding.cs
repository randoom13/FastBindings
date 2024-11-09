using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.BindingManagers;
using System.ComponentModel;

namespace FastBindings
{
    [ContentProperty(nameof(Target))]
    public class OneWayFastBinding : BaseOneWayBinding, IMarkupExtension
    {
        private readonly OneWayBindingUpdateManager<IPropertyAccessor> _updateManager =
    new OneWayBindingUpdateManager<IPropertyAccessor>(new FastViewModelTreeHelper());

        public string Target
        {
            get => _updateManager.Target;
            set { _updateManager.Target = value; }
        }

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

        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

        public object? ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        internal override void Initialize(BindableObject view)
        {
            var element = view as VisualElement;
            if (element != null)
            {
                element.Loaded += OnTargetLoaded;
            }
        }

        private void OnTargetLoaded(object? sender, EventArgs? args)
        {
            var element = sender as VisualElement;
            if (element != null)
            {
                element.Loaded -= OnTargetLoaded;
                _updateManager.Initialize(element, CacheStrategy);
            }
        }
    }
}
