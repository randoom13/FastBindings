using FastBindings.Diagnostics;
using FastBindings.Helpers;
using FastBindings.BindingManagers;
using System.Runtime.CompilerServices;

namespace FastBindings.StateManagerObjects
{
    internal static class StateManagerFactory
    {
        private static SourceViewModelCache _cache = new SourceViewModelCache();

        internal const string ErrorMessage = "reading the source property";
        internal const string AsyncErrorMessage = "reading the async source property";

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static ISourceStateManager[] Build<T>(string sources, BindableObject targetObject,
            DataContextParams dataContextParams, CacheStrategy strategy, IViewModelTreeHelper<T> treeHelper, BindingDescription? description = null)
        {
            if (string.IsNullOrEmpty(sources)) 
            {
                throw new ArgumentException($"{nameof(sources)}");
            }
            var obj = dataContextParams.AnchorObject ?? targetObject;
            var cache = strategy == CacheStrategy.Simple ? _cache : null;
            if (sources.IndexOf(PropertyPathParser.PropertiesDevider[0]) < 0)
            {
                var single = sources.Trim();
                if (single.Length > 0)
                    return new[] { Describe(ParseOrInvalid(single, obj, dataContextParams, cache, treeHelper, description), description) };
            }
            return sources.Split(new string[] { PropertyPathParser.PropertiesDevider },
                    StringSplitOptions.RemoveEmptyEntries).
                               Select(property =>
                               {
                                   return Describe(ParseOrInvalid(property.Trim(), obj, dataContextParams, cache, treeHelper, description), description);
                               }).
                ToArray();
        }
        private static ISourceStateManager ParseOrInvalid<T>(string property, BindableObject targetObject, DataContextParams dataContextParams,
              ICache? cache, IViewModelTreeHelper<T> treeHelper, BindingDescription? description)
        {
            try
            {
                return Parse<T>(property, targetObject, dataContextParams, cache, treeHelper);
            }
            catch (Exception ex)
            {
                // the source shows nothing from now on (its reads are this exception): say why, once
                if (BindingLog.IsOn) BindingLog.Report(ex is BindingPathException path ? path.Kind : BindingIssueKind.SourcesInvalid, ex, $"source '{property}'",
                    target: targetObject, binding: description);
                return new InvalidSourceStateManager(ex);
            }
        }

        private static ISourceStateManager Describe(ISourceStateManager manager, BindingDescription? description)
        {
            if (description == null)
                return manager;
            switch (manager)
            {
                case SourceDependencyObjectStateManager dependency: dependency.Description = description; break;
                case SourceEventStateManager events: events.Description = description; break;
                default:
                    if (manager is IDescribedSource described)
                        described.Description = description;
                    break;
            }
            return manager;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static ISourceStateManager Parse<T>(string property, BindableObject targetObject, DataContextParams dataContextParams,
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
            var child = propertyPathParser.CalculateSource(targetObject);
            if (child == null)
            {
                throw new BindingPathException(BindingIssueKind.AncestorNotFound, $"no control '{propertyPathParser.Source}' from {targetObject.GetType().Name}");
            }
            try
            {
                var depend = ReflectionUtility.FindDependencyPropertyByName(child, propertyPathParser.Property);
                if (depend != null)
                {
                    return new SourceDependencyObjectStateManager(depend, child, propertyPathParser.Optional);
                }
                var trackingEvent = ReflectionUtility.FindEventByName(child, propertyPathParser.Property!);
                if (trackingEvent != null)
                {
                    return new SourceEventStateManager(child, trackingEvent, propertyPathParser.Optional);
                }
            }
            catch (Exception ex)
            {
                throw BuildException(propertyPathParser, ex);
            }
            throw new BindingPathException(BindingIssueKind.PathNotFound, $"'{propertyPathParser.Property}' is neither a property nor an event of {child.GetType().Name}");
        }

        private static Exception BuildException(PropertyPathParser parser, Exception? exception = null)
        {
            var message = $"[FastBinding] Could not find {parser.Property} on control {parser.Source}.";
            throw exception == null? new InvalidOperationException(message) : new InvalidOperationException(message, exception);
        }
    }
}
