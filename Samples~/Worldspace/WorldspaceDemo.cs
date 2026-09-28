using UnityEngine;
using UnityEngine.UI;

namespace Ceffy.Demos.Worldspace
{
    /// <summary>
    /// Builds a world-space Ceffy panel (unity.com) viewed from an angle, plus an on-screen
    /// hint for the fly-camera controls.
    /// </summary>
    public sealed class WorldspaceDemo : MonoBehaviour
    {
        [Tooltip("Page shown on the world-space Ceffy instance.")]
        public string StartUrl = "https://www.unity.com";

        [Tooltip("Browser viewport size in CSS pixels.")]
        public int Width = 1280;

        [Tooltip("Browser viewport size in CSS pixels.")]
        public int Height = 720;

        [Tooltip("World-space meters per UI pixel.")]
        public float MetersPerPixel = 0.0015f;

        private void Start()
        {
            var camera = Camera.main;
            if (!camera)
            {
                Debug.LogError("[WorldspaceDemo] No Main Camera found.");
                return;
            }

            CreateGround();
            CreateWorldspaceCeffy(camera);
            CreateControlsHint();
        }

        private void CreateWorldspaceCeffy(Camera camera)
        {
            var canvasGo = new GameObject("Worldspace Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Width);
            canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
            canvasGo.transform.SetPositionAndRotation(
                new Vector3(0f, 1.4f, 0f),
                Quaternion.Euler(0f, -22f, 0f));
            canvasGo.transform.localScale = Vector3.one * MetersPerPixel;

            var instanceGo = new GameObject("Ceffy", typeof(RectTransform));
            instanceGo.SetActive(false);
            instanceGo.transform.SetParent(canvasGo.transform, false);

            var instanceRect = (RectTransform)instanceGo.transform;
            instanceRect.anchorMin = Vector2.zero;
            instanceRect.anchorMax = Vector2.one;
            instanceRect.offsetMin = Vector2.zero;
            instanceRect.offsetMax = Vector2.zero;

            var instance = instanceGo.AddComponent<CeffyInstance>();
            instance.StartUrl = StartUrl;
            instance.Width = Width;
            instance.Height = Height;
            instance.AutoResizeToRectTransform = false;
            instanceGo.SetActive(true);
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            var renderer = ground.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (renderer && shader)
                renderer.sharedMaterial = new Material(shader) { color = new Color(0.18f, 0.2f, 0.23f) };
        }

        private static void CreateControlsHint()
        {
            var canvasGo = new GameObject("Controls Hint", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var textGo = new GameObject("Hint", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(520f, 88f);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            text.text =
                "Camera: WASD to move, Q/E up/down\n" +
                "Hold right mouse button to look around\n" +
                "Hold Shift to move faster  •  Left-click the page to interact";

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }
}
