using System;
using System.Linq.Expressions;
using System.Reflection;


namespace FastBindings.Helpers
{
    internal static class ExpressionTreeHelper
    {
        public static  Func<object, object> CreateGetter(PropertyInfo fieldInfo, Type targetType, object target)
        {
            try
            {
                // Create a parameter expression for the target object (of type object)
                var param = Expression.Parameter(typeof(object), "x");
                // Create an expression to cast the parameter to the correct target type
                var castTarget = Expression.Convert(param, targetType);
                // Access the property using reflection
                var propertyAccess = Expression.Property(castTarget, fieldInfo);
                // Convert the property value to object
                var convert = Expression.Convert(propertyAccess, typeof(object));
                // Create and compile the lambda expression
                return Expression.Lambda<Func<object, object>>(convert, param).Compile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create expression tree for getter");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }

        public static Action<object, object> CreateSetter(PropertyInfo fieldInfo, Type targetType, object target)
        {
            try
            {
                // Create a parameter expression for the target object (of type object)
                var param = Expression.Parameter(typeof(object), "x");
                var valueParam = Expression.Parameter(typeof(object), "value");
                // Create an expression to cast the target object to the correct type
                var castTarget = Expression.Convert(param, targetType);
                // Access the property using reflection
                var propertyAccess = Expression.Property(castTarget, fieldInfo);
                // Create an expression to convert the value and assign it to the property
                var assign = Expression.Assign(propertyAccess, Expression.Convert(valueParam, fieldInfo.PropertyType));
                // Create and compile the lambda expression
                return Expression.Lambda<Action<object, object>>(assign, param, valueParam).Compile();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write($"[FastBinding] Failed to create expression tree for setter");
                System.Diagnostics.Debug.Write(ex);
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }

        public static Action<object, object> CreateMethodAction(Type subscriberType, string methodName)
        {
            // Get the MethodInfo for the method
            MethodInfo methodInfo = subscriberType.GetMethod(methodName);
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
                var lambda = Expression.Lambda<Action<object, object>>(methodCall, subscriberParameter, valueParameter).Compile();
                return lambda;
            }
            return null;
        }
    }
}
