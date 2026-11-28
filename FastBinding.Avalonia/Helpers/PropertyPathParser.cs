using Avalonia;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

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
        private static readonly char[] ExpectedSymbolChars = ExpectedSymbols.ToCharArray();
        // one Regex for every source: making one per $[[...]] source cost ~12 µs and ~6.8 KB each (its pattern parsed every time)
        private static readonly Regex SourceRegex = new Regex(Pattern, RegexOptions.CultureInvariant);
        private const string Pattern = @"\$\[\[(?<source>.+?)\]\.(?<property>.+?)\](\[(?<optional>.+?)\])?";
        public string? Source { get; private set; }
        public string? Property { get; private set; }
        public string? Optional { get; private set; }

        public string Input { get; private set; }

        public bool IsValid => !string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(Property);

        public static bool NeedApply(string input)
        {
            return input.IndexOfAny(ExpectedSymbolChars) >= 0;   // no closure or iterator per binding
        }

        internal PropertyPathParser(string input)
        {
            Input = input;
            // Extract name and property
            Source = null;
            Property = null;
            // Regular expression to match the pattern
            var match = SourceRegex.Match(input);
            if (match.Success)
            {
                // Extract name and property
                Source = match.Groups["source"].Value;
                Property = match.Groups["property"].Value;
                Optional = match.Groups["optional"].Value;
            }
        }

        internal AvaloniaObject? CalculateSource(AvaloniaObject targetObject)
        {
            return CalculateSource(targetObject, Source);
        }

        internal static AvaloniaObject? CalculateSource(AvaloniaObject targetObject, string? sourceName)
        {
            if (string.IsNullOrEmpty(sourceName))
            {
                throw new ArgumentException(nameof(sourceName));
            }
            if (sourceName == DependencyObjectCurrent)
            {
                return targetObject;
            }
            AvaloniaObject? source = null;
            if (sourceName.Contains(TypeLevelSplitter))
            {
                var pars = sourceName.Split(new[] { TypeLevelSplitter }, StringSplitOptions.RemoveEmptyEntries);
                if (pars.Length != 2 || pars.Any(pa => string.IsNullOrEmpty(pa)) || !int.TryParse(pars[1], out int level)
                    || level < 1)
                {
                    if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.SourcesInvalid, detail: $"'{sourceName}' is no Type/level (a level from 1)", target: targetObject);
                    return null;
                }

                source = VisualTreeHelperEx.GetParent(targetObject, level, pars[0]) ??
                  // cover search ListBox control from ListBoxItem
                  VisualTreeHelperEx.GetLogicalParent(targetObject, level, pars[0]);
            }
            else
            {
                var parent = VisualTreeHelperEx.GetParent<AvaloniaObject>(targetObject);
                source = VisualTreeHelperEx.FindChildByName(parent, sourceName);
            }
            return source;   // null: not found, reported by the caller (AncestorNotFound), which knows what it was for
        }
    }
}
