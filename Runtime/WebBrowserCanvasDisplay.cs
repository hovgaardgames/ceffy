using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Ceffy
{
    /// <summary>
    /// Displays a <see cref="WebBrowser"/> on a uGUI RawImage and forwards pointer and keyboard input.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class WebBrowserCanvasDisplay : MonoBehaviour
    {
        [Tooltip("The WebBrowser component to display. If null, will search for one in the scene.")]
        public WebBrowser webBrowser;

        [Tooltip("Enable debug logging for mouse events")]
        public bool debugMouseEvents = false;
        
        private RawImage rawImage;
        private RectTransform rectTransform;
        private Canvas canvas;
        private Camera canvasCamera;
        private Texture2D lastTexture;
        private bool isPointerInside;
        private bool hasFocus;
        private Vector2 lastMousePosition;
        private int lastBrowserX = -1;
        private int lastBrowserY = -1;
        private Material browserMaterial;
        
        // Click tracking for double-click support
        private float[] lastClickTime = new float[3];
        private int[] clickCount = new int[3];
        private const float DoubleClickTime = 0.3f;

        // Drag tracking for HTML5 drag & drop
        private bool isDragging;
        private bool isDragOutside;
        private DragOperation dragAllowedOps;
        
        private const string BROWSER_MATERIAL_NAME = "WebBrowserUIMaterial";

        private void Start()
        {
            if (TryGetComponent(out rawImage))
                rawImage = gameObject.AddComponent<RawImage>();
            
            rectTransform = transform as RectTransform;
            canvas = GetComponentInParent<Canvas>();
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvasCamera = canvas.worldCamera;
            }

            if (!webBrowser)
            {
                webBrowser = transform.parent.gameObject.GetComponent<WebBrowser>();
                if (!webBrowser)
                {
                    Debug.LogWarning("WebBrowserCanvasDisplay: No WebBrowser found in scene. Please assign one manually.");
                    return;
                }
            }

            webBrowser.OnViewportResized += (w, h) => rawImage.rectTransform.sizeDelta = new Vector2(w, h);
            webBrowser.OnDragStart += HandleDragStart;
            rawImage.rectTransform.sizeDelta = new Vector2(webBrowser.Width, webBrowser.Height);
            rawImage.texture = null;
            SetupBrowserMaterial();
        }
        
        private void SetupBrowserMaterial()
        {
            var sourceMaterial = Resources.Load<Material>(BROWSER_MATERIAL_NAME);
            if (sourceMaterial)
            {
                browserMaterial = new Material(sourceMaterial);
                rawImage.material = browserMaterial;
            }
            else
            {
                Debug.LogWarning($"WebBrowserCanvasDisplay: Material '{BROWSER_MATERIAL_NAME}' not found in Resources. " +
                               "Colors may appear incorrect in Linear color space mode.");
            }
        }

        private void OnDestroy()
        {
            if (browserMaterial)
            {
                Destroy(browserMaterial);
                browserMaterial = null;
            }
        }

        private void Update()
        {
            if (!webBrowser || !rawImage || !gameObject.activeInHierarchy || !enabled)
                return;

            UpdateMaterialForBackBuffer();
            UpdateTexture();
            HandleMouseInput();
        }

        private void UpdateMaterialForBackBuffer()
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear || !browserMaterial)
                return;

            // D3D12 doesn't apply sRGBWrite for Canvas rendering in fullscreen at non-native
            // resolution, causing the shader's linear output to appear dark. In that case,
            // skip gamma correction so raw sRGB texture data passes through directly.
            bool isD3D12NonNativeFullscreen =
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12
                && Screen.fullScreen
                && (Screen.width != Display.main.systemWidth
                    || Screen.height != Display.main.systemHeight);

            rawImage.material = isD3D12NonNativeFullscreen ? null : browserMaterial;
        }
        
        private void UpdateTexture()
        {
            Texture2D currentTexture = webBrowser.Texture;
            if (currentTexture && currentTexture != lastTexture)
            {
                webBrowser.UpdateTexture();
                rawImage.texture = currentTexture;
                rawImage.uvRect = new Rect(0, 1, 1, -1); // flip Y (browser origin is top-left)
                lastTexture = currentTexture;
            }
        }

        #region Mouse Input
        
        private void HandleMouseInput()
        {
            Vector2 mousePos = WebBrowserInput.GetMousePosition();
            
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, mousePos, canvasCamera, out Vector2 localPoint))
            {
                HandlePointerExit();
                return;
            }
            
            bool isInside = rectTransform.rect.Contains(localPoint);
            
            if (WebBrowserInput.GetMouseButtonDown(0))
                hasFocus = isInside;
            
            if (isInside != isPointerInside)
            {
                isPointerInside = isInside;
                if (isInside)
                {
                    if (debugMouseEvents)
                        Debug.Log("[WebBrowserCanvasDisplay] Pointer entered");
                }
                else
                {
                    HandlePointerExit();
                }
            }
            
            if (!isInside)
                return;
            
            if (!ScreenToBrowserCoords(mousePos, out int x, out int y))
                return;
            
            if (isDragging && isDragOutside)
            {
                // Pointer re-entered during an active drag — restart drag target tracking.
                isDragOutside = false;
                webBrowser.SendDragTargetEnter(x, y, GetModifiers(), dragAllowedOps);
            }

            if (webBrowser.InputMouseMove && mousePos != lastMousePosition)
            {
                lastMousePosition = mousePos;
                
                if (x != lastBrowserX || y != lastBrowserY)
                {
                    lastBrowserX = x;
                    lastBrowserY = y;
                    if (isDragging)
                    {
                        if (debugMouseEvents)
                            Debug.Log($"[WebBrowserCanvasDisplay] Drag over: ({x}, {y})");
                        webBrowser.SendDragTargetOver(x, y, GetModifiers(), dragAllowedOps);
                    }
                    else
                    {
                        if (debugMouseEvents)
                            Debug.Log($"[WebBrowserCanvasDisplay] Mouse move: ({x}, {y})");
                        webBrowser.SendMouseMove(x, y, GetModifiers());
                    }
                }
            }
            
            if (webBrowser.InputMouseClick)
            {
                if (isDragging)
                {
                    // Drag ends on left button release — complete the drag lifecycle.
                    if (WebBrowserInput.GetMouseButtonUp(0))
                    {
                        if (debugMouseEvents)
                            Debug.Log($"[WebBrowserCanvasDisplay] Drag drop: ({x}, {y})");
                        if (!isDragOutside)
                            webBrowser.SendDragTargetDrop(x, y, GetModifiers());
                        webBrowser.DragSourceEndedAt(x, y);
                        webBrowser.DragSourceSystemDragEnded();
                        isDragging = false;
                        isDragOutside = false;
                    }
                }
                else
                {
                    for (int button = 0; button < 3; button++)
                    {
                        if (WebBrowserInput.GetMouseButtonDown(button))
                        {
                            float timeSinceLastClick = Time.unscaledTime - lastClickTime[button];
                            if (timeSinceLastClick <= DoubleClickTime)
                                clickCount[button]++;
                            else
                                clickCount[button] = 1;
                            lastClickTime[button] = Time.unscaledTime;
                            
                            if (debugMouseEvents)
                                Debug.Log($"[WebBrowserCanvasDisplay] Mouse down: button={button}, clicks={clickCount[button]}");
                            webBrowser.SendMouseDown(x, y, ToMouseButton(button), clickCount[button], GetModifiers());
                        }
                        
                        if (WebBrowserInput.GetMouseButtonUp(button))
                        {
                            if (debugMouseEvents)
                                Debug.Log($"[WebBrowserCanvasDisplay] Mouse up: button={button}");
                            webBrowser.SendMouseUp(x, y, ToMouseButton(button), clickCount[button], GetModifiers());
                        }
                    }
                }
            }
            
            if (webBrowser.InputMouseScroll)
            {
                Vector2 scrollDelta = WebBrowserInput.GetMouseScrollDelta();
                if (scrollDelta != Vector2.zero)
                {
                    // 120 is the standard Windows wheel delta unit
                    int deltaX = Mathf.RoundToInt(scrollDelta.x * 120);
                    int deltaY = Mathf.RoundToInt(scrollDelta.y * 120);
                    
                    if (debugMouseEvents)
                        Debug.Log($"[WebBrowserCanvasDisplay] Scroll: ({deltaX}, {deltaY})");
                    webBrowser.SendMouseWheel(x, y, deltaX, deltaY, GetModifiers());
                }
            }
        }
        
        private void HandleDragStart(int x, int y, DragOperation allowedOps)
        {
            if (debugMouseEvents)
                Debug.Log($"[WebBrowserCanvasDisplay] Drag start: ({x}, {y}), ops={allowedOps}");
            isDragging = true;
            isDragOutside = false;
            dragAllowedOps = allowedOps;
            webBrowser.SendDragTargetEnter(x, y, GetModifiers(), allowedOps);
        }

        private void HandlePointerExit()
        {
            if (debugMouseEvents)
                Debug.Log("[WebBrowserCanvasDisplay] Pointer exited");
            lastBrowserX = -1;
            lastBrowserY = -1;
            if (isDragging && !isDragOutside)
            {
                isDragOutside = true;
                if (webBrowser) 
                    webBrowser.SendDragTargetLeave();
            }
            else
            {
                if (webBrowser) 
                    webBrowser.SendMouseLeave();
            }
        }
        
        #endregion
        
        #region Keyboard Input
        
        private void OnGUI()
        {
            if (!hasFocus || !webBrowser || !webBrowser.InputKeyboard || EventSystem.current.currentSelectedGameObject)
                return;

            Event e = Event.current;
            if (!e.isKey)
                return;

            var modifiers = GetModifiersFromEvent(e);

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode != KeyCode.None)
                    webBrowser.KeyDown(e.keyCode, modifiers);

                if (e.character != '\0')
                {
                    char c = e.character;
                    if (!char.IsControl(c) || c == '\r' || c == '\n' || c == '\t' || c == '\b')
                    {
                        char charToSend = (c == '\n') ? '\r' : c;
                        var charModifiers = modifiers;
                        if ((charModifiers & EventFlags.AltGrDown) != 0)
                            charModifiers &= ~(EventFlags.ControlDown | EventFlags.AltDown);
                        webBrowser.SendCharacter(charToSend, charModifiers);
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.KeyUp)
            {
                if (e.keyCode != KeyCode.None)
                    webBrowser.KeyUp(e.keyCode, modifiers);
                e.Use();
            }
        }

        private EventFlags GetModifiersFromEvent(Event e)
        {
            EventFlags flags = EventFlags.None;
            if ((e.modifiers & EventModifiers.Shift) != 0)   flags |= EventFlags.ShiftDown;
            if ((e.modifiers & EventModifiers.Control) != 0)  flags |= EventFlags.ControlDown;
            if ((e.modifiers & EventModifiers.Alt) != 0)      flags |= EventFlags.AltDown;
            if ((e.modifiers & EventModifiers.Command) != 0)  flags |= EventFlags.CommandDown;
            if ((e.modifiers & EventModifiers.CapsLock) != 0) flags |= EventFlags.CapsLockOn;
            if (WebBrowserInput.GetMouseButton(0)) flags |= EventFlags.LeftMouseButton;
            if (WebBrowserInput.GetMouseButton(1)) flags |= EventFlags.RightMouseButton;
            if (WebBrowserInput.GetMouseButton(2)) flags |= EventFlags.MiddleMouseButton;

            if (WebBrowserInput.GetRightAlt())
                flags |= EventFlags.AltGrDown;

            return flags;
        }
        
        #endregion
        
        #region Coordinate Conversion
        
        /// <summary>
        /// Convert a screen position to browser texture coordinates.
        /// Returns true if the position is within the browser bounds.
        /// </summary>
        private bool ScreenToBrowserCoords(Vector2 screenPosition, out int x, out int y)
        {
            x = 0;
            y = 0;
            
            if (!webBrowser || !rectTransform)
                return false;
            
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition, canvasCamera, out Vector2 localPoint))
                return false;
            
            Rect rect = rectTransform.rect;
            float normalizedX = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
            float normalizedY = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);
            
            x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * webBrowser.Width), 0, webBrowser.Width - 1);
            y = Mathf.Clamp(Mathf.RoundToInt((1f - normalizedY) * webBrowser.Height), 0, webBrowser.Height - 1); // flip Y
            
            return true;
        }
        
        /// <summary>
        /// Convert Unity mouse button index to CEF mouse button.
        /// </summary>
        private MouseButton ToMouseButton(int unityButton)
        {
            return unityButton switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Right,
                2 => MouseButton.Middle,
                _ => MouseButton.Left
            };
        }
        
        /// <summary>
        /// Get current keyboard and mouse button modifiers.
        /// CEF requires mouse button state in event flags for drag operations (e.g. scrollbar thumb dragging).
        /// </summary>
        private EventFlags GetModifiers()
        {
            EventFlags modifiers = EventFlags.None;
            
            if (WebBrowserInput.GetShift())
                modifiers |= EventFlags.ShiftDown;
            if (WebBrowserInput.GetControl())
                modifiers |= EventFlags.ControlDown;
            if (WebBrowserInput.GetAlt())
                modifiers |= EventFlags.AltDown;
            
            // Include mouse button state - required by CEF for drag operations
            if (WebBrowserInput.GetMouseButton(0))
                modifiers |= EventFlags.LeftMouseButton;
            if (WebBrowserInput.GetMouseButton(1))
                modifiers |= EventFlags.RightMouseButton;
            if (WebBrowserInput.GetMouseButton(2))
                modifiers |= EventFlags.MiddleMouseButton;
            
            return modifiers;
        }

        #endregion
    }
}
