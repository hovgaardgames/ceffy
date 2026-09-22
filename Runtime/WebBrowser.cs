using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Hosts an off-screen web browser and exposes its texture, navigation, messaging, zoom, and input APIs.
    /// </summary>
    public class WebBrowser : MonoBehaviour
    {
        /// <summary>
        /// Port used for remote debugging. Default: 9222. Set before any WebBrowser is enabled to override.
        /// </summary>
        public static int RemoteDebuggingPort = 9222;

        public string StartUrl = "";
        public float ResizeDelay = 0.25f;

        private static readonly Regex StreamingAssetsUrlRegex = new Regex(@"^streaming-assets:(//)?(.*)$", RegexOptions.IgnoreCase);

        [Tooltip("Log detailed Ceffy lifecycle and diagnostics to the Unity console. Errors and warnings are always logged.")]
        public bool VerboseLogging = false;

        [Tooltip("Enable Chrome DevTools remote debugging on port 9222. Override port via WebBrowser.RemoteDebuggingPort.")]
        public bool RemoteDebugging = false;

        [Tooltip("Automatically resize the browser viewport to match the target RectTransform (if available).")]
        public bool AutoResizeToRectTransform = true;

        [Header("Input")]
        [Tooltip("Enable mouse move events (hover tracking)")]
        public bool InputMouseMove = true;

        [Tooltip("Enable mouse click events (down/up)")]
        public bool InputMouseClick = true;

        [Tooltip("Enable mouse scroll wheel events")]
        public bool InputMouseScroll = true;

        [Tooltip("Enable keyboard input")]
        public bool InputKeyboard = true;

        [Tooltip("Initial width of the browser viewport.")]
        public int Width = 800;
        [Tooltip("Initial height of the browser viewport.")]
        public int Height = 600;

        public event Action<string> OnMessageFromCeffy;
        public event Action<LogLevel, string, string, int> OnConsoleMessage;
        public event Action<int, int> OnViewportResized;

        /// <summary>
        /// Fired when CEF begins an HTML5 drag operation inside the browser.
        /// Parameters: drag-start x, drag-start y, allowed DragOperation flags.
        /// The display component (or any subscriber) must respond by driving the
        /// drag lifecycle via SendDragTarget* and DragSourceEnded*.
        /// </summary>
        public event Action<int, int, DragOperation> OnDragStart;

        /// <summary>
        /// Gets the browser's external texture, or null until native initialization completes.
        /// </summary>
        public Texture2D Texture { get; private set; }

        private static readonly Dictionary<int, WebBrowser> activeBrowsers = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => activeBrowsers.Clear();

        private RectTransform targetRectTransform;
        private RectTransform cachedRectTransform;
        private Canvas cachedCanvas;
        private Camera cachedCanvasCamera;
        private readonly Vector3[] worldCorners = new Vector3[4];
        private int lastViewportWidth = -1;
        private int lastViewportHeight = -1;
        private float resizeTimer;
        private int browserId = -1;
        private int textureWidth;
        private int textureHeight;

        private void OnEnable()
        {
            if (VerboseLogging)
                WebBrowserRuntime.VerboseLogging = true;

            var remotePort = RemoteDebugging ? RemoteDebuggingPort : 0;
            WebBrowserRuntime.Instance.EnsureStarted(remotePort);

            CacheViewportRefs();
            UpdateViewportSizeIfNeeded(true);
            StartCoroutine(Init());
            StartCoroutine(EndOfFrameRequestLoop());
        }

        private IEnumerator Init()
        {
            yield return new WaitUntil(() => WebBrowserRuntime.Instance.IsReady);

            var initialUrl = TransformUrl(StartUrl);
            browserId = WebBrowserRuntime.Instance.CreateBrowser(Width, Height, initialUrl);
            if (browserId < 0)
            {
                Debug.LogError("WebBrowser.Init failed: CreateBrowser returned invalid ID.");
                yield break;
            }
            activeBrowsers[browserId] = this;

            textureWidth = Width;
            textureHeight = Height;

            // Wait for the native browser to be fully ready before exposing the
            // texture.  Ceffy_EnsureInitialized returns 0 immediately when CEF
            // hasn't finished its async CreateBrowser yet (e.g. it's still
            // closing the previous browser after a scene reload).  Yielding
            // between retries keeps the main thread responsive while we wait.
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
            {
                Texture = D3D11SharedTexture.CreateFromSharedHandle(sharedHandle, textureWidth, textureHeight);
            }
            else
            {
                Debug.LogWarning("[Ceffy] Ceffy_GetSharedHandle returned null. Texture will not be available.");
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (browserId >= 0) 
            {
                activeBrowsers.Remove(browserId);
                NativeBridge.Ceffy_CloseBrowser(browserId);
                browserId = -1;
            }
            if (Texture)
            {
                Destroy(Texture);
                Texture = null;
            }
        }
        
        private void Update()
        {
            UpdateViewportSizeIfNeeded(false);
            PollCallbacks();
        }

        private void PollCallbacks()
        {
            if (browserId < 0) return;

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
                HandleMessageFromCeffy(data);
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
                HandleConsoleMessage((LogLevel)level, message, source, line);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Ceffy] Failed to parse console message: {ex.Message}");
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
            var sb = new System.Text.StringBuilder();
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

        /// <summary>
        /// Refresh the external texture reference so Unity picks up GPU updates.
        /// Call from display code to avoid stalling WebBrowser.Update.
        /// </summary>
        public void UpdateTexture()
        {
            if (Texture)
                Texture.UpdateExternalTexture(Texture.GetNativeTexturePtr());
        }

        /// <summary>
        /// Request CEF's next frame after Unity has finished rendering.
        /// Only requests when the browser is active and visible so we don't drive CEF when the UI is hidden.
        /// </summary>
        private IEnumerator EndOfFrameRequestLoop()
        {
            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return waitForEndOfFrame;
                if (browserId >= 0 && gameObject.activeInHierarchy && enabled)
                    NativeBridge.Ceffy_RequestFrame(browserId);
            }
        }

        /// <summary>
        /// Navigate to a URL. Supports special URL schemes:
        /// - streaming-assets:// - loads a local page from StreamingAssets folder
        ///   (e.g., "streaming-assets://MyFolder/index.html")
        /// - file:// - loads a local file directly
        /// - http://, https:// - loads a remote page
        /// </summary>
        public void Navigate(string url)
        {
            var transformedUrl = TransformUrl(url);
            if (WebBrowserRuntime.VerboseLogging)
                Debug.Log($"[Ceffy] Navigating to: {transformedUrl}");
            if (browserId >= 0)
                NativeBridge.Ceffy_Navigate(browserId, transformedUrl);
        }

        /// <summary>
        /// Transforms special URL schemes to browser-compatible URLs.
        /// </summary>
        /// <param name="originalUrl">The original URL which may use special schemes like streaming-assets://</param>
        /// <returns>A URL that the browser can load</returns>
        public static string TransformUrl(string originalUrl)
        {
            if (string.IsNullOrEmpty(originalUrl))
                return originalUrl;
                
            var match = StreamingAssetsUrlRegex.Match(originalUrl);
            if (match.Success)
            {
                var urlPath = match.Groups[2].Captures[0].Value;
                
                var streamingAssetsPath = Application.streamingAssetsPath;
                
                string fileUrl;
                if (streamingAssetsPath.Contains("://"))
                {
                    fileUrl = Path.Combine(streamingAssetsPath, urlPath);
                }
                else
                {
                    var fullPath = Path.GetFullPath(Path.Combine(streamingAssetsPath, urlPath));
                    fileUrl = "file:///" + fullPath.Replace("\\", "/").Replace(" ", "%20");
                }
                
                if (WebBrowserRuntime.VerboseLogging)
                    Debug.Log($"[Ceffy] Transformed streaming-assets URL: {originalUrl} -> {fileUrl}");
                return fileUrl;
            }
            
            return originalUrl;
        }

        public void ExecuteJS(string code)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_ExecuteJS(browserId, code);
        }

        public void SendToCeffy(string message)
        {
            if (browserId < 0)
            {
                Debug.LogWarning("WebBrowser.SendToCeffy called before browser is initialized.");
                return;
            }

            NativeBridge.Ceffy_SendMessage(browserId, message);
        }

        private void HandleMessageFromCeffy(string message)
        {
            try
            {
                OnMessageFromCeffy?.Invoke(message);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void HandleConsoleMessage(LogLevel level, string message, string source, int line)
        {
            try
            {
                OnConsoleMessage?.Invoke(level, message, source, line);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void HandleDragStart(string json)
        {
            try
            {
                int x = ExtractJsonInt(json, "x");
                int y = ExtractJsonInt(json, "y");
                int ops = ExtractJsonInt(json, "ops");
                OnDragStart?.Invoke(x, y, (DragOperation)ops);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Ceffy] Failed to parse drag start: {ex.Message}");
            }
        }

        #region Viewport Resize

        private void CacheViewportRefs()
        {
            cachedRectTransform = targetRectTransform != null
                ? targetRectTransform
                : GetComponent<RectTransform>();

            if (cachedRectTransform)
            {
                cachedCanvas = cachedRectTransform.GetComponentInParent<Canvas>();
                if (cachedCanvas && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    cachedCanvasCamera = cachedCanvas.worldCamera;
                }
            }
        }

        private void UpdateViewportSizeIfNeeded(bool force)
        {
            if (!AutoResizeToRectTransform)
                return;

            if (!force && resizeTimer > 0f)
            {
                resizeTimer -= Time.unscaledDeltaTime;
                if (resizeTimer <= 0f)
                    force = true;
            }

            if (!cachedRectTransform)
                CacheViewportRefs();

            if (!cachedRectTransform)
                return;

            if (!TryGetViewportPixelSize(out int viewportWidth, out int viewportHeight))
                return;

            if (!force && viewportWidth == lastViewportWidth && viewportHeight == lastViewportHeight)
                return;

            lastViewportWidth = viewportWidth;
            lastViewportHeight = viewportHeight;
            if (force)
                ApplyResize();
            else
                resizeTimer = ResizeDelay;
        }

        private void ApplyResize()
        {
            Width = lastViewportWidth;
            Height = lastViewportHeight;
            if (browserId >= 0)
            {
                IntPtr newHandle = NativeBridge.Ceffy_Resize(browserId, Width, Height);
                if (newHandle != IntPtr.Zero)
                {
                    textureWidth = Width;
                    textureHeight = Height;
                    if (Texture) 
                        Destroy(Texture);
                    Texture = D3D11SharedTexture.CreateFromSharedHandle(newHandle, textureWidth, textureHeight);
                }
            }
            OnViewportResized?.Invoke(Width, Height);
        }

        private bool TryGetViewportPixelSize(out int width, out int height)
        {
            width = 0;
            height = 0;

            if (!cachedRectTransform)
                return false;

            cachedRectTransform.GetWorldCorners(worldCorners);
            Vector3 bottomLeft = RectTransformUtility.WorldToScreenPoint(cachedCanvasCamera, worldCorners[0]);
            Vector3 topRight = RectTransformUtility.WorldToScreenPoint(cachedCanvasCamera, worldCorners[2]);

            float w = Mathf.Abs(topRight.x - bottomLeft.x);
            float h = Mathf.Abs(topRight.y - bottomLeft.y);

            if (w <= 0f || h <= 0f)
                return false;

            width = Mathf.Max(1, Mathf.RoundToInt(w));
            height = Mathf.Max(1, Mathf.RoundToInt(h));
            return true;
        }

        #endregion

        #region Zoom

        private const double ZoomBase = 1.2;

        /// <summary>
        /// Set the browser zoom level (0.0 = 100%). Uses Chrome's logarithmic scale:
        /// zoomPercent = 100 * 1.2^zoomLevel. Use <see cref="SetZoomPercent"/> for a simpler API.
        /// </summary>
        public void SetZoomLevel(double zoomLevel)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SetZoomLevel(browserId, zoomLevel);
        }

        /// <summary>
        /// Gets the browser zoom level on Chrome's logarithmic scale.
        /// </summary>
        public double GetZoomLevel()
        {
            return browserId >= 0 ? NativeBridge.Ceffy_GetZoomLevel(browserId) : 0.0;
        }

        public void SetZoomPercent(double percent)
        {
            SetZoomLevel(Math.Log(percent / 100.0) / Math.Log(ZoomBase));
        }

        public double GetZoomPercent()
        {
            return 100.0 * Math.Pow(ZoomBase, GetZoomLevel());
        }

        #endregion
        
        #region Mouse Input
        
        /// <summary>
        /// Send a mouse move event. Coordinates are relative to the browser view (0,0 = top-left).
        /// </summary>
        public void SendMouseMove(int x, int y, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseMove(browserId, x, y, (int)modifiers);
        }
        
        /// <summary>
        /// Send a mouse leave event (cursor left the browser view).
        /// </summary>
        public void SendMouseLeave()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseLeave(browserId);
        }
        
        /// <summary>
        /// Send a mouse button down event.
        /// </summary>
        public void SendMouseDown(int x, int y, MouseButton button = MouseButton.Left, int clickCount = 1, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseClick(browserId, x, y, (int)button, 0, clickCount, (int)modifiers);
        }
        
        /// <summary>
        /// Send a mouse button up event.
        /// </summary>
        public void SendMouseUp(int x, int y, MouseButton button = MouseButton.Left, int clickCount = 1, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseClick(browserId, x, y, (int)button, 1, clickCount, (int)modifiers);
        }
        
        /// <summary>
        /// Send a mouse wheel event.
        /// </summary>
        public void SendMouseWheel(int x, int y, int deltaX, int deltaY, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendMouseWheel(browserId, x, y, deltaX, deltaY, (int)modifiers);
        }
        
        #endregion
        
        #region Drag Input

        /// <summary>
        /// Begin a drag-target operation at the given position. Must be called once
        /// after receiving OnDragStart, before any SendDragTargetOver calls.
        /// </summary>
        public void SendDragTargetEnter(int x, int y, EventFlags modifiers = EventFlags.None, DragOperation allowedOps = DragOperation.Every)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDragEnter(browserId, x, y, (int)modifiers, (int)allowedOps);
        }

        /// <summary>
        /// Update the drag-target position. Call for each mouse move while a drag is active.
        /// </summary>
        public void SendDragTargetOver(int x, int y, EventFlags modifiers = EventFlags.None, DragOperation allowedOps = DragOperation.Every)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDragOver(browserId, x, y, (int)modifiers, (int)allowedOps);
        }

        /// <summary>
        /// Notify CEF that the dragged item has left the browser viewport.
        /// </summary>
        public void SendDragTargetLeave()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDragLeave(browserId);
        }

        /// <summary>
        /// Complete the drop at the given position.
        /// </summary>
        public void SendDragTargetDrop(int x, int y, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragTargetDrop(browserId, x, y, (int)modifiers);
        }

        /// <summary>
        /// Notify CEF that the drag source ended at the given position.
        /// The native side uses the last UpdateDragCursor operation as the result.
        /// Must be called after SendDragTargetDrop (or after SendDragTargetLeave for a cancelled drag).
        /// </summary>
        public void DragSourceEndedAt(int x, int y)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragSourceEndedAt(browserId, x, y);
        }

        /// <summary>
        /// Finalizes the drag and drop session. Must be called after DragSourceEndedAt.
        /// </summary>
        public void DragSourceSystemDragEnded()
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_DragSourceSystemDragEnded(browserId);
        }

        #endregion

        #region Keyboard Input
        
        /// <summary>
        /// Send a keyboard event to the browser.
        /// </summary>
        /// <param name="eventType">Type of key event.</param>
        /// <param name="windowsKeyCode">Windows virtual key code, or character code for Char events.</param>
        /// <param name="nativeKeyCode">Platform-specific native key code (scan code on Windows).</param>
        /// <param name="modifiers">Keyboard modifiers.</param>
        /// <param name="isSystemKey">True if this is a system key (e.g., Alt+key).</param>
        public void SendKeyEvent(KeyEventType eventType, int windowsKeyCode, int nativeKeyCode, EventFlags modifiers = EventFlags.None, bool isSystemKey = false)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendKeyEvent(browserId, (int)eventType, windowsKeyCode, nativeKeyCode, (int)modifiers, isSystemKey ? 1 : 0);
        }
        
        /// <summary>
        /// Send a character input event (for text input).
        /// </summary>
        /// <param name="character">The character to input.</param>
        /// <param name="modifiers">Keyboard modifiers.</param>
        public void SendCharacter(char character, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.Ceffy_SendKeyEvent(browserId, (int)KeyEventType.Char, character, 0, (int)modifiers, 0);
        }
        
        /// <summary>
        /// Send a key-down event from a Unity KeyCode.
        /// </summary>
        public void KeyDown(KeyCode key, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.SendKeyDown(browserId, key, modifiers);
        }
        
        /// <summary>
        /// Send a key-up event from a Unity KeyCode.
        /// </summary>
        public void KeyUp(KeyCode key, EventFlags modifiers = EventFlags.None)
        {
            if (browserId >= 0)
                NativeBridge.SendKeyUp(browserId, key, modifiers);
        }

        #endregion
    }
}
