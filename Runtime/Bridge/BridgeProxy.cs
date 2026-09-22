using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Ceffy.Bridge
{
    /// <summary>
    /// Intercepts C# interface method calls and forwards them as JSON-RPC
    /// messages to the JS side. Used for calling UI Methods from Unity.
    /// </summary>
    public class BridgeProxy<T> : DispatchProxy where T : class
    {
        private CeffyBridge bridge;

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            var returnType = targetMethod.ReturnType;
            var parameters = args == null ? null : new List<object>(args);
            var jsMethodName = BridgeNaming.ToCamelCase(targetMethod.Name);

            if (returnType == typeof(void) || returnType == typeof(Task))
            {
                bridge.SendToJs(jsMethodName, parameters);
                return returnType == typeof(Task) ? Task.CompletedTask : null;
            }

            var resultType = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                ? returnType.GetGenericArguments()[0]
                : returnType;

            var sendMethod = typeof(CeffyBridge)
                .GetMethod(nameof(CeffyBridge.SendToJsWithResponse), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(resultType);

            var task = sendMethod.Invoke(bridge, new object[] { jsMethodName, args });

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                return task;

            // Synchronous return type -- block on the task
            return ((Task)task).GetType().GetProperty("Result").GetValue(task);
        }

        internal static T Create(CeffyBridge bridge)
        {
            var proxy = Create<T, BridgeProxy<T>>() as BridgeProxy<T>;
            proxy.bridge = bridge;
            return proxy as T;
        }
    }
}
