using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ceffy.Demos.GameplayHud
{
    public class OutpostArena : MonoBehaviour
    {
        private const float CameraHeightMultiplier = 2.6f;
        private const float CameraBackMultiplier = -1.8f;
        private const float CameraFieldOfView = 50.0f;
        private const float CameraFarClipMultiplier = 6.0f;
        private const float CellSize = 2.0f;
        private const float GridThickness = 0.035f;
        private const float FloorThickness = 0.25f;

        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>(8);
        private Material sourceMaterial;
        private Transform root;
        private Transform map;
        private Transform grid;
        private Transform actors;
        private Transform enemies;
        private Transform pickups;
        private Transform turrets;
        private Transform buildPads;
        private Transform bullets;
        private Transform cameraRig;
        private Transform cameraTransform;
        private Transform systems;
        private Transform ui;
        private Transform worldPanels;
        private Transform enemyPanels;
        private Transform turretPanels;
        private float halfSize;

        public Camera Camera { get; private set; }
        public Transform Player { get; private set; }
        public Transform Generator { get; private set; }
        public Transform SystemsRoot => systems;
        public Transform UiRoot => ui;

        public void Initialize(float halfSize)
        {
            root = transform;
            this.halfSize = halfSize;
            CreateGroups();
            CreateCamera();
            CreateFloor(halfSize);
            CreateGrid(halfSize);
            Generator = CreateBody("Generator", PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(1.8f, 0.7f, 1.8f), new Color(0.1f, 0.8f, 0.75f));
            Generator.SetParent(actors, false);
            var core = CreateBody("Core", PrimitiveType.Cube, new Vector3(0.0f, 1.8f, 0.0f),
                Vector3.one * 0.8f, new Color(0.65f, 1.0f, 0.85f));
            core.SetParent(Generator, true);
            core.localPosition = Vector3.up;
            Player = CreateBody("Player", PrimitiveType.Capsule, new Vector3(0.0f, 0.0f, -3.0f),
                new Vector3(0.7f, 0.6f, 0.7f), new Color(0.3f, 0.7f, 1.0f));
            Player.SetParent(actors, false);
            CreateBarrel(Player, new Color(0.8f, 0.9f, 1.0f));
        }

        private void OnDestroy()
        {
            foreach (var material in materials.Values)
                Destroy(material);
        }

        private void CreateCamera()
        {
            var cameraObject = new GameObject("OutpostCamera", typeof(Camera), typeof(AudioListener));
            cameraTransform = cameraObject.transform;
            cameraTransform.SetParent(cameraRig, false);
            cameraTransform.position =
                new Vector3(0.0f, halfSize * CameraHeightMultiplier, halfSize * CameraBackMultiplier);
            cameraTransform.LookAt(Vector3.zero);
            Camera = cameraObject.GetComponent<Camera>();
            Camera.orthographic = false;
            Camera.fieldOfView = CameraFieldOfView;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.025f, 0.045f, 0.065f);
            Camera.farClipPlane = halfSize * CameraFarClipMultiplier;

            var lightObject = new GameObject("Sun", typeof(Light));
            lightObject.transform.SetParent(cameraRig, false);
            lightObject.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        public void SetCameraZoom(float zoom)
        {
            cameraTransform.position =
                new Vector3(0.0f, halfSize * CameraHeightMultiplier, halfSize * CameraBackMultiplier) / zoom;
        }

        private void CreateGroups()
        {
            map = CreateGroup("Map", root);
            grid = CreateGroup("Grid", map);
            buildPads = CreateGroup("BuildPads", map);
            actors = CreateGroup("Actors", root);
            enemies = CreateGroup("Enemies", root);
            pickups = CreateGroup("EnergyPickups", root);
            turrets = CreateGroup("Turrets", root);
            bullets = CreateGroup("Bullets", root);
            cameraRig = CreateGroup("CameraRig", root);
            systems = CreateGroup("Systems", root);
            ui = CreateGroup("UI", root);
            worldPanels = CreateGroup("WorldPanels", ui);
            enemyPanels = CreateGroup("EnemyPanels", worldPanels);
            turretPanels = CreateGroup("TurretPanels", worldPanels);
        }

        private Transform CreateGroup(string objectName, Transform parent)
        {
            var group = new GameObject(objectName).transform;
            group.SetParent(parent, false);
            return group;
        }

        private void CreateFloor(float halfSize)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(map, false);
            floor.transform.position = Vector3.down * FloorThickness;
            floor.transform.localScale = new Vector3(halfSize * 2.0f, FloorThickness, halfSize * 2.0f);
            var renderer = floor.GetComponent<Renderer>();
            // Reuse the render pipeline's default shader; the sample supplies no custom world shader.
            var pipeline = GraphicsSettings.currentRenderPipeline;
            sourceMaterial = pipeline != null ? pipeline.defaultMaterial : renderer.sharedMaterial;
            renderer.sharedMaterial = GetMaterial(new Color(0.09f, 0.14f, 0.18f));
            Destroy(floor.GetComponent<Collider>());
        }

        private void CreateGrid(float halfSize)
        {
            var gridColor = new Color(0.15f, 0.23f, 0.27f);
            for (var position = -halfSize; position <= halfSize; position += CellSize)
            {
                var vertical = CreateBody("GridLine", PrimitiveType.Cube, new Vector3(position, 0.0f, 0.0f),
                    new Vector3(GridThickness, GridThickness, halfSize * 2.0f), gridColor);
                vertical.SetParent(grid, false);
                var horizontal = CreateBody("GridLine", PrimitiveType.Cube, new Vector3(0.0f, 0.0f, position),
                    new Vector3(halfSize * 2.0f, GridThickness, GridThickness), gridColor);
                horizontal.SetParent(grid, false);
            }
        }

        public Transform CreateEnemy()
        {
            var enemy = CreateBody("Enemy", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.85f, 0.85f, 0.85f), new Color(1.0f, 0.28f, 0.27f));
            enemy.SetParent(enemies, false);
            enemy.gameObject.SetActive(false);
            return enemy;
        }

        public Transform CreatePickup()
        {
            var pickup = CreateBody("Energy", PrimitiveType.Cube, Vector3.zero,
                Vector3.one * 0.4f, new Color(1.0f, 0.8f, 0.2f));
            pickup.SetParent(pickups, false);
            pickup.rotation = Quaternion.Euler(0.0f, 45.0f, 45.0f);
            pickup.gameObject.SetActive(false);
            return pickup;
        }

        public Transform CreateTurret(Vector3 position)
        {
            var turret = CreateBody("Turret", PrimitiveType.Cylinder, position,
                new Vector3(1.0f, 0.5f, 1.0f), new Color(0.35f, 0.85f, 0.8f));
            turret.SetParent(turrets, false);
            CreateBarrel(turret, new Color(0.8f, 0.9f, 1.0f));
            turret.gameObject.SetActive(false);
            return turret;
        }

        public Renderer CreateBuildPad(Vector3 position)
        {
            var pad = CreateBody("BuildPad", PrimitiveType.Cylinder, position,
                new Vector3(2.4f, 0.04f, 2.4f), new Color(0.17f, 0.35f, 0.4f));
            pad.SetParent(buildPads, false);
            return pad.GetComponent<Renderer>();
        }

        public void SetPadHighlight(Renderer pad, bool isHovered)
        {
            pad.sharedMaterial = GetMaterial(isHovered
                ? new Color(0.55f, 1.0f, 0.75f)
                : new Color(0.17f, 0.35f, 0.4f));
        }

        public Transform CreateBullet(float radius)
        {
            var bullet = CreateBody("Bullet", PrimitiveType.Sphere, Vector3.zero,
                Vector3.one * (radius * 2.0f), new Color(1.0f, 0.8f, 0.2f));
            bullet.SetParent(bullets, false);
            bullet.gameObject.SetActive(false);
            return bullet;
        }

        private void CreateBarrel(Transform owner, Color color)
        {
            var barrel = CreateBody("Barrel", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.2f, 0.2f, 1.0f), color);
            barrel.SetParent(owner, false);
            barrel.localPosition = new Vector3(0.0f, 0.7f, 0.7f);
        }

        private Transform CreateBody(string objectName, PrimitiveType type, Vector3 position, Vector3 scale,
            Color color)
        {
            var body = GameObject.CreatePrimitive(type);
            body.name = objectName;
            body.transform.SetParent(root, false);
            body.transform.position = position + Vector3.up * scale.y;
            body.transform.localScale = scale;
            body.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
            Destroy(body.GetComponent<Collider>());
            return body.transform;
        }

        private Material GetMaterial(Color color)
        {
            if (materials.TryGetValue(color, out var material))
                return material;

            material = new Material(sourceMaterial) { color = color };
            materials.Add(color, material);
            return material;
        }

        public OutpostPanel CreatePanel(OutpostGame game, Transform target, string kind, int index)
        {
            var panelObject = new GameObject("WorldPanel", typeof(RectTransform));
            panelObject.SetActive(false);
            var parent = kind == "enemy" ? enemyPanels : kind == "turret" ? turretPanels : worldPanels;
            panelObject.transform.SetParent(parent, false);
            var panel = panelObject.AddComponent<OutpostPanel>();
            panel.Initialize(game, Camera, target, kind, index);
            panelObject.SetActive(true);
            return panel;
        }
    }
}
