using Avalonia;
using Avalonia.Controls;
using FastBindings.BindingManagers;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using System;
using System.ComponentModel;

namespace FastBindings
{
    public class AsyncFastBinding : BaseBinding
    {
        private readonly AsyncBindingUpdateManager<IPropertyAccessor> _updateManager = 
            new AsyncBindingUpdateManager<IPropertyAccessor>(new FastViewModelTreeHelper());

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
        public IBaseNotificationFilter? Notification
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

        public AsyncFastBinding()
        {
        }

        public AsyncFastBinding(string sources)
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
                _updateManager.Initialize(this,CacheStrategy);
            }
        }
    }
}
