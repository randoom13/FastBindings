using FastBindings.Helpers;
using System;
using System.Linq;
using System.Windows;

namespace FastBindings.StateManagerObjects
{
    internal static class StateManagerFactory
    {
        private static SourceViewModelCache _cache = new SourceViewModelCache();

        public const string ErrorMessage = "[FastBinding] An error occurred while attempting to get data from the source property";
        public const string AsyncErrorMessage = "[AsyncFastBinding] An error occurred while attempting to get data from the source property";
        public static ISourceStateManager[] Build<T>(string sources, DependencyObject targetObject,
DataContextParams dataContextParams, CacheStrategy strategy, IViewModelTreeHelper<T> treeHelper)
        {
            if (string.IsNullOrEmpty(sources)) 
            {
                throw new ArgumentException($"{nameof(sources)}");
            }
            var cache = strategy == CacheStrategy.Simple ? _cache : null;
            var obj = dataContextParams.AnchorObject ?? targetObject;
            return sources.Trim().Split(new string[] { PropertyPathParser.PropertiesDevider },
                    StringSplitOptions.RemoveEmptyEntries).
                    Select(property =>
                    {
                        try
                        {
                            return Parse<T>(property.Trim(), obj, dataContextParams, cache, treeHelper);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.Write($"[FastBinding] Failed to create source state manager");
                            System.Diagnostics.Debug.Write(ex);
                            System.Diagnostics.Debug.Write(ex.StackTrace);
                            return (ISourceStateManager)new InvalidSourceStateManager(ex);
                        }
                    }).
                ToArray();
        }

        private static ISourceStateManager Parse<T>(string property, DependencyObject targetObject, DataContextParams dataContextParams,
        ICache? cache, IViewModelTreeHelper<T> treeHelper)
        {
            if (string.IsNullOrEmpty(property))
            {
                throw new ArgumentException(nameof(property));
            }
            if (!PropertyPathParser.NeedApply(property))
            {
                var manager = new SourceViewModelStateManager<T>(property, targetObject, dataContextParams) { Cache = cache };
                manager.Initialize(treeHelper);
                return manager;
            }
            var propertyPathParser = new PropertyPathParser(property);
            if (!propertyPathParser.IsValid)
            {
                throw new InvalidOperationException($"Unexpected {property}. Failed to parse.");
            }
            DependencyObject? child = propertyPathParser.CalculateSource(targetObject);
            if (child == null)
            {
                throw BuildException(propertyPathParser);
            }
            try
            {
                var depend = ReflectionUtility.FindDependencyPropertyByName(child, propertyPathParser.Property);
                if (depend != null)
                {
                    return new SourceDependencyObjectStateManager(depend, child, propertyPathParser.Optional) ;
                }
                var trackingEvent = ReflectionUtility.FindEventByName(child, propertyPathParser.Property);
                if (trackingEvent != null)
                {
                    return new SourceEventStateManager(child, trackingEvent, propertyPathParser.Optional);
                }
            }
            catch (Exception ex)
            {
                throw BuildException(propertyPathParser, ex);
            }
            throw BuildException(propertyPathParser);
        }

        private static Exception BuildException(PropertyPathParser parser, Exception? exception = null) 
        {
            var message = $"Could not find {parser.Property} on control {parser.Source}.";
            throw exception == null? new InvalidOperationException(message) : new InvalidOperationException(message, exception);
        }
    }
}
