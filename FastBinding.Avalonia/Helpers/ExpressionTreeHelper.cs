using System;
using FastBindings;
using System.Linq.Expressions;
using System.Reflection;

namespace FastBindings.Helpers
{
    internal static class ExpressionTreeHelper
    {
        public static Func<object, object?>? CreateGetter(PropertyInfo? fieldInfo, Type targetType, object target)
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
            // a public method of that name taking the value, else one taking nothing (an event that is only a signal); GetMethod alone
            // threw on overloads and a method without a parameter failed at GetParameters()[0]
            MethodInfo? methodInfo = null, without = null;
            foreach (var candidate in subscriberType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (candidate.Name != methodName || candidate.IsGenericMethodDefinition)
                    continue;
                var count = candidate.GetParameters().Length;
                if (count == 1 && methodInfo == null)
                    methodInfo = candidate;
                else if (count == 0 && without == null)
                    without = candidate;
            }
            if (methodInfo == null && without != null)
            {
                var target = Expression.Parameter(typeof(object), "subscriber");
                var ignored = Expression.Parameter(typeof(object), "value");
                return FastBindingsDiagnostics.Compile(Expression.Lambda<Action<object, object?>>(
                    Expression.Call(Expression.Convert(target, subscriberType), without), target, ignored));
            }
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
