using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace FastBindings.Helpers
{
    internal struct PropertyPathParser
    {
        //$[[name].dependencyProperty]
        //$[[this].dependencyProperty]
        //$[[ListBox/1].dependencyProperty]
        public const string PropertiesDevider = ";";
        public const string DependencyObjectCurrent = "this";
        public const string TypeLevelSplitter = "/";
        private const string ExpectedSymbols = "$[]";
        private const string Pattern = @"\$\[\[(?<source>.+?)\]\.(?<property>.+?)\]";

        public string? Source { get; private set; }
        public string? Property { get; private set; }
        public string Input { get; private set; }

        public bool IsValid => !string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(Property);

        public static bool NeedApply(string input)
        {
            return ExpectedSymbols.Any(sym => input.Contains(sym));
        }

        internal PropertyPathParser(string input)
        {
            Input = input;
            // Extract name and property
            Source = null;
            Property = null;
            // Regular expression to match the pattern
            var regex = new Regex(Pattern);

            var match = regex.Match(input);
            if (match.Success)
            {
                // Extract name and property
                Source = match.Groups["source"].Value;
                Property = match.Groups["property"].Value;
            }
        }

        internal DependencyObject? CalculateSource(DependencyObject targetObject)
        {
            return CalculateSource(targetObject, Source);
        }

        internal static DependencyObject? CalculateSource(DependencyObject targetObject, string? sourceName)
        {
            if (string.IsNullOrEmpty(sourceName))
            {
                throw new ArgumentException(nameof(sourceName));
            }
            if (sourceName == DependencyObjectCurrent)
            {
                return targetObject;
            }
            DependencyObject? source = null;
            if (sourceName.Contains(TypeLevelSplitter))
            {
                var pars = sourceName.Split(new[] { TypeLevelSplitter }, StringSplitOptions.RemoveEmptyEntries);
                if (pars.Length != 2 || pars.Any(pa => string.IsNullOrEmpty(pa)) || !int.TryParse(pars[1], out int level)
                    || level < 1)
                {
                    Debug.Assert(false, $"{nameof(sourceName)} is impossible to parse");
                    return null;
                }

                source = VisualTreeHelperEx.GetParent(targetObject, level, pars[0]) ??
                  // cover search ListBox control from ListBoxItem
                  VisualTreeHelperEx.GetLogicalParent(targetObject, level, pars[0]);
            }
            else
            {
                var parent = VisualTreeHelperEx.GetParent<DependencyObject>(targetObject);
                source = VisualTreeHelperEx.FindChildByName(parent, sourceName);
            }
            Debug.Assert(source != null, $"{nameof(source)} is not found");
            return source;
        }
    }
}
