using System.Collections.Generic;
using Ceffy.Bridge;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ceffy.Demos.GameplayHud
{
    public class OutpostHud : MonoBehaviour, ICanvasRaycastFilter
    {
        private const string PageReadyMessage = "outpost:ready";
        private const string PageUrl = "streaming-assets://Demos/GameplayHud/index.html";

        private readonly List<RaycastResult> hits = new List<RaycastResult>(24);
        private OutpostInputRegion[] regions;
        private RectTransform rectTransform;
        private OutpostGame game;
        private CeffyInstance instance;
        private CeffyBridge bridge;
        private IOutpostUi ui;
        private PointerEventData pointer;
        private bool isReady;
        private float snapshotTimer;

        public bool IsReady => isReady;

        public void Initialize(OutpostGame game)
        {
            this.game = game;
            rectTransform = (RectTransform)transform;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            instance = gameObject.AddComponent<CeffyInstance>();
            instance.StartUrl = PageUrl;
            instance.InputKeyboard = false;
            instance.OnMessageFromCeffy += HandleMessage;
            bridge = new CeffyBridge(instance);
            bridge.Bind<IOutpostCommands>(game);
            ui = bridge.CreateProxy<IOutpostUi>();
        }

        private void Update()
        {
            bridge.ProcessMessages();
            if (!isReady)
                return;

            snapshotTimer += Time.unscaledDeltaTime;
            if (snapshotTimer < OutpostGame.UiUpdateInterval)
                return;

            snapshotTimer = 0.0f;
            ui.UpdateState(game.GetState());
        }

        private void OnDestroy()
        {
            if (instance)
                instance.OnMessageFromCeffy -= HandleMessage;
            bridge?.Dispose();
        }

        private void HandleMessage(string message)
        {
            if (message != PageReadyMessage)
                return;

            // A page handshake avoids guessing readiness from the texture or a fixed delay.
            bridge.InjectBridgeRuntime();
            isReady = true;
            ApplyZoomPercent(game.HudZoomPercent);
        }

        public void ApplyZoomPercent(float percent)
        {
            instance.SetZoomPercent(percent);
        }

        public void SetInputRegions(OutpostInputRegion[] regions)
        {
            this.regions = regions;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (regions == null)
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out var local))
                return false;

            var rect = rectTransform.rect;
            var x = (local.x - rect.xMin) / rect.width;
            var y = (rect.yMax - local.y) / rect.height;
            for (var i = 0; i < regions.Length; i++)
            {
                var region = regions[i];
                if (x >= region.X && x <= region.X + region.Width
                    && y >= region.Y && y <= region.Y + region.Height)
                    return true;
            }

            return false;
        }

        public bool IsPointerOverUi(Vector2 position)
        {
            return GetPointerTarget(position) != null;
        }

        public GameObject GetPointerTarget(Vector2 position)
        {
            if (pointer == null)
                pointer = new PointerEventData(EventSystem.current);
            pointer.position = position;
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        public void Notify(string message)
        {
            if (isReady)
                ui.ShowNotification(message);
        }
    }
}
