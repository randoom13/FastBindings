using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using FastBindings;
using System.Reflection;


namespace FastBindings.Helpers
{
    internal static class ExpressionTreeHelper
    {
        public static  Func<object, object?>? CreateGetter(PropertyInfo? fieldInfo, Type targetType, object target)
        {
            // a delegate to the property's getter: no expression tree to compile (FastAccessors)
            if (fieldInfo != null && FastAccessors.Getter(fieldInfo) is { } fast)
            {
                FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._delegates);
                return fast;
            }
            try
            {
                // Create a parameter expression for the target object (of type object)
                var param = Expression.Parameter(typeof(object), "x");
                // Create an expression to cast the parameter to the correct target type
                var castTarget = Expression.Convert(param, targetType);
                // Access the property using reflection
                var propertyAccess = Expression.Property(castTarget, fieldInfo!);
                // Convert the property value to object
                var convert = Expression.Convert(propertyAccess, typeof(object));
                // Create and compile the lambda expression
                return FastBindingsDiagnostics.Compile(Expression.Lambda<Func<object, object?>>(convert, param));
            }
            catch (Exception ex)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.GetterThrew, ex, $"no getter could be made for {fieldInfo?.Name} of {targetType.Name}");
                return null;
            }
        }

        public static Action<object, object?>? CreateSetter(PropertyInfo? fieldInfo, Type targetType, object target)
        {
            if (fieldInfo != null && FastAccessors.Setter(fieldInfo) is { } fast)
            {
                FastBindingsDiagnostics.Count(ref FastBindingsDiagnostics._delegates);
                return fast;
            }
            try
            {
                // Create a parameter expression for the target object (of type object)
                var param = Expression.Parameter(typeof(object), "x");
                var valueParam = Expression.Parameter(typeof(object), "value");
                // Create an expression to cast the target object to the correct type
                var castTarget = Expression.Convert(param, targetType);
                // Access the property using reflection
                var propertyAccess = Expression.Property(castTarget, fieldInfo!);
                // Create an expression to convert the value and assign it to the property
                var assign = Expression.Assign(propertyAccess, Expression.Convert(valueParam, fieldInfo!.PropertyType));
                // Create and compile the lambda expression
                return FastBindingsDiagnostics.Compile(Expression.Lambda<Action<object, object?>>(assign, param, valueParam));
            }
            catch (Exception ex)
            {
                if (Diagnostics.BindingLog.IsOn) Diagnostics.BindingLog.Report(Diagnostics.BindingIssueKind.SetterThrew, ex, $"no setter could be made for {fieldInfo?.Name} of {targetType.Name}");
                return null;
            }
        }

        public static Action<object, object?>? CreateMethodAction(Type subscriberType, string methodName)
        {
            // Get the MethodInfo for the method
            MethodInfo? methodInfo = subscriberType.GetMethod(methodName);
            if (methodInfo != null)
            {
                // Create parameter expressions
                var subscriberParameter = Expression.Parameter(typeof(object), "subscriber");
                var valueParameter = Expression.Parameter(typeof(object), "value");

                // Create the method call expression
                var methodCall = Expression.Call(
                    Expression.Convert(subscriberParameter, subscriberType),
                    methodInfo,
                    methodInfo.GetParameters()[0].ParameterType.IsValueType
                        ? Expression.Convert(valueParameter, methodInfo.GetParameters()[0].ParameterType)
                        : Expression.Convert(valueParameter, typeof(object))
                );
                var lambda = FastBindingsDiagnostics.Compile(Expression.Lambda<Action<object, object>>(methodCall, subscriberParameter, valueParameter));
                return lambda;
            }
            return null;
        }
    }
}
