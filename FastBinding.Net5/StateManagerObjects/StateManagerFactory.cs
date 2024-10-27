using System;
using System.Linq;
using System.Windows;
using FastBindings.Helpers;

namespace FastBindings.StateManagerObjects
{
    internal static class StateManagerFactory
    {
        private static SourceViewModelCache _cache = new SourceViewModelCache();

        public const string ErrorMessage = "[FastBinding] An error occurred while attempting to get data from the source property";
        public const string AsyncErrorMessage = "[AsyncFastBinding] An error occurred while attempting to get data from the source property";

        public static ISourceStateManager[] Build(string sources, DependencyObject targetObject, 
            DataContextParams dataContextParams, CacheStrategy cacheStrategy)
        {
            if (string.IsNullOrEmpty(sources)) 
            {
                throw new ArgumentException($"{nameof(sources)}");
            }
            var cache = cacheStrategy == CacheStrategy.Simple ? _cache : null;
            return sources.Trim().Split(new string[] { PropertyPathParser.PropertiesDevider },
                    StringSplitOptions.RemoveEmptyEntries).
                    Select(property =>
                    {
                        try
                        {
                            return Parse(property.Trim(), targetObject, dataContextParams, cache);
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
   
        private static ISourceStateManager Parse(string property, DependencyObject targetObject, DataContextParams dataContextParams,
             ICache? cache)
        {
            if (string.IsNullOrEmpty(property))
            {
                throw new ArgumentException(nameof(property));
            }
            if (!PropertyPathParser.NeedApply(property))
            {
                return new SourceViewModelStateManager(property, targetObject, dataContextParams) { Cache = cache };
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
                var depend = PropertyUtility.FindDependencyPropertyByName(child, propertyPathParser.Property);
                if (depend != null)
                {
                    return new SourceDependencyObjectStateManager(depend, child) ;
                }
                var trackingEvent = PropertyUtility.FindEventByName(child, propertyPathParser.Property);
                if (trackingEvent != null)
                {
                    return new SourceEventStateManager(child, trackingEvent);
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
