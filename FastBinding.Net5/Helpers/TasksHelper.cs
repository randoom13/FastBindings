﻿using System.Threading.Tasks;
namespace FastBindings.Helpers
{
    internal class TasksHelper
    {
        private const string ExpectedTaskType = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder";

        public static bool IsTask(object? obj)
        {
            if (obj == null || !obj.GetType().IsGenericType)
                return false;

            var type = obj.GetType().GetGenericTypeDefinition();
            return type == typeof(Task<>) || type.FullName?.StartsWith(ExpectedTaskType) == true;
        }

        public static async Task<object?> GetResult(Task value)
        {
            await ((Task)value!);
            var type = value.GetType();
            var propertyInfo = type.GetProperty("Result");
            var result = propertyInfo?.GetValue(value);
            return result;
        }
    }
}
