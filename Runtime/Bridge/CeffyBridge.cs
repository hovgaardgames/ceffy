using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ceffy.Bridge
{
    /// <summary>
    /// Bidirectional RPC bridge between Unity (C#) and a web page running in a
    /// <see cref="CeffyInstance"/>. Handles JSON-RPC messaging, automatic JS
    /// injection, and typed proxy generation.
    /// </summary>
    public class CeffyBridge
    {
        private readonly ICeffyBridgeTransport transport;
        private readonly BridgeDispatcher dispatcher;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<object>> pendingResponses = new();
        private int nextMessageId = 1;
        private bool coreInjected;
        private bool disposed;

        private readonly List<string> pendingInjections = new();

        public CeffyBridge(CeffyInstance ceffyInstance) : this(new CeffyInstanceTransport(ceffyInstance))
        {
        }

        internal CeffyBridge(ICeffyBridgeTransport transport)
        {
            this.transport = transport;
            this.dispatcher = new BridgeDispatcher(SendRawToJs);
            transport.MessageReceived += OnMessageFromJs;
        }

        /// <summary>
        /// Must be called from Update() on the main thread to process incoming
        /// messages from JS and dispatch them to bound implementations.
        /// </summary>
        public void ProcessMessages()
        {
            dispatcher.ProcessMessages();
        }

        /// <summary>
        /// Registers a C# implementation of a Unity Methods interface so that
        /// JS can call its methods. Also injects callable stubs on ceffy.unity.
        /// </summary>
        public void Bind<T>(T implementation) where T : class
        {
            dispatcher.Bind(implementation);

            var js = new System.Text.StringBuilder();
            foreach (var m in typeof(T).GetMethods())
                js.AppendLine(BridgeJavaScript.GenerateUnityMethodStub(m.Name));

            InjectJs(js.ToString());
        }

        /// <summary>
        /// Creates a typed proxy for a UI Methods interface. Calls on the
        /// returned object are serialized as JSON-RPC and sent to JS.
        /// Also injects placeholder keys on ceffy.ui.
        /// </summary>
        public T CreateProxy<T>() where T : class
        {
            var js = new System.Text.StringBuilder();
            foreach (var m in typeof(T).GetMethods())
                js.AppendLine(BridgeJavaScript.GenerateUiMethodPlaceholder(m.Name));

            InjectJs(js.ToString());

            return BridgeProxy<T>.Create(this);
        }

        /// <summary>
        /// Inject the core bridge runtime into the page. Call this once the
        /// browser/page is ready. If called multiple times, subsequent calls
        /// are no-ops (the JS itself has an idempotency guard).
        /// </summary>
        public void InjectBridgeRuntime()
        {
            transport.ExecuteJavaScript(BridgeJavaScript.CoreRuntime);
            coreInjected = true;

            foreach (var pending in pendingInjections)
                transport.ExecuteJavaScript(pending);
            pendingInjections.Clear();
        }

        public void Dispose()
        {
            disposed = true;
            transport.MessageReceived -= OnMessageFromJs;
        }

        #region Internal send methods (used by BridgeProxy)

        internal void SendToJs(string method, List<object> parameters)
        {
            SendRawToJs(BridgeSerializer.SerializeRequest(method, parameters, null));
        }

        internal async Task<T> SendToJsWithResponse<T>(string method, object[] parameters)
        {
            var id = nextMessageId++;
            var tcs = new TaskCompletionSource<object>();
            pendingResponses[id] = tcs;

            SendRawToJs(BridgeSerializer.SerializeRequest(
                method,
                parameters == null ? null : new List<object>(parameters),
                id));

            try
            {
                var result = await tcs.Task;
                if (result is T typed) return typed;
                return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(result));
            }
            catch
            {
                pendingResponses.TryRemove(id, out _);
                throw;
            }
        }

        #endregion

        #region Message handling

        private void OnMessageFromJs(string json)
        {
            if (TryHandleResponse(json) || TryHandleError(json))
                return;

            dispatcher.Enqueue(json);
        }

        private bool TryHandleResponse(string json)
        {
            if (!json.Contains("\"result\"")) return false;
            var id = BridgeSerializer.ExtractIntField(json, "id");
            if (!id.HasValue) return false;
            if (!pendingResponses.TryRemove(id.Value, out var tcs)) return false;
            tcs.SetResult(BridgeSerializer.ExtractResult(json));
            return true;
        }

        private bool TryHandleError(string json)
        {
            if (!json.Contains("\"error\"")) return false;
            var id = BridgeSerializer.ExtractIntField(json, "id");
            if (!id.HasValue) return false;
            if (!pendingResponses.TryRemove(id.Value, out var tcs)) return false;
            var error = BridgeSerializer.ExtractStringField(json, "error");
            tcs.SetException(new Exception(error ?? "Unknown bridge error"));
            return true;
        }

        private void SendRawToJs(string json)
        {
            if (disposed) return;
            transport.SendMessage(json);
        }

        private void InjectJs(string js)
        {
            if (coreInjected)
                transport.ExecuteJavaScript(js);
            else
                pendingInjections.Add(js);
        }

        #endregion

        private class CeffyInstanceTransport : ICeffyBridgeTransport
        {
            private readonly CeffyInstance ceffyInstance;

            public event Action<string> MessageReceived
            {
                add => ceffyInstance.OnMessageFromCeffy += value;
                remove => ceffyInstance.OnMessageFromCeffy -= value;
            }

            public CeffyInstanceTransport(CeffyInstance ceffyInstance)
            {
                this.ceffyInstance = ceffyInstance;
            }

            public void ExecuteJavaScript(string javaScript)
            {
                ceffyInstance.ExecuteJS(javaScript);
            }

            public void SendMessage(string message)
            {
                ceffyInstance.SendToCeffy(message);
            }
        }
    }
}
