using System;
using System.Collections.Concurrent;
using System.Text;

namespace FastBindings.Diagnostics
{
    /// <summary>One problem with one binding. Made only when there is a problem and a logger; its text only when read.</summary>
    public sealed class BindingIssue
    {
        private string? _message;

        internal BindingIssue(BindingIssueKind kind, BindingDescription binding, object? target, Type? dataContextType, object? value,
            Exception? exception, string? detail, int occurrences)
        {
            Kind = kind;
            Binding = binding;
            Target = target;
            DataContextType = dataContextType;
            Value = value;
            Exception = exception;
            Detail = detail;
            Occurrences = occurrences;
        }

        public BindingIssueKind Kind { get; }
        public BindingIssueSeverity Severity => SeverityOf(Kind);

        /// <summary>The declaration: what was written in the XAML, and where. The same object for every instance of it.</summary>
        public BindingDescription Binding { get; }

        /// <summary>The control (or other object) the binding sets, when known. FastBindings itself holds it only weakly.</summary>
        public object? Target { get; }

        /// <summary>The type of the DataContext the binding read from, when known.</summary>
        public Type? DataContextType { get; }

        /// <summary>The value involved (what a converter got, what the target or source refused), when there is one.</summary>
        public object? Value { get; }

        public Exception? Exception { get; }

        /// <summary>What FastBindings knows beyond the kind: the name not found, the type expected, the source part that failed.</summary>
        public string? Detail { get; }

        /// <summary>
        /// How many times this declaration has had this kind of problem with this exception type: an issue is reported the 1st, 10th,
        /// 100th, 1 000th … time, so a broken binding in a list's row template does not report once per row and per change.
        /// </summary>
        public int Occurrences { get; }

        /// <summary>The element's Name, when the target is a control that has one.</summary>
        public string? ElementName => (Target as Avalonia.StyledElement)?.Name;

        public string Message => _message ??= Format();

        public override string ToString() => Message;

        private string Format()
        {
            var text = new StringBuilder();
            text.Append(Kind).Append(" · ").Append(Binding);
            if (ElementName is { Length: > 0 } name)
                text.Append(" · element '").Append(name).Append('\'');
            if (DataContextType != null)
                text.Append(" · DataContext ").Append(DataContextType.Name);
            if (Detail is { Length: > 0 })
                text.Append(" · ").Append(Detail);
            if (Exception != null)
                text.Append(" · ").Append(Exception.GetType().Name).Append(": ").Append(Exception.Message);
            if (Occurrences > 1)
                text.Append(" · ×").Append(Occurrences);
            return text.ToString();
        }

        public static BindingIssueSeverity SeverityOf(BindingIssueKind kind) => kind switch
        {
            BindingIssueKind.FastPathUnavailable => BindingIssueSeverity.Trace,
            >= BindingIssueKind.PathNotFound => BindingIssueSeverity.Warning,
            _ => BindingIssueSeverity.Error,
        };
    }

    /// <summary>
    /// A binding's key: everything written in the XAML (the binding's kind and its parameters that differ from their defaults), the
    /// target type and property, and the view's file when the loader names it. Equal declarations share one description and one
    /// <see cref="Id"/>: a row template's binding is one description for all its rows.
    /// </summary>
    public sealed class BindingDescription
    {
        private static readonly ConcurrentDictionary<string, BindingDescription> s_byKey = new ConcurrentDictionary<string, BindingDescription>();
        private static int s_lastId;

        /// <summary>For an issue whose binding was not described: no logger was set while its view loaded.</summary>
        public static BindingDescription Unknown { get; } = new BindingDescription(0, "", "", "", "", null);

        private BindingDescription(int id, string binding, string parameters, string targetType, string targetProperty, string? view)
        {
            Id = id;
            BindingKind = binding;
            Parameters = parameters;
            TargetType = targetType;
            TargetProperty = targetProperty;
            View = view;
        }

        /// <summary>Short, and the same for the same declaration within a run.</summary>
        public int Id { get; }
        /// <summary>The markup extension: "CommonBinding", "AsyncFastBinding", …</summary>
        public string BindingKind { get; }
        /// <summary>The parameters as written, the first unnamed: "Title, Mode=TwoWay, ConverterName=Not".</summary>
        public string Parameters { get; }
        public string TargetType { get; }
        public string TargetProperty { get; }
        /// <summary>The view's file as the XAML loader names it ("avares://App/Views/MainView.axaml"), when it does.</summary>
        public string? View { get; }

        /// <summary>The view's file name alone ("MainView.axaml").</summary>
        public string? ViewName => View == null ? null : View.Substring(View.LastIndexOfAny(new[] { '/', '\\' }) + 1);

        public override string ToString() => ReferenceEquals(this, Unknown)
            ? "(a binding loaded before a logger was set)"
            : $"{(ViewName != null ? ViewName + " · " : "")}{TargetType}.{TargetProperty} ← {{{BindingKind} {Parameters}}}";

        internal static BindingDescription Of(string binding, string parameters, string targetType, string targetProperty, string? view)
        {
            var key = string.Concat(view, "\n", targetType, "\n", targetProperty, "\n", binding, "\n", parameters);
            if (s_byKey.TryGetValue(key, out var known))
                return known;
            return s_byKey.GetOrAdd(key, _ => new BindingDescription(System.Threading.Interlocked.Increment(ref s_lastId), binding, parameters, targetType, targetProperty, view));
        }
    }
}
