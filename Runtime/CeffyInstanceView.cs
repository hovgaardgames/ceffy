using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Ceffy
{
    /// <summary>
    /// Displays the <see cref="CeffyInstance"/> on the same GameObject through its RawImage and forwards
    /// pointer and keyboard input to it. Added automatically by <see cref="CeffyInstance"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage), typeof(CeffyInstance))]
    public class CeffyInstanceView : MonoBehaviour
    {
        [Tooltip("Enable debug logging for mouse events")]
        public bool debugMouseEvents = false;
        
        private CeffyInstance instance;
        private RawImage rawImage;
        private RectTransform rectTransform;
        private Camera canvasCamera;
        private bool hasGraphicRaycaster;
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

        // One UI raycast per frame shared by all views, used to let the topmost view take the pointer.
        private static readonly List<RaycastResult> raycastResults = new();
        private static PointerEventData pointerEventData;
        private static int raycastFrame = -1;
        private static Vector2 raycastPosition;
        private static GameObject topmostHit;
        private static Texture2D transparentTexture;

        private void Awake()
        {
            instance = GetComponent<CeffyInstance>();
            rawImage = GetComponent<RawImage>();
            rectTransform = transform as RectTransform;
            rawImage.texture = GetTransparentTexture();
            SetupBrowserMaterial();
        }

        private void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasCamera = canvas.worldCamera;
            hasGraphicRaycaster = GetComponentInParent<GraphicRaycaster>();
        }

        private void OnEnable()
        {
            instance.OnViewportResized += HandleViewportResized;
            instance.OnDragStart += HandleDragStart;
            ApplyFixedSize();
        }

        private void OnDisable()
        {
            instance.OnViewportResized -= HandleViewportResized;
            instance.OnDragStart -= HandleDragStart;
            if (isPointerInside)
                HandlePointerExit();
            SetFocus(false);
            lastTexture = null;
        }

        private void HandleViewportResized(int width, int height) => ApplyFixedSize();

        /// <summary>
        /// Without auto-resize the page size is authored, so the rect follows it instead of the other way round.
        /// </summary>
        private void ApplyFixedSize()
        {
            if (!instance.AutoResizeToRectTransform && rectTransform)
                rectTransform.sizeDelta = new Vector2(instance.Width, instance.Height);
        }

        private static Texture2D GetTransparentTexture()
        {
            if (transparentTexture)
                return transparentTexture;

            transparentTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            transparentTexture.SetPixel(0, 0, Color.clear);
            transparentTexture.Apply();
            return transparentTexture;
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
                Debug.LogWarning($"CeffyInstanceView: Material '{BROWSER_MATERIAL_NAME}' not found in Resources. " +
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
            HandleMouseInput();
        }

        private void LateUpdate()
        {
            UpdateMaterialForBackBuffer();
            UpdateTexture();
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
            Texture2D currentTexture = instance.Texture;
            if (currentTexture != lastTexture)
            {
                if (currentTexture)
                    instance.UpdateTexture();
                rawImage.texture = currentTexture ? currentTexture : GetTransparentTexture();
                lastTexture = currentTexture;
            }
            rawImage.uvRect = currentTexture ? instance.UvRect : CeffyUv.FullTexture;
        }

        #region Mouse Input
        
        private void HandleMouseInput()
        {
            Vector2 mousePos = WebBrowserInput.GetMousePosition();
            bool isInside = IsPointerOver(mousePos);
            
            if (WebBrowserInput.GetMouseButtonDown(0))
                SetFocus(isInside);
            
            if (isInside != isPointerInside)
            {
                if (isInside)
                {
                    isPointerInside = true;
                    if (debugMouseEvents)
                        Debug.Log("[CeffyInstanceView] Pointer entered");
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
                instance.SendDragTargetEnter(x, y, GetModifiers(), dragAllowedOps);
            }

            if (instance.InputMouseMove && mousePos != lastMousePosition)
            {
                lastMousePosition = mousePos;
                
                if (x != lastBrowserX || y != lastBrowserY)
                {
                    lastBrowserX = x;
                    lastBrowserY = y;
                    if (isDragging)
                    {
                        if (debugMouseEvents)
                            Debug.Log($"[CeffyInstanceView] Drag over: ({x}, {y})");
                        instance.SendDragTargetOver(x, y, GetModifiers(), dragAllowedOps);
                    }
                    else
                    {
                        if (debugMouseEvents)
                            Debug.Log($"[CeffyInstanceView] Mouse move: ({x}, {y})");
                        instance.SendMouseMove(x, y, GetModifiers());
                    }
                }
            }
            
            if (instance.InputMouseClick)
            {
                if (isDragging)
                {
                    // Drag ends on left button release — complete the drag lifecycle.
                    if (WebBrowserInput.GetMouseButtonUp(0))
                    {
                        if (debugMouseEvents)
                            Debug.Log($"[CeffyInstanceView] Drag drop: ({x}, {y})");
                        if (!isDragOutside)
                            instance.SendDragTargetDrop(x, y, GetModifiers());
                        instance.DragSourceEndedAt(x, y);
                        instance.DragSourceSystemDragEnded();
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
                                Debug.Log($"[CeffyInstanceView] Mouse down: button={button}, clicks={clickCount[button]}");
                            instance.SendMouseDown(x, y, ToMouseButton(button), clickCount[button], GetModifiers());
                        }
                        
                        if (WebBrowserInput.GetMouseButtonUp(button))
                        {
                            if (debugMouseEvents)
                                Debug.Log($"[CeffyInstanceView] Mouse up: button={button}");
                            instance.SendMouseUp(x, y, ToMouseButton(button), clickCount[button], GetModifiers());
                        }
                    }
                }
            }
            
            if (instance.InputMouseScroll)
            {
                Vector2 scrollDelta = WebBrowserInput.GetMouseScrollDelta();
                if (scrollDelta != Vector2.zero)
                {
                    // 120 is the standard Windows wheel delta unit
                    int deltaX = Mathf.RoundToInt(scrollDelta.x * 120);
                    int deltaY = Mathf.RoundToInt(scrollDelta.y * 120);
                    
                    if (debugMouseEvents)
                        Debug.Log($"[CeffyInstanceView] Scroll: ({deltaX}, {deltaY})");
                    instance.SendMouseWheel(x, y, deltaX, deltaY, GetModifiers());
                }
            }
        }

        /// <summary>
        /// True when the pointer is inside the rect and, if this RawImage takes part in UI raycasts,
        /// no other UI element is on top of it.
        /// </summary>
        private bool IsPointerOver(Vector2 screenPosition)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPosition, canvasCamera, out Vector2 localPoint))
                return false;

            if (!rectTransform.rect.Contains(localPoint))
                return false;

            if (!rawImage.raycastTarget || !hasGraphicRaycaster)
                return true;

            return GetTopmostHit(screenPosition) == gameObject;
        }

        private static GameObject GetTopmostHit(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (!eventSystem)
                return null;

            if (raycastFrame == Time.frameCount && raycastPosition == screenPosition)
                return topmostHit;

            if (pointerEventData == null || pointerEventData.currentInputModule != eventSystem.currentInputModule)
                pointerEventData = new PointerEventData(eventSystem);
            pointerEventData.position = screenPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            raycastFrame = Time.frameCount;
            raycastPosition = screenPosition;
            topmostHit = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
            return topmostHit;
        }
        
        private void HandleDragStart(int x, int y, DragOperation allowedOps)
        {
            if (debugMouseEvents)
                Debug.Log($"[CeffyInstanceView] Drag start: ({x}, {y}), ops={allowedOps}");
            isDragging = true;
            isDragOutside = false;
            dragAllowedOps = allowedOps;
            instance.SendDragTargetEnter(x, y, GetModifiers(), allowedOps);
        }

        private void HandlePointerExit()
        {
            if (debugMouseEvents)
                Debug.Log("[CeffyInstanceView] Pointer exited");
            isPointerInside = false;
            lastBrowserX = -1;
            lastBrowserY = -1;
            if (isDragging && !isDragOutside)
            {
                isDragOutside = true;
                instance.SendDragTargetLeave();
            }
            else
            {
                instance.SendMouseLeave();
            }
        }
        
        #endregion
        
        #region Keyboard Input

        private void SetFocus(bool focused)
        {
            if (hasFocus == focused)
                return;
            hasFocus = focused;
            instance.SetKeyboardFocus(focused);
        }
        
        private void OnGUI()
        {
            if (!hasFocus || !instance.InputKeyboard)
                return;
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject)
                return;

            Event e = Event.current;
            if (!e.isKey)
                return;

            var modifiers = GetModifiersFromEvent(e);

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode != KeyCode.None)
                    instance.KeyDown(e.keyCode, modifiers);

                if (e.character != '\0')
                {
                    char c = e.character;
                    if (!char.IsControl(c) || c == '\r' || c == '\n' || c == '\t' || c == '\b')
                    {
                        char charToSend = (c == '\n') ? '\r' : c;
                        var charModifiers = modifiers;
                        if ((charModifiers & EventFlags.AltGrDown) != 0)
                            charModifiers &= ~(EventFlags.ControlDown | EventFlags.AltDown);
                        instance.SendCharacter(charToSend, charModifiers);
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.KeyUp)
            {
                if (e.keyCode != KeyCode.None)
                    instance.KeyUp(e.keyCode, modifiers);
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
        /// Convert a screen position to page coordinates.
        /// Returns true if the position is within the page bounds.
        /// </summary>
        private bool ScreenToBrowserCoords(Vector2 screenPosition, out int x, out int y)
        {
            x = 0;
            y = 0;
            
            if (!rectTransform)
                return false;
            
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, screenPosition, canvasCamera, out Vector2 localPoint))
                return false;
            
            Rect rect = rectTransform.rect;
            float normalizedX = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
            float normalizedY = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);
            
            x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * instance.Width), 0, instance.Width - 1);
            y = Mathf.Clamp(Mathf.RoundToInt((1f - normalizedY) * instance.Height), 0, instance.Height - 1); // flip Y
            
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
