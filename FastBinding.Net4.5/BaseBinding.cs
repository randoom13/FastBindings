using FastBindings.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using System.Xaml;

namespace FastBindings
{
    public abstract class BaseBinding : MarkupExtension
    {
        [DefaultValue(CacheStrategy.None)]
        public CacheStrategy CacheStrategy { get; set; }

        /* track binding likes
         <TextBox>
        <TextBox.Foreground>
            <SolidColorBrush Color = "{custom:CommonBinding Color}" />
        </TextBox.Foreground >
        </TextBox >*/
        protected void PrepareInitialization(DependencyObject targetObject, DependencyProperty targetProperty)
        {
            var topParent = targetObject;
            RoutedEventHandler? handler = null;
            handler = (sender, _) =>
            {
                if (handler == null)
                    return;

                VisualTreeHelperEx.UnSubscribeOnLoaded(topParent, handler);
                handler = null;
                LazyInitialization(targetObject, targetProperty, null);
            };
            if (!VisualTreeHelperEx.SubscribeOnLoaded(topParent, handler))
                handler = null;
        }

        protected void PrepareInitialization(IServiceProvider serviceProvider, DependencyObject targetObject,
            DependencyProperty targetProperty)
        {
            var objectProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
            var topParent = objectProvider?.RootObject as DependencyObject;
            RoutedEventHandler? handler = null;
            handler = (sender, _) =>
            {
                if (handler == null)
                    return;

                VisualTreeHelperEx.UnSubscribeOnLoaded(topParent, handler);
                handler = null;
                var dataContextParent = VisualTreeHelperEx.FindParentByValue(topParent, targetObject);
                LazyInitialization(targetObject, targetProperty, dataContextParent);
            };
            if (!VisualTreeHelperEx.SubscribeOnLoaded(topParent, handler))
                handler = null;
        }

        internal abstract void LazyInitialization(DependencyObject targetObject, DependencyProperty targetProperty,
            DependencyObject? dataContextObj);

    }

}
