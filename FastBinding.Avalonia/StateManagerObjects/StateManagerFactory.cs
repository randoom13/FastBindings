using Avalonia;
using System;
using System.Linq;
using FastBindings.Helpers;

namespace FastBindings.StateManagerObjects
{
    internal static class StateManagerFactory
    {
        public const string ErrorMessage = "[FastBinding] An error occurred while attempting to get data from the source property";
        public const string AsyncErrorMessage = "[AsyncFastBinding] An error occurred while attempting to get data from the source property";

        private static SourceViewModelCache _cache = new SourceViewModelCache();

        public static ISourceStateManager[] Build(string sources, AvaloniaObject targetObject, 
            DataContextParams dataContextParams, CacheStrategy cacheStrategy)
        {
            if (string.IsNullOrEmpty(sources)) 
            {
                throw new ArgumentException($"{nameof(sources)}");
            }
            var cache = cacheStrategy == CacheStrategy.Simple ? _cache : null;
            return sources.Split(new string[] { PropertyPathParser.PropertiesDevider },
                    StringSplitOptions.RemoveEmptyEntries).
                          Select(property =>
                          {
                              try
                              {
                                  return Parse(property.Trim(), targetObject, dataContextParams, cache);
                              }
                              catch (Exception ex)
                              {
                                  return (ISourceStateManager)new InvalidSourceStateManager(ex);
                              }
                          }).
                ToArray();
        }
   
        private static ISourceStateManager Parse(string property, AvaloniaObject targetObject, DataContextParams dataContextParams,
             ICache? cache)
        {
            if (string.IsNullOrEmpty(property))
                throw new ArgumentException(nameof(property));

            if (!PropertyPathParser.NeedApply(property))
                return new SourceViewModelStateManager(property, targetObject, dataContextParams) { Cache = cache };

            var propertyPathParser = new PropertyPathParser(property);
            if (!propertyPathParser.IsValid)
            {
                throw new InvalidOperationException($"[FastBinding] Unexpected {property}. Failed to parse.");
            }
            var child = propertyPathParser.CalculateSource(targetObject);
            if (child == null)
            {
                throw BuildException(propertyPathParser);
            }
            try
            {
                var dependencyProperty = PropertyUtility.FindDependencyPropertyByName(child, propertyPathParser.Property!);
                if (dependencyProperty != null)
                {
                    return new SourceDependencyObjectStateManager(dependencyProperty, child) ;
                }
                var trackingEvent = PropertyUtility.FindEventByName(child, propertyPathParser.Property!);
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
            var message = $"[FastBinding] Could not find {parser.Property} on control {parser.Source}.";
            throw exception == null? new InvalidOperationException(message) : new InvalidOperationException(message, exception);
        }
    }
}
