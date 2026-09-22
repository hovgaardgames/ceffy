using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Ceffy.Bridge
{
    /// <summary>
    /// Receives incoming JSON messages from JS, resolves methods across all
    /// bound Unity Methods interfaces, invokes the implementation, and sends
    /// back responses.
    /// </summary>
    internal class BridgeDispatcher
    {
        private readonly Dictionary<string, (MethodInfo method, object target)> methodMap = new();
        private readonly ConcurrentQueue<string> incomingMessages = new();
        private readonly Action<string> sendToJs;

        public BridgeDispatcher(Action<string> sendToJs)
        {
            this.sendToJs = sendToJs;
        }

        public void Bind<T>(T implementation) where T : class
        {
            var interfaceType = typeof(T);
            foreach (var method in interfaceType.GetMethods())
            {
                var jsMethodName = BridgeNaming.ToCamelCase(method.Name);
                if (methodMap.ContainsKey(jsMethodName))
                {
                    Debug.LogWarning($"[Ceffy Bridge] Method '{jsMethodName}' is already bound. Skipping duplicate from {interfaceType.Name}.");
                    continue;
                }
                methodMap[jsMethodName] = (method, implementation);
            }
        }

        public void Enqueue(string messageJson)
        {
            incomingMessages.Enqueue(messageJson);
        }

        public void ProcessMessages()
        {
            while (incomingMessages.TryDequeue(out var json))
            {
                ProcessMessage(json);
            }
        }

        private void ProcessMessage(string json)
        {
            TryHandleAsRequest(json);
        }

        private bool TryHandleAsRequest(string json)
        {
            var method = BridgeSerializer.ExtractStringField(json, "method");
            if (string.IsNullOrEmpty(method))
                return false;

            var id = BridgeSerializer.ExtractIntField(json, "id");
            InvokeMethod(method, json, id);
            return true;
        }

        private async void InvokeMethod(string methodName, string json, int? id)
        {
            try
            {
                if (!methodMap.TryGetValue(methodName, out var entry))
                {
                    Debug.LogError($"[Ceffy Bridge] Method '{methodName}' not found in any bound interface.");
                    if (id.HasValue)
                        sendToJs(BridgeSerializer.SerializeError(id.Value, $"Method '{methodName}' not found"));
                    return;
                }

                var parameters = BridgeSerializer.ExtractParams(json);
                var args = BridgeSerializer.ConvertParameters(entry.method, parameters);
                var result = entry.method.Invoke(entry.target, args);

                if (result is Task task)
                {
                    await task;
                    result = task.GetType().IsGenericType
                        ? task.GetType().GetProperty("Result")?.GetValue(task)
                        : null;
                }

                if (id.HasValue)
                    sendToJs(BridgeSerializer.SerializeResponse(id.Value, result));
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                Debug.LogException(inner);
                if (id.HasValue)
                    sendToJs(BridgeSerializer.SerializeError(id.Value, inner.Message));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (id.HasValue)
                    sendToJs(BridgeSerializer.SerializeError(id.Value, ex.Message));
            }
        }

        public IReadOnlyDictionary<string, (MethodInfo method, object target)> GetMethodMap() => methodMap;
    }
}
