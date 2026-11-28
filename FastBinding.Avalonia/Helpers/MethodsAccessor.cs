using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FastBindings.Helpers
{
    internal class MethodsAccessor
    {
        public Type TargetType { private set; get; }

        public MethodsAccessor(Type targetType)
        {
            if (targetType == null)
                throw new ArgumentException(nameof(targetType));

            TargetType = targetType;
        }

        // Read and filled from any thread a binding runs on.
        private readonly ConcurrentDictionary<string, Action<object, object?>> _methodActionByNames = new ConcurrentDictionary<string, Action<object, object?>>(StringComparer.Ordinal);
        private void Verify(object target, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException($"{nameof(propertyName)}");
            }
            if (target == null)
            {
                throw new ArgumentException($"{nameof(target)}");
            }
            if (target.GetType() != TargetType)
            {
                throw new ArgumentException($"{nameof(target)} should be {TargetType}, not {target.GetType()}");
            }
        }

        public void InvokeMethod(object target, string actionMethodName, object? value)
        {
            Verify(target, actionMethodName);
            Action<object, object?>? methodAction;
            if (!_methodActionByNames.TryGetValue(actionMethodName, out methodAction))
            {
                methodAction = ExpressionTreeHelper.CreateMethodAction(TargetType, actionMethodName);
                if (methodAction == null)
                {
                    if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.PathNotFound, detail: $"no method {actionMethodName} of one parameter or none on {TargetType.Name}", value: value);
                    return;
                }

                methodAction = _methodActionByNames.GetOrAdd(actionMethodName, methodAction);
            }
            // what it throws — its own exception, or an InvalidCastException for a value of another type than its parameter's — is
            // reported where the binding catches it (MethodTargetThrew)
            methodAction(target, value);
        }
    }

}
