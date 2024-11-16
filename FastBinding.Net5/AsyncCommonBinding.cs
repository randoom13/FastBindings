﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using FastBindings.Helpers;
using FastBindings.Interfaces;
using FastBindings.BindingManagers;

namespace FastBindings
{
    public class AsyncCommonBinding : BaseBinding
    {
        private readonly AsyncBindingUpdateManager<object> _updateManager =
     new AsyncBindingUpdateManager<object>(new LightViewModelTreeHelper());

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
        public object? TargetNullValue
        {
            get => _updateManager.TargetNullValue;
            set => _updateManager.TargetNullValue = value;
        }
        [DefaultValue(null)]
        public object? FallBackValue
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

        // Constructor
        public AsyncCommonBinding()
        {
        }

        // Constructor with parameter
        public AsyncCommonBinding(string sources)
        {
            Sources = sources;
        }
        public override object? ProvideValue(IServiceProvider serviceProvider)
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

            if (_updateManager.CanSubscribeOnDataContext(targetObject))
            {
                PrepareInitialization(targetObject, targetProperty);
            }
            else
                PrepareInitialization(serviceProvider, targetObject, targetProperty);

            return targetProperty.DefaultMetadata?.DefaultValue ?? DependencyProperty.UnsetValue;
        }

        internal override void LazyInitialization(DependencyObject targetObject, DependencyProperty targetProperty,
    DependencyObject? dataContextObj)
        {
            _updateManager.ApplyTargets(targetObject, targetProperty, dataContextObj);
            _updateManager.Initialize(this, CacheStrategy);
        }
    }
}