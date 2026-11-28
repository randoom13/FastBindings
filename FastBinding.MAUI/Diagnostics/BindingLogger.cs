using System;

namespace FastBindings.Diagnostics
{
    /// <summary>
    /// What FastBindings tells the application about its bindings: set <see cref="FastBindingsDiagnostics.Logger"/> to an
    /// instance before views load (the bindings' descriptions are taken while XAML loads). Each method is called on the thread the
    /// binding ran on — usually the UI thread, a worker for async sources — so an implementation must be thread-safe. An
    /// exception a method throws is caught and dropped: a logger never breaks a binding. An abstract class, not an interface, so a
    /// method added later breaks no implementation.
    /// </summary>
    public abstract class BindingLogger
    {
        /// <summary>The binding still works, but not as written: a path names nothing, a value does not fit, an ancestor is missing.</summary>
        public virtual void Warning(BindingIssue issue) { }

        /// <summary>Something threw: a getter, a setter, a converter, the target property, an event source, a method target.</summary>
        public virtual void Error(BindingIssue issue) { }

        /// <summary>Neither error nor warning: for tuning (a binding fell back from its fast path to the general one).</summary>
        public virtual void Trace(BindingIssue issue) { }
    }

    /// <summary>How serious a <see cref="BindingIssueKind"/> is; which <see cref="BindingLogger"/> method receives it.</summary>
    public enum BindingIssueSeverity { Trace, Warning, Error }

    /// <summary>What went wrong. One list for every FastBindings package: a package that cannot meet a kind never reports it.</summary>
    public enum BindingIssueKind
    {
        // Error: something threw
        /// <summary>The source property's getter threw; the fallback value (if any) is shown.</summary>
        GetterThrew,
        /// <summary>A two-way binding wrote back and the source property's setter threw.</summary>
        SetterThrew,
        /// <summary>The converter threw while converting a source value for the target.</summary>
        ForwardConverterThrew,
        /// <summary>The converter threw while converting a target value back for the source.</summary>
        BackConverterThrew,
        /// <summary>The target property refused the value: its validation, the wrong thread, or a value its type cannot take.</summary>
        TargetRefusedValue,
        /// <summary>A method target (<c>Target="SetHeight("</c>) threw, or was given a parameter of another type.</summary>
        MethodTargetThrew,
        /// <summary>An event source (<c>$[[this].SizeChanged]</c>) could not be subscribed, or its optional part could not be read.</summary>
        EventSourceFailed,
        /// <summary>An async source's task failed.</summary>
        AsyncSourceFaulted,
        /// <summary>The notification filter threw.</summary>
        NotificationFilterThrew,
        /// <summary>The <c>Sources</c> text could not be parsed; the binding shows nothing from that source.</summary>
        SourcesInvalid,

        // Warning: works, but not as written
        /// <summary>A name in the path is not a property of the object it is read from.</summary>
        PathNotFound,
        /// <summary>The source property has no getter.</summary>
        NoGetter,
        /// <summary>A two-way binding wrote back, and the source property has no setter.</summary>
        NoSetter,
        /// <summary>A two-way binding wrote back a value the source property's type cannot take.</summary>
        WriteBackTypeMismatch,
        /// <summary>A source value the target property's type cannot take, and no converter: the target keeps its value.</summary>
        ValueNotAssignable,
        /// <summary><c>DataContextSource</c>, or a source control named by type, level or name, was not found.</summary>
        AncestorNotFound,
        /// <summary>The parent element could not be found (MAUI: read through its non-public API).</summary>
        ParentNotFound,

        // Trace
        /// <summary>A fast accessor or direct pusher could not be made: the binding works through the general path.</summary>
        FastPathUnavailable,

        // Error (appended: the kinds before keep their numbers)
        /// <summary>
        /// A value change handed from a worker thread to the UI thread failed there with an exception nothing else caught: no caller
        /// is left to catch it, and without this report it reached the dispatcher's unhandled-exception handler.
        /// </summary>
        UpdateFailed,
    }
}
