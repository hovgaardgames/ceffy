using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ceffy.Demos.GameplayHud
{
    public class OutpostPanel : MonoBehaviour
    {
        private const int PageWidth = 320;
        private const int PageHeight = 144;
        private const float EnemyPanelHeight = 2.0f;
        private const float WorldPanelHeight = 2.4f;
        private const string PageUrl = "streaming-assets://Demos/GameplayHud/panel.html";

        private Transform target;
        private Vector3 offset;
        private Quaternion rotation;
        private CeffyInstance instance;
        private Canvas canvas;
        private GraphicRaycaster raycaster;
        private Transform cachedTransform;
        private Camera viewCamera;
        private Transform cameraTransform;
        private float screenWidth;
        private OutpostGame game;
        private int index;
        private bool isReady;
        private readonly PanelState state = new PanelState();

        public void Initialize(OutpostGame game, Camera camera, Transform target, string kind, int index)
        {
            this.game = game;
            viewCamera = camera;
            cameraTransform = camera.transform;
            screenWidth = kind == "enemy" ? game.EnemyPanelWidth : game.WorldPanelWidth;
            this.target = target;
            offset = Vector3.up * (kind == "enemy" ? EnemyPanelHeight : WorldPanelHeight);
            rotation = camera.transform.rotation;
            this.index = index;
            state.kind = kind;
            state.index = index;
            cachedTransform = transform;
            var rect = (RectTransform)cachedTransform;
            rect.sizeDelta = new Vector2(PageWidth, PageHeight);
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = kind != "enemy";

            instance = gameObject.AddComponent<CeffyInstance>();
            instance.StartUrl = PageUrl;
            instance.UseSharedInstance = true;
            instance.AutoResizeToRectTransform = false;
            instance.Width = PageWidth;
            instance.Height = PageHeight;
            instance.InputKeyboard = false;
            instance.OnMessageFromCeffy += HandleMessage;
            // A disabled raycaster gives passive views the normal topmost-hit input gate without taking clicks.
        }

        private void LateUpdate()
        {
            cachedTransform.SetPositionAndRotation(target.position + offset, rotation);
            // Compensate for perspective depth so distant labels retain their configured screen width.
            var depth = Vector3.Dot(cachedTransform.position - cameraTransform.position, cameraTransform.forward);
            var heightAtDepth = depth * 2.0f * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var metersPerPixel = heightAtDepth / viewCamera.pixelHeight;
            cachedTransform.localScale = Vector3.one * (metersPerPixel * screenWidth / PageWidth);
        }

        private void OnDestroy()
        {
            if (instance)
                instance.OnMessageFromCeffy -= HandleMessage;
        }

        private void HandleMessage(string message)
        {
            if (message == "panel:ready")
            {
                isReady = true;
                SendState();
            }
            else if (message == "repair")
            {
                game.RepairGenerator();
            }
            else if (message == "upgrade")
            {
                game.UpgradeTurret(index);
            }
            else if (message == "build")
            {
                game.BuildTurretAt(index);
            }
        }

        public void SetState(string title, float health, float maxHealth, string action, bool canAct, bool isVisible)
        {
            var roundedHealth = Mathf.CeilToInt(health);
            var roundedMaxHealth = Mathf.CeilToInt(maxHealth);
            if (state.title == title && state.health == roundedHealth && state.maxHealth == roundedMaxHealth
                && state.action == action && state.canAct == canAct && state.isVisible == isVisible
                && state.theme == game.Theme)
                return;

            state.title = title;
            state.health = roundedHealth;
            state.maxHealth = roundedMaxHealth;
            state.action = action;
            state.canAct = canAct;
            state.isVisible = isVisible;
            state.theme = game.Theme;
            state.command = state.kind == "generator" ? "repair" : roundedHealth == 0 ? "build" : "upgrade";
            // Hiding the Canvas keeps its iframe/atlas slot alive for reuse on the next spawn.
            canvas.enabled = isVisible;
            raycaster.enabled = isVisible && state.kind != "enemy";
            SendState();
        }

        private void SendState()
        {
            if (isReady)
                instance.SendToCeffy(JsonUtility.ToJson(state));
        }

        [Serializable]
        private class PanelState
        {
            public string kind;
            public int index;
            public string title;
            public int health;
            public int maxHealth;
            public string action;
            public bool canAct;
            public bool isVisible;
            public string theme;
            public string command;
        }
    }
}
