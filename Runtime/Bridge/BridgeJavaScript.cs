namespace Ceffy.Bridge
{
    /// <summary>
    /// Contains the embedded JavaScript bridge runtime injected into web pages.
    /// Phase 1 (CoreRuntime) sets up the RPC plumbing on window.ceffy.
    /// Phase 2 stubs are generated dynamically per Bind/CreateProxy call.
    /// </summary>
    internal static class BridgeJavaScript
    {
        /// <summary>
        /// Core bridge runtime. Sets up window.ceffy with _rpc, _dispatch,
        /// pending promise tracking, and the unity/ui namespaces.
        /// Wrapped in an idempotency guard.
        /// </summary>
        public const string CoreRuntime = @"
(function() {
    if (window.ceffy && window.ceffy._bridgeReady) return;

    var ceffy = window.ceffy || {};
    window.ceffy = ceffy;

    ceffy._bridgeReady = true;
    ceffy._nextId = 1;
    ceffy._pending = {};
    ceffy.unity = ceffy.unity || {};
    ceffy.ui = ceffy.ui || {};

    ceffy._rpc = function(method, args) {
        return new Promise(function(resolve, reject) {
            var id = ceffy._nextId++;
            ceffy._pending[id] = { resolve: resolve, reject: reject };
            var msg = { method: method, id: id };
            if (args && args.length > 0) msg.params = args;
            ceffy.SendToUnity(JSON.stringify(msg));
        });
    };

    ceffy._dispatch = function(raw) {
        var data;
        try {
            data = typeof raw === 'string' ? JSON.parse(raw) : raw;
        } catch(e) {
            console.error('[Ceffy Bridge] Failed to parse message:', e, raw);
            return;
        }

        if (typeof data.id === 'number' && 'result' in data) {
            var p = ceffy._pending[data.id];
            if (p) { delete ceffy._pending[data.id]; p.resolve(data.result); }
            return;
        }

        if (typeof data.id === 'number' && typeof data.error === 'string') {
            var p2 = ceffy._pending[data.id];
            if (p2) { delete ceffy._pending[data.id]; p2.reject(new Error(data.error)); }
            return;
        }

        if (typeof data.method === 'string') {
            var handler = ceffy.ui[data.method];
            if (typeof handler !== 'function') {
                console.warn('[Ceffy Bridge] No handler for UI method:', data.method);
                if (typeof data.id === 'number') {
                    ceffy.SendToUnity(JSON.stringify({ id: data.id, error: 'No handler for ' + data.method }));
                }
                return;
            }
            try {
                var params = data.params || [];
                var result = handler.apply(null, params);
                if (result && typeof result.then === 'function') {
                    result.then(function(r) {
                        if (typeof data.id === 'number')
                            ceffy.SendToUnity(JSON.stringify({ id: data.id, result: r !== undefined ? r : null }));
                    }).catch(function(err) {
                        if (typeof data.id === 'number')
                            ceffy.SendToUnity(JSON.stringify({ id: data.id, error: err.message || String(err) }));
                    });
                } else {
                    if (typeof data.id === 'number')
                        ceffy.SendToUnity(JSON.stringify({ id: data.id, result: result !== undefined ? result : null }));
                }
            } catch(err) {
                console.error('[Ceffy Bridge] Error in UI method', data.method, err);
                if (typeof data.id === 'number')
                    ceffy.SendToUnity(JSON.stringify({ id: data.id, error: err.message || String(err) }));
            }
            return;
        }

        console.warn('[Ceffy Bridge] Unknown message type:', data);
    };

    ceffy.onMessageFromUnity = ceffy._dispatch;

    document.dispatchEvent(new Event('ceffy:ready'));
})();
";

        /// <summary>
        /// Generates JS to register a callable method stub on ceffy.unity.
        /// </summary>
        public static string GenerateUnityMethodStub(string methodName)
        {
            var jsMethodName = BridgeNaming.ToCamelCase(methodName);
            return $"window.ceffy.unity.{jsMethodName} = function() {{ return window.ceffy._rpc('{jsMethodName}', Array.prototype.slice.call(arguments)); }};";
        }

        /// <summary>
        /// Generates JS to register a placeholder on ceffy.ui for a UI method.
        /// </summary>
        public static string GenerateUiMethodPlaceholder(string methodName)
        {
            var jsMethodName = BridgeNaming.ToCamelCase(methodName);
            return $"window.ceffy.ui.{jsMethodName} = window.ceffy.ui.{jsMethodName} || null;";
        }
    }
}
