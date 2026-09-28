using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Owns one native CEF browser and its shared texture. Has no Unity lifecycle of its own:
    /// the owning MonoBehaviour runs <see cref="Create"/> as a coroutine, calls
    /// <see cref="PollCallbacks"/> each frame, and calls <see cref="Close"/> when disabled.
    /// </summary>
    internal sealed class CeffyBrowser
    {
        private static readonly Dictionary<int, CeffyBrowser> activeBrowsers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => activeBrowsers.Clear();

        private int browserId = -1;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public Texture2D Texture { get; private set; }
        public bool IsCreated => browserId >= 0;

        public event Action<string> MessageReceived;
        public event Action<LogLevel, string, string, int> ConsoleMessage;
        public event Action<int, int, DragOperation> DragStarted;

        public CeffyBrowser(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
        }

        /// <summary>
        /// Creates the native browser once the runtime is ready and exposes its texture.
        /// </summary>
        public IEnumerator Create(string url)
        {
            yield return new WaitUntil(() => WebBrowserRuntime.Instance.IsReady);

            browserId = WebBrowserRuntime.Instance.CreateBrowser(Width, Height, url);
            if (browserId < 0)
            {
                Debug.LogError("[Ceffy] CreateBrowser returned an invalid ID.");
                yield break;
            }
            activeBrowsers[browserId] = this;

            // Ceffy_EnsureInitialized returns 0 until CEF's async CreateBrowser finishes
            // (e.g. it's still closing the previous browser after a scene reload).
            float timeout = 10f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (NativeBridge.Ceffy_EnsureInitialized(browserId) != 0)
                    break;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            IntPtr sharedHandle = NativeBridge.Ceffy_GetSharedHandle(browserId);
            if (sharedHandle != IntPtr.Zero)
                ReplaceTexture(sharedHandle);
            else
                Debug.LogWarning("[Ceffy] Ceffy_GetSharedHandle returned null. Texture will not be available.");
        }

        public void Close()
        {
            if (browserId >= 0)
            {
                activeBrowsers.Remove(browserId);
                NativeBridge.Ceffy_CloseBrowser(browserId);
                browserId = -1;
            }
            if (Texture)
            {
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }

        public void Resize(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            if (browserId < 0)
                return;

            IntPtr newHandle = NativeBridge.Ceffy_Resize(browserId, Width, Height);
            if (newHandle != IntPtr.Zero)
                ReplaceTexture(newHandle);
        }

        private void ReplaceTexture(IntPtr sharedHandle)
        {
            if (Texture)
                UnityEngine.Object.Destroy(Texture);
            Texture = D3D11SharedTexture.CreateFromSharedHandle(sharedHandle, Width, Height);
        }

        /// <summary>
        /// Refresh the external texture reference so Unity picks up GPU updates.
        /// </summary>
        public void UpdateTexture()
        {
            if (Texture)
                Texture.UpdateExternalTexture(Texture.GetNativeTexturePtr());
        }

        public void RequestFrame()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_RequestFrame(browserId);
        }

        /// <summary>
        /// Drains the shared native callback queue and dispatches to the owning browsers.
        /// </summary>
        public static void PollCallbacks()
        {
            if (activeBrowsers.Count == 0)
                return;

            for (int i = 0; i < 100; i++)
            {
                if (!NativeBridge.PollCallback(out int type, out int cbBrowserId, out string data))
                    break;
                if (activeBrowsers.TryGetValue(cbBrowserId, out var target))
                    target.DispatchCallback(type, data);
            }
        }

        private void DispatchCallback(int type, string data)
        {
            if (type == 1)
                HandleConsoleMessageJson(data);
            else if (type == 2)
                MessageReceived?.Invoke(data);
            else if (type == 3)
                HandleDragStart(data);
            else if (type == 4)
                Debug.Log(data);
        }

        private void HandleConsoleMessageJson(string json)
        {
            try
            {
                int level = ExtractJsonInt(json, "level");
                string message = ExtractJsonString(json, "message");
                string source = ExtractJsonString(json, "source");
                int line = ExtractJsonInt(json, "line");
                ConsoleMessage?.Invoke((LogLevel)level, message, source, line);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Ceffy] Failed to parse console message: {ex.Message}");
            }
        }

        private void HandleDragStart(string json)
        {
            try
            {
                int x = ExtractJsonInt(json, "x");
                int y = ExtractJsonInt(json, "y");
                int ops = ExtractJsonInt(json, "ops");
                DragStarted?.Invoke(x, y, (DragOperation)ops);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Ceffy] Failed to parse drag start: {ex.Message}");
            }
        }

        private static int ExtractJsonInt(string json, string key)
        {
            var pattern = "\"" + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return 0;
            idx += pattern.Length;
            while (idx < json.Length && json[idx] == ' ') idx++;
            int end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (int.TryParse(json.Substring(idx, end - idx), out int val)) return val;
            return 0;
        }

        private static string ExtractJsonString(string json, string key)
        {
            var pattern = "\"" + key + "\":\"";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return "";
            idx += pattern.Length;
            var sb = new StringBuilder();
            for (int i = idx; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    i++;
                    switch (json[i])
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(json[i]); break;
                    }
                }
                else if (json[i] == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(json[i]);
                }
            }
            return sb.ToString();
        }

        #region Navigation and messaging

        public void Navigate(string url)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_Navigate(browserId, url);
        }

        public void ExecuteJS(string code)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_ExecuteJS(browserId, code);
        }

        public void SendMessage(string message)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMessage(browserId, message);
        }

        public void SetZoomLevel(double zoomLevel)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SetZoomLevel(browserId, zoomLevel);
        }

        public double GetZoomLevel() => browserId >= 0 ? NativeBridge.Ceffy_GetZoomLevel(browserId) : 0.0;

        #endregion

        #region Input

        public void SendMouseMove(int x, int y, EventFlags modifiers)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseMove(browserId, x, y, (int)modifiers);
        }

        public void SendMouseLeave()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseLeave(browserId);
        }

        public void SendMouseClick(int x, int y, MouseButton button, bool isUp, int clickCount, EventFlags modifiers)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseClick(browserId, x, y, (int)button, isUp ? 1 : 0, clickCount, (int)modifiers);
        }

        public void SendMouseWheel(int x, int y, int deltaX, int deltaY, EventFlags modifiers)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseWheel(browserId, x, y, deltaX, deltaY, (int)modifiers);
        }

        public void SendDragTargetEnter(int x, int y, EventFlags modifiers, DragOperation allowedOps)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDragEnter(browserId, x, y, (int)modifiers, (int)allowedOps);
        }

        public void SendDragTargetOver(int x, int y, EventFlags modifiers, DragOperation allowedOps)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDragOver(browserId, x, y, (int)modifiers, (int)allowedOps);
        }

        public void SendDragTargetLeave()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDragLeave(browserId);
        }

        public void SendDragTargetDrop(int x, int y, EventFlags modifiers)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDrop(browserId, x, y, (int)modifiers);
        }

        public void DragSourceEndedAt(int x, int y)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragSourceEndedAt(browserId, x, y);
        }

        public void DragSourceSystemDragEnded()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragSourceSystemDragEnded(browserId);
        }

        public void SendKeyEvent(KeyEventType eventType, int windowsKeyCode, int nativeKeyCode, EventFlags modifiers, bool isSystemKey)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendKeyEvent(browserId, (int)eventType, windowsKeyCode, nativeKeyCode, (int)modifiers, isSystemKey ? 1 : 0);
        }

        public void KeyDown(KeyCode key, EventFlags modifiers)
        {
            if (browserId >= 0)
                NativeBridge.SendKeyDown(browserId, key, modifiers);
        }

        public void KeyUp(KeyCode key, EventFlags modifiers)
        {
            if (browserId >= 0)
                NativeBridge.SendKeyUp(browserId, key, modifiers);
        }

        #endregion
    }
}
