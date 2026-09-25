using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Ceffy
{
    /// <summary>
    /// Shows a web page on a uGUI RawImage and exposes navigation, messaging, zoom, and input APIs.
    /// At runtime this component adds the <see cref="RawImage"/> and <see cref="CeffyInstanceView"/> it
    /// renders through, plus a Canvas when the GameObject is not already under one. Those display
    /// components are not serialized into scenes or prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class CeffyInstance : MonoBehaviour
    {
        /// <summary>
        /// Port used for remote debugging. Default: 9222. Set before any CeffyInstance is enabled to override.
        /// </summary>
        public static int RemoteDebuggingPort = 9222;

        /// <summary>
        /// Size of the texture shared by all instances with <see cref="UseSharedInstance"/> enabled.
        /// Set before the first shared instance is enabled to override.
        /// </summary>
        public static int SharedAtlasWidth = 2048;

        /// <inheritdoc cref="SharedAtlasWidth"/>
        public static int SharedAtlasHeight = 2048;

        public string StartUrl = "";

        [Tooltip("Run this page in a browser shared with every other CeffyInstance that has this enabled, " +
                 "instead of starting a dedicated one. Each page stays isolated in its own iframe and gets its own " +
                 "region of one shared texture. Recommended for many small UI elements such as nameplates, labels, " +
                 "and tooltips. Leave off for full-screen or heavy UIs. Zoom is not supported in shared mode. " +
                 "Changes take effect the next time the component is enabled.")]
        public bool UseSharedInstance = false;

        public float ResizeDelay = 0.25f;

        private static readonly Regex StreamingAssetsUrlRegex = new Regex(@"^streaming-assets:(//)?(.*)$", RegexOptions.IgnoreCase);

        [Tooltip("Log detailed Ceffy lifecycle and diagnostics to the Unity console. Errors and warnings are always logged.")]
        public bool VerboseLogging = false;

        [Tooltip("Enable Chrome DevTools remote debugging on port 9222. Override port via CeffyInstance.RemoteDebuggingPort.")]
        public bool RemoteDebugging = false;

        [Tooltip("Automatically resize the browser viewport to match this RectTransform's size in screen pixels. " +
                 "When off, the RectTransform is sized to Width x Height instead.")]
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
        /// Fired when CEF begins an HTML5 drag operation inside the page.
        /// Parameters: drag-start x, drag-start y, allowed DragOperation flags.
        /// The view (or any subscriber) must respond by driving the
        /// drag lifecycle via SendDragTarget* and DragSourceEnded*.
        /// </summary>
        public event Action<int, int, DragOperation> OnDragStart;

        /// <summary>
        /// Gets the texture the page renders into, or null until it is available.
        /// Shared instances return the shared texture; use <see cref="UvRect"/> to sample this page's region.
        /// </summary>
        public Texture2D Texture => isShared ? (sharedSlot != null ? sharedHost.Texture : null) : browser?.Texture;

        /// <summary>
        /// UV rect of this page within <see cref="Texture"/>, already flipped for RawImage.
        /// </summary>
        public Rect UvRect => isShared && sharedSlot != null
            ? CeffyUv.GetUvRect(sharedSlot.Content, sharedHost.Browser.Width, sharedHost.Browser.Height)
            : CeffyUv.FullTexture;

        private CeffyBrowser browser;
        private CeffySharedHost sharedHost;
        private CeffySharedHost.Slot sharedSlot;
        private bool isShared;
        private bool warnedSharedZoom;

        private RectTransform cachedRectTransform;
        private Canvas cachedCanvas;
        private Camera cachedCanvasCamera;
        private readonly Vector3[] worldCorners = new Vector3[4];
        private int lastViewportWidth = -1;
        private int lastViewportHeight = -1;
        private float resizeTimer;

        private void Reset()
        {
            EnsureCanvas();
        }

        private void Awake()
        {
            EnsureCanvas();
            EnsureDisplayComponents();
        }

        /// <summary>
        /// A RawImage only renders under a Canvas, so a CeffyInstance added outside one becomes its own
        /// screen-space overlay canvas.
        /// </summary>
        private void EnsureCanvas()
        {
            if (GetComponentInParent<Canvas>(true))
                return;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
        }

        private void EnsureDisplayComponents()
        {
            if (!GetComponent<RawImage>())
                gameObject.AddComponent<RawImage>();
            if (!GetComponent<CeffyInstanceView>())
                gameObject.AddComponent<CeffyInstanceView>();
        }

        private void OnEnable()
        {
            if (VerboseLogging)
                WebBrowserRuntime.VerboseLogging = true;

            var remotePort = RemoteDebugging ? RemoteDebuggingPort : 0;
            WebBrowserRuntime.Instance.EnsureStarted(remotePort);

            isShared = UseSharedInstance;
            CacheViewportRefs();
            UpdateViewportSizeIfNeeded(true);

            if (isShared)
            {
                sharedHost = CeffySharedHost.GetOrCreate();
                sharedSlot = sharedHost.Register(this, TransformUrl(StartUrl));
            }
            else
            {
                browser = new CeffyBrowser(Width, Height);
                browser.MessageReceived += RaiseMessageFromCeffy;
                browser.ConsoleMessage += RaiseConsoleMessage;
                browser.DragStarted += RaiseDragStart;
                StartCoroutine(browser.Create(TransformUrl(StartUrl)));
                StartCoroutine(EndOfFrameRequestLoop());
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (sharedSlot != null)
            {
                if (sharedHost)
                    sharedHost.Unregister(sharedSlot);
                sharedSlot = null;
            }
            sharedHost = null;

            if (browser != null)
            {
                browser.MessageReceived -= RaiseMessageFromCeffy;
                browser.ConsoleMessage -= RaiseConsoleMessage;
                browser.DragStarted -= RaiseDragStart;
                browser.Close();
                browser = null;
            }
        }

        private void Update()
        {
            UpdateViewportSizeIfNeeded(false);
            if (browser != null)
                CeffyBrowser.PollCallbacks();
        }

        /// <summary>
        /// Refresh the external texture reference so Unity picks up GPU updates.
        /// Called by <see cref="CeffyInstanceView"/> when the texture changes.
        /// </summary>
        public void UpdateTexture()
        {
            if (isShared)
                sharedHost?.Browser.UpdateTexture();
            else
                browser?.UpdateTexture();
        }

        /// <summary>
        /// Request CEF's next frame after Unity has finished rendering, only while this instance is enabled.
        /// </summary>
        private IEnumerator EndOfFrameRequestLoop()
        {
            var waitForEndOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return waitForEndOfFrame;
                browser?.RequestFrame();
            }
        }

        internal void RaiseMessageFromCeffy(string message)
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

        internal void RaiseConsoleMessage(LogLevel level, string message, string source, int line)
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

        internal void RaiseDragStart(int x, int y, DragOperation allowedOps)
        {
            try
            {
                OnDragStart?.Invoke(x, y, allowedOps);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
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

            if (isShared)
            {
                if (sharedSlot != null)
                    sharedHost.Navigate(sharedSlot, transformedUrl);
            }
            else
            {
                browser?.Navigate(transformedUrl);
            }
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

        /// <summary>
        /// Run JavaScript in the page. Shared instances queue the code until their page has loaded,
        /// and can only reach pages served from the same origin as the shared host (e.g. file:// pages).
        /// </summary>
        public void ExecuteJS(string code)
        {
            if (isShared)
            {
                if (sharedSlot != null)
                    sharedHost.ExecuteJS(sharedSlot, code);
            }
            else
            {
                browser?.ExecuteJS(code);
            }
        }

        /// <summary>
        /// Send a message to the page's window.ceffy.onMessageFromUnity handler.
        /// Shared instances queue messages until their page has loaded.
        /// </summary>
        public void SendToCeffy(string message)
        {
            if (isShared)
            {
                if (sharedSlot != null)
                    sharedHost.SendMessage(sharedSlot, message);
                return;
            }

            if (browser == null || !browser.IsCreated)
            {
                Debug.LogWarning("CeffyInstance.SendToCeffy called before browser is initialized.");
                return;
            }

            browser.SendMessage(message);
        }

        #region Viewport Resize

        private void CacheViewportRefs()
        {
            cachedRectTransform = GetComponent<RectTransform>();

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
            if (sharedSlot != null)
                sharedHost.ResizeSlot(sharedSlot, Width, Height);
            else
                browser?.Resize(Width, Height);
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
        /// Not supported for shared instances.
        /// </summary>
        public void SetZoomLevel(double zoomLevel)
        {
            if (isShared)
            {
                if (!warnedSharedZoom)
                    Debug.LogWarning($"[Ceffy] Zoom is not supported for shared instances ('{name}').");
                warnedSharedZoom = true;
                return;
            }
            browser?.SetZoomLevel(zoomLevel);
        }

        /// <summary>
        /// Gets the browser zoom level on Chrome's logarithmic scale.
        /// </summary>
        public double GetZoomLevel()
        {
            return isShared || browser == null ? 0.0 : browser.GetZoomLevel();
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
        /// Send a mouse move event. Coordinates are relative to the page view (0,0 = top-left).
        /// </summary>
        public void SendMouseMove(int x, int y, EventFlags modifiers = EventFlags.None)
        {
            if (isShared)
                sharedHost?.SendMouseMove(sharedSlot, x, y, modifiers);
            else
                browser?.SendMouseMove(x, y, modifiers);
        }
        
        /// <summary>
        /// Send a mouse leave event (cursor left the page view).
        /// </summary>
        public void SendMouseLeave()
        {
            if (isShared)
                sharedHost?.SendMouseLeave(sharedSlot);
            else
                browser?.SendMouseLeave();
        }
        
        /// <summary>
        /// Send a mouse button down event.
        /// </summary>
        public void SendMouseDown(int x, int y, MouseButton button = MouseButton.Left, int clickCount = 1, EventFlags modifiers = EventFlags.None)
        {
            if (isShared)
                sharedHost?.SendMouseClick(sharedSlot, x, y, button, false, clickCount, modifiers);
            else
                browser?.SendMouseClick(x, y, button, false, clickCount, modifiers);
        }
        
        /// <summary>
        /// Send a mouse button up event.
        /// </summary>
        public void SendMouseUp(int x, int y, MouseButton button = MouseButton.Left, int clickCount = 1, EventFlags modifiers = EventFlags.None)
        {
            if (isShared)
                sharedHost?.SendMouseClick(sharedSlot, x, y, button, true, clickCount, modifiers);
            else
                browser?.SendMouseClick(x, y, button, true, clickCount, modifiers);
        }
        
        /// <summary>
        /// Send a mouse wheel event.
        /// </summary>
        public void SendMouseWheel(int x, int y, int deltaX, int deltaY, EventFlags modifiers = EventFlags.None)
        {
            if (isShared)
                sharedHost?.SendMouseWheel(sharedSlot, x, y, deltaX, deltaY, modifiers);
            else
                browser?.SendMouseWheel(x, y, deltaX, deltaY, modifiers);
        }
        
        #endregion
        
        #region Drag Input

        /// <summary>
        /// Begin a drag-target operation at the given position. Must be called once
        /// after receiving OnDragStart, before any SendDragTargetOver calls.
        /// </summary>
        public void SendDragTargetEnter(int x, int y, EventFlags modifiers = EventFlags.None, DragOperation allowedOps = DragOperation.Every)
        {
            if (isShared)
                sharedHost?.SendDragTargetEnter(sharedSlot, x, y, modifiers, allowedOps);
            else
                browser?.SendDragTargetEnter(x, y, modifiers, allowedOps);
        }

        /// <summary>
        /// Update the drag-target position. Call for each mouse move while a drag is active.
        /// </summary>
        public void SendDragTargetOver(int x, int y, EventFlags modifiers = EventFlags.None, DragOperation allowedOps = DragOperation.Every)
        {
            if (isShared)
                sharedHost?.SendDragTargetOver(sharedSlot, x, y, modifiers, allowedOps);
            else
                browser?.SendDragTargetOver(x, y, modifiers, allowedOps);
        }

        /// <summary>
        /// Notify CEF that the dragged item has left the page view.
        /// </summary>
        public void SendDragTargetLeave()
        {
            if (isShared)
                sharedHost?.SendDragTargetLeave(sharedSlot);
            else
                browser?.SendDragTargetLeave();
        }

        /// <summary>
        /// Complete the drop at the given position.
        /// </summary>
        public void SendDragTargetDrop(int x, int y, EventFlags modifiers = EventFlags.None)
        {
            if (isShared)
                sharedHost?.SendDragTargetDrop(sharedSlot, x, y, modifiers);
            else
                browser?.SendDragTargetDrop(x, y, modifiers);
        }

        /// <summary>
        /// Notify CEF that the drag source ended at the given position.
        /// The native side uses the last UpdateDragCursor operation as the result.
        /// Must be called after SendDragTargetDrop (or after SendDragTargetLeave for a cancelled drag).
        /// </summary>
        public void DragSourceEndedAt(int x, int y)
        {
            if (isShared)
                sharedHost?.DragSourceEndedAt(sharedSlot, x, y);
            else
                browser?.DragSourceEndedAt(x, y);
        }

        /// <summary>
        /// Finalizes the drag and drop session. Must be called after DragSourceEndedAt.
        /// </summary>
        public void DragSourceSystemDragEnded()
        {
            if (isShared)
                sharedHost?.DragSourceSystemDragEnded(sharedSlot);
            else
                browser?.DragSourceSystemDragEnded();
        }

        #endregion

        #region Keyboard Input

        private CeffyBrowser KeyboardTarget => isShared ? (sharedSlot != null ? sharedHost.Browser : null) : browser;

        /// <summary>
        /// Gives or removes keyboard focus. Shared instances also move DOM focus to their iframe,
        /// since all shared pages receive key events through the same browser.
        /// </summary>
        internal void SetKeyboardFocus(bool focused)
        {
            if (isShared)
                sharedHost?.SetFocus(sharedSlot, focused);
        }
        
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
            KeyboardTarget?.SendKeyEvent(eventType, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey);
        }
        
        /// <summary>
        /// Send a character input event (for text input).
        /// </summary>
        /// <param name="character">The character to input.</param>
        /// <param name="modifiers">Keyboard modifiers.</param>
        public void SendCharacter(char character, EventFlags modifiers = EventFlags.None)
        {
            KeyboardTarget?.SendKeyEvent(KeyEventType.Char, character, 0, modifiers, false);
        }
        
        /// <summary>
        /// Send a key-down event from a Unity KeyCode.
        /// </summary>
        public void KeyDown(KeyCode key, EventFlags modifiers = EventFlags.None)
        {
            KeyboardTarget?.KeyDown(key, modifiers);
        }
        
        /// <summary>
        /// Send a key-up event from a Unity KeyCode.
        /// </summary>
        public void KeyUp(KeyCode key, EventFlags modifiers = EventFlags.None)
        {
            KeyboardTarget?.KeyUp(key, modifiers);
        }

        #endregion
    }
}
