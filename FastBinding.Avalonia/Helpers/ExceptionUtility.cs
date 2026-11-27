using FastBindings.Diagnostics;
using System;
using System.Threading.Tasks;

namespace FastBindings.Helpers
{
    internal static class ExceptionUtility
    {
        internal static object? Handle(Func<object?> func, bool isWrapException, string errorMessage, BindingIssueKind kind = BindingIssueKind.GetterThrew, BindingDescription? binding = null)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return Wrap(ex, isWrapException, errorMessage, kind, binding);
            }
        }

        /// <summary>What <see cref="Handle"/> returns for an exception, for callers that catch it themselves (no closure per call).</summary>
        internal static object Wrap(Exception ex, bool isWrapException, string errorMessage, BindingIssueKind kind = BindingIssueKind.GetterThrew, BindingDescription? binding = null)
        {
            if (BindingLog.IsOn) BindingLog.Report(ex is BindingPathException path ? path.Kind : kind, ex, errorMessage, binding: binding);
            return isWrapException ? new ExceptionHolder(ex) : ex;
        }

        internal static async Task<object?> AsyncHandle(Func<object?> func, bool isWrapException, string errorMessage, BindingDescription? binding = null)
        {
            object? value;
            try
            {
                value = func();
            }
            catch (Exception ex)
            {
                return Wrap(ex, isWrapException, errorMessage, BindingIssueKind.GetterThrew, binding);
            }
            try
            {
                return TasksHelper.IsTask(value) ? await TasksHelper.GetResult((Task)value!) : value;
            }
            catch (Exception ex)
            {
                return Wrap(ex, isWrapException, errorMessage, BindingIssueKind.AsyncSourceFaulted, binding);
            }
        }
    }
}
