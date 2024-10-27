using FastBindings.Common;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiExample
{
    public class MyCustomExtension : IMarkupExtension
    {
        public string Greeting { get; set; } = "Hello";
        public string Name { get; set; } = "World";

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            return $"{Greeting}, {Name}!";
        }
    }
}
