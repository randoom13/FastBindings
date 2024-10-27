
using FastBindings.Interfaces;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;

namespace FastBindings
{
    public class FastBinding : MarkupExtension
    {
        private readonly FastBindingUpdateManager _updateManager = new FastBindingUpdateManager();

        [DefaultValue(null)]
        public string NotificationPath
        {
            get => _updateManager.NotificationPath;
            set => _updateManager.NotificationPath = value;
        }

       [DefaultValue(null)]
        public string NotificationName
        {
            get => _updateManager.NotificationName;
            set => _updateManager.NotificationName = value;
        }

        [DefaultValue(null)]
        public INotificationFilter Notification
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
        public string DataContextSource
        {
            get => _updateManager.DataContextSource;
            set => _updateManager.DataContextSource = value;
        }

        [DefaultValue(null)]
        public string ConverterPath
        {
            get => _updateManager.ConverterPath;
            set => _updateManager.ConverterPath = value;
        }

        [DefaultValue(null)]
        public string ConverterName
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
        public object TargetNullValue
        {
            get => _updateManager.TargetNullValue;
            set => _updateManager.TargetNullValue = value;
        }

        [DefaultValue(null)]
        public object FallBackValue
        {
            get => _updateManager.FallBackValue;
            set => _updateManager.FallBackValue = value;
        }

        [DefaultValue(null)]
        public IValueConverterBase Converter
        {
            get => _updateManager.Converter;
            set => _updateManager.Converter = value;
        }
        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

        // Constructor
        public FastBinding()
        {
        }

        // Constructor with parameter
        public FastBinding(string sources)
        {
            Sources = sources;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Sources))
                return DependencyProperty.UnsetValue;

            var valueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetProperty = valueTarget?.TargetProperty as DependencyProperty;
            if (targetProperty == null)
                return DependencyProperty.UnsetValue;

            var targetObject = valueTarget?.TargetObject as DependencyObject;
            if (targetObject == null)
                return DependencyProperty.UnsetValue;

            return _updateManager.Initialize(targetObject, targetProperty, CacheStrategy);
        }
    }
}
