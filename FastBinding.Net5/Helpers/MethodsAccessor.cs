using System;
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

        private readonly Dictionary<string, Action<object, object?>> _methodActionByNames = new Dictionary<string, Action<object, object?>>();
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
                    return;

                _methodActionByNames[actionMethodName] = methodAction;
            }
            try
            {
                methodAction(target, value);
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] It looks like the method {actionMethodName} requires a different incoming parameter type.");
                throw;
            }
        }
    }

}
