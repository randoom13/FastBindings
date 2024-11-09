using System.ComponentModel;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.BindingManagers;

namespace FastBindings
{
    [ContentProperty(nameof(Sources))]
    public class FastBinding : BaseBinding
    {
        private readonly BindingUpdateManager<IPropertyAccessor> _updateManager
            = new BindingUpdateManager<IPropertyAccessor>(new FastViewModelTreeHelper());

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
