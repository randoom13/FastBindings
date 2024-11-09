using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.XamlIl.Runtime;
using FastBindings.BindingManagers;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using System;
using System.ComponentModel;
using System.Linq;

namespace FastBindings
{
    public abstract class BaseBinding : AvaloniaObject
    {
        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

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
                visualElement.Loaded += OnTargetLoaded;
            }
            return AvaloniaProperty.UnsetValue;
        }

        // Switch to lazy loading to avoid issues with binding to a ListBox from any object in the DataTemplate
        internal abstract void OnTargetLoaded(object? sender, EventArgs? args);

        internal abstract void ApplyTarget(AvaloniaObject targetObject, AvaloniaProperty targetProperty, AvaloniaObject? dataContextObj);
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


        public override object? ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Sources))
                return AvaloniaProperty.UnsetValue;

            return base.ProvideValue(serviceProvider);
        }

        internal override void ApplyTarget(AvaloniaObject targetObject, AvaloniaProperty targetProperty, AvaloniaObject? dataContextObj)
        {
            _updateManager.ApplyTargets(targetObject, targetProperty, dataContextObj);
        }

        // Switch to lazy loading to avoid issues with binding to a ListBox from any object in the DataTemplate
        internal override void OnTargetLoaded(object? sender, EventArgs? args)
        {
            var element = sender as Control;
            if (element != null)
            {
                element.Loaded -= OnTargetLoaded;
                _updateManager.Initialize(CacheStrategy);
            }
        }
    }
}
