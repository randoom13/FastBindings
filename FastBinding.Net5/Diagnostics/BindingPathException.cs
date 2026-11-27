using System;

namespace FastBindings.Diagnostics
{
    /// <summary>
    /// A path that cannot be read as written (a name that is no property, a property without a getter): thrown where it is found,
    /// so the binding shows its fallback, and reported as its own kind where the read is caught — not as a getter that threw.
    /// </summary>
    internal sealed class BindingPathException : InvalidOperationException
    {
        internal BindingPathException(BindingIssueKind kind, string message) : base(message) => Kind = kind;
        internal BindingIssueKind Kind { get; }
    }
}

namespace FastBindings.Diagnostics
{
    /// <summary>A source state manager that reports as the binding it belongs to.</summary>
    internal interface IDescribedSource
    {
        BindingDescription? Description { get; set; }
    }
}
