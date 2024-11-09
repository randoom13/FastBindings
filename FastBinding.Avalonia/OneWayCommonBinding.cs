using Avalonia.Controls;
using FastBindings.BindingManagers;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using System;
using System.ComponentModel;

namespace FastBindings
{
    public class OneWayCommonBinding : BaseOneWayBinding
    {
        private readonly OneWayBindingUpdateManager<object> _updateManager =
            new OneWayBindingUpdateManager<object>(new LightViewModelTreeHelper());

        public OneWayCommonBinding()
        {
        }


        public string Sources
        {
            get => _updateManager.Sources;
            set { _updateManager.Sources = value; }
        }

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

        public OneWayCommonBinding(string sources)
        {
            Sources = sources?.Trim() ?? string.Empty;
        }

        internal override void Initialize(Control control)
        {
            if (control == null)
            {
                throw new ArgumentException(nameof(control));
            }
            control.Loaded += OnTargetLoaded;
        }

        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

        private void OnTargetLoaded(object? sender, EventArgs? args)
        {
            var element = sender as Control;
            if (element != null)
            {
                element.Loaded -= OnTargetLoaded;
                _updateManager.Initialize(element, CacheStrategy);
            }
        }
    }

}
