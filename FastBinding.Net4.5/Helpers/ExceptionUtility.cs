using System;
using System.Threading.Tasks;

namespace FastBindings.Helpers
{
    internal static class ExceptionUtility
    {
        internal static object? Handle(Func<object?> func, bool isWrapException, string errorMessage)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return Wrap(ex, isWrapException, errorMessage);
            }
        }

        /// <summary>What <see cref="Handle"/> returns for an exception, for callers that catch it themselves (no closure per call).</summary>
        internal static object Wrap(Exception ex, bool isWrapException, string errorMessage)
        {
            System.Diagnostics.Debug.Write(errorMessage);
            System.Diagnostics.Debug.Write(ex);
            System.Diagnostics.Debug.Write(ex.StackTrace);
            return isWrapException ? new ExceptionHolder(ex) : ex;
        }

        internal static async Task<object?> AsyncHandle(Func<object?> func, bool isWrapException, string errorMessage)
        {
            try
            {
                var value = func();
                return TasksHelper.IsTask(value) ? await TasksHelper.GetResult((Task)value!) : value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(errorMessage);
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return isWrapException ? new ExceptionHolder(ex) : ex;
            }
        }
    }
}
