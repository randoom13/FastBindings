using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using FastBindings.Diagnostics;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>
    /// What FastBindings reports through <see cref="FastBindingsDiagnostics.Logger"/>: one test per kind a WPF binding can meet,
    /// each provoked on the <see cref="Faults"/> view, and the rules of reporting (descriptions, repeats, a throwing logger, none set).
    /// </summary>
    [Collection(nameof(DiagnosticsCollection))]   // the logger is one for the process
    public class BindingLoggerTests
    {
        private static (CollectingLogger Log, Shown<Faults> Ui, Faulty Vm) Show()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            var vm = new Faulty();
            return (log, Ui.Show(new Faults(), vm), vm);
        }

        private static BindingIssue Single(CollectingLogger log, BindingIssueKind kind, string element) =>
            log.Issues.SingleOrDefault(i => i.Kind == kind && i.ElementName == element)
            ?? throw new Xunit.Sdk.XunitException($"no {kind} on '{element}'; reported: {string.Join(Environment.NewLine, log.Issues)}");

        private static void Type(TextBox box, string text)
        {
            box.Text = text;
            Ui.Settle();
        }

        [WpfFact]
        public void GetterThrew()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                var issue = Single(log, BindingIssueKind.GetterThrew, "Getter");
                Assert.IsType<InvalidOperationException>(issue.Exception);
                Assert.Equal(BindingIssueSeverity.Error, issue.Severity);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void SetterThrew()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Type(ui.Find<TextBox>("Setter"), "typed");
                Assert.IsType<ArgumentException>(Single(log, BindingIssueKind.SetterThrew, "Setter").Exception);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void ForwardConverterThrew()
        {
            var (log, ui, _) = Show();
            using (ui)
                Assert.IsType<FormatException>(Single(log, BindingIssueKind.ForwardConverterThrew, "Forward").Exception);
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void BackConverterThrew()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Type(ui.Find<TextBox>("Back"), "typed");
                var issue = Single(log, BindingIssueKind.BackConverterThrew, "Back");
                Assert.IsType<FormatException>(issue.Exception);
                Assert.Equal("typed", issue.Value);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void TargetRefusedValue()
        {
            var (log, ui, vm) = Show();
            using (ui)
            {
                vm.Row = -1;   // Grid.Row's validation refuses it: SetValue throws
                Ui.Settle();
                Assert.IsType<ArgumentException>(Single(log, BindingIssueKind.TargetRefusedValue, "Refused").Exception);
                Assert.Equal(-1, Single(log, BindingIssueKind.TargetRefusedValue, "Refused").Value);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void MethodTargetThrew()
        {
            var (log, ui, vm) = Show();
            using (ui)
            {
                ui.Find<Border>("Method").Tag = "tagged";
                Ui.Settle();
                Assert.IsType<NotSupportedException>(Single(log, BindingIssueKind.MethodTargetThrew, "Method").Exception);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void EventSourceFailed()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                ui.Find<Poker>("Event").Poke();
                Ui.Settle();
                Assert.NotNull(Single(log, BindingIssueKind.EventSourceFailed, "Event").Exception);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void AsyncSourceFaulted()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Ui.Settle();
                Assert.IsType<TimeoutException>(Single(log, BindingIssueKind.AsyncSourceFaulted, "Async").Exception);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void NotificationFilterThrew()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Type(ui.Find<TextBox>("Notification"), "typed");
                Assert.IsType<InvalidOperationException>(Single(log, BindingIssueKind.NotificationFilterThrew, "Notification").Exception);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void SourcesInvalid()
        {
            var (log, ui, _) = Show();
            using (ui)
                Assert.Contains("$[[Nowhere", Single(log, BindingIssueKind.SourcesInvalid, "Invalid").Detail);
            FastBindingsDiagnostics.Logger = null;
        }

        /// <summary>A misspelled name shows the fallback and says so — it showed "False" (the getter returned false for "not found").</summary>
        [WpfFact]
        public void PathNotFound_shows_the_fallback()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Assert.Contains("'Misspeled' is not a property of Faulty", Single(log, BindingIssueKind.PathNotFound, "Missing").Detail);
                Assert.Equal("fallback", ui.Find<TextBlock>("Missing").Text);
                Assert.Equal(BindingIssueSeverity.Warning, Single(log, BindingIssueKind.PathNotFound, "Missing").Severity);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        /// <summary>A ConverterPath that names no converter: the value passes unconverted, and that is said.</summary>
        [WpfFact]
        public void PathNotFound_for_a_converter_path()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Assert.Contains("ConverterPath 'A' on Faulty is String, no converter", Single(log, BindingIssueKind.PathNotFound, "NoConverter").Detail);
                Assert.Equal("a", ui.Find<TextBlock>("NoConverter").Text);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void PathNotFound_without_a_logger_shows_no_False()
        {
            FastBindingsDiagnostics.Logger = null;
            using var ui = Ui.Show(new Faults(), new Faulty());
            Assert.NotEqual("False", ui.Find<TextBlock>("Missing").Text);
        }

        [WpfFact]
        public void NoGetter()
        {
            var (log, ui, _) = Show();
            using (ui)
                Single(log, BindingIssueKind.NoGetter, "NoGetter");
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void NoSetter()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                Type(ui.Find<TextBox>("NoSetter"), "typed");
                Assert.Equal("typed", Single(log, BindingIssueKind.NoSetter, "NoSetter").Value);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void WriteBackTypeMismatch()
        {
            var (log, ui, vm) = Show();
            using (ui)
            {
                Type(ui.Find<TextBox>("Mismatch"), "seven");
                Assert.Contains("Nullable", Single(log, BindingIssueKind.WriteBackTypeMismatch, "Mismatch").Detail);
                Assert.Null(vm.Count);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void ValueNotAssignable()
        {
            var (log, ui, _) = Show();
            using (ui)
                Assert.Equal("a", Single(log, BindingIssueKind.ValueNotAssignable, "NotAssignable").Value);
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void AncestorNotFound()
        {
            var (log, ui, _) = Show();
            using (ui)
                Assert.Contains("ListBox/1", Single(log, BindingIssueKind.AncestorNotFound, "Ancestor").Detail);
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void A_binding_that_works_reports_nothing()
        {
            var (log, ui, vm) = Show();
            using (ui)
            {
                vm.A = "changed";
                Ui.Settle();
                Assert.DoesNotContain(log.Issues, i => i.ElementName == "Fine");
                Assert.Equal("changed", ui.Find<TextBlock>("Fine").Text);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        /// <summary>The description names the view's file, the target and the parameters as written.</summary>
        [WpfFact]
        public void An_issue_says_where_and_what_the_binding_is()
        {
            var (log, ui, _) = Show();
            using (ui)
            {
                var issue = Single(log, BindingIssueKind.GetterThrew, "Getter");
                // WPF names a compiled view by its BAML resource, in lower case (pack://…/views/faults.xaml)
                Assert.Equal("faults.xaml", issue.Binding.ViewName, ignoreCase: true);
                Assert.Equal("TextBlock", issue.Binding.TargetType);
                Assert.Equal("Text", issue.Binding.TargetProperty);
                Assert.Equal("CommonBinding", issue.Binding.BindingKind);
                Assert.Equal("Throws", issue.Binding.Parameters);
                Assert.Equal(typeof(Faulty), issue.DataContextType);
                Assert.Contains("TextBlock.Text ← {CommonBinding Throws}", issue.Message);
                Assert.Contains("element 'Getter'", issue.Message);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        /// <summary>The same declaration shown many times is one description: a row template's binding reports per declaration.</summary>
        [WpfFact]
        public void Equal_declarations_share_one_description_and_repeats_are_counted()
        {
            var log = new CollectingLogger();
            FastBindingsDiagnostics.Logger = log;
            for (var i = 0; i < 12; i++)
            {
                using var ui = Ui.Show(new Faults(), new Faulty());
            }
            var getters = log.Issues.Where(i => i.Kind == BindingIssueKind.GetterThrew && i.Binding.Parameters == "Throws").ToList();
            Assert.Single(getters.Select(i => i.Binding.Id).Distinct());
            Assert.Equal(new[] { 1, 10 }, getters.Select(i => i.Occurrences).ToArray());   // the 1st and the 10th of 12
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void A_logger_that_throws_breaks_no_binding()
        {
            var logger = new ThrowingLogger();
            FastBindingsDiagnostics.Logger = logger;
            var vm = new Faulty();
            using (var ui = Ui.Show(new Faults(), vm))
            {
                vm.A = "still bound";
                Ui.Settle();
                Assert.Equal("still bound", ui.Find<TextBlock>("Fine").Text);
                Assert.True(logger.Calls > 0);
            }
            FastBindingsDiagnostics.Logger = null;
        }

        [WpfFact]
        public void Without_a_logger_nothing_is_described()
        {
            FastBindingsDiagnostics.Logger = null;
            var view = new Faults();
            using var ui = Ui.Show(view, new Faulty());
            // nothing to assert beyond: no exception, and the bindings work
            Assert.Equal("a", ui.Find<TextBlock>("Fine").Text);
        }

        /// <summary>
        /// Every error path reports through <c>BindingLog</c>: no Debug.Write is left in the package (removed in Release builds, it
        /// was where every failure went; a Debug.Assert stopped a Debug build on a binding to a missing ancestor), and no catch block ignores what it caught.
        /// </summary>
        [Fact]
        public void Every_error_path_reports_through_the_logger()
        {
            const string Package = "FastBinding.Net5";
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, Package)))
                dir = dir.Parent;
            Assert.NotNull(dir);
            var offenders = Directory.EnumerateFiles(Path.Combine(dir!.FullName, Package), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .SelectMany(f =>
                {
                    var text = File.ReadAllText(f);
                    var name = Path.GetFileName(f);
                    var debug = Regex.Matches(text, @"\b(Debug|Trace)\.(Write\w*|Assert|Fail|Print)").Select(m => name + ": " + m.Value);
                    // a catch block whose body neither reports, rethrows, nor hands the exception on
                    var silent = Regex.Matches(text, @"catch\s*(\([^)]*\))?\s*\{(?<body>(?:[^{}]|\{[^{}]*\})*)\}")
                        .Where(m => name != "BindingLog.cs" && name != "BindingDescriptions.cs"
                            && !Regex.IsMatch(m.Groups["body"].Value, @"BindingLog\.Report|\bthrow\b|Wrap\(|InvalidSourceStateManager\(ex\)"))
                        .Select(m => name + ": " + Regex.Replace(m.Value, @"\s+", " "));
                    // a report's detail is text made by the call: only after checking a logger is set (else unbinding 6 000 bindings
                    // made 0.6 MB of messages nobody read)
                    var unguarded = name == "BindingLog.cs" ? Enumerable.Empty<string>()
                        : Regex.Matches(text, @"(?m)^.*BindingLog\.Report\(.*$").Where(m => !m.Value.Contains("BindingLog.IsOn)"))
                            .Select(m => name + ": unguarded " + m.Value.Trim());
                    return debug.Concat(silent).Concat(unguarded);
                })
                .ToList();
            Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        }
    }
}
