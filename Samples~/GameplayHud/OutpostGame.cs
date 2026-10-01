using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Ceffy.Demos.GameplayHud
{
    public class OutpostGame : MonoBehaviour, IOutpostCommands
    {
        public const float UiUpdateInterval = 0.1f;
        public const float MinHudZoomPercent = 75.0f;
        public const float MaxHudZoomPercent = 200.0f;

        private const int EnemyPoolSize = 12;
        private const int PickupPoolSize = 32;
        private const int TurretCount = 3;
        private const int BulletPoolSize = 64;
        private const float BulletHeight = 0.85f;
        private const float BuildPadRadius = 1.2f;
        private const float PadHoverDelay = 0.5f;
        private const float SpawnInterval = 0.8f;
        private const float EnemyRadius = 0.65f;
        private const float AttackDistance = 1.4f;
        private const float PlayerAggroRange = 5.0f;
        private const float PickupRange = 1.5f;
        private const float PickupLifetime = 35.0f;
        private const int PointsPerEnemy = 100;
        private const float MinCameraZoom = 0.5f;
        private const float MaxCameraZoom = 3.0f;

        [Header("Arena"), SerializeField]
        private float arenaHalfSize = 22.0f;

        [Header("Camera"), SerializeField, Range(MinCameraZoom, MaxCameraZoom)]
        [Tooltip("1 uses the original view. Higher values move closer. Applied when the scene starts.")]
        private float cameraZoom = 1.4f;

        [Header("Player"), SerializeField]
        private float moveSpeed = 7.0f;
        [SerializeField]
        private int playerHealth = 100;
        [Header("Generator"), SerializeField]
        private int generatorHealth = 200;

        [Header("Starting Resources"), SerializeField]
        private int startingEnergy = 60;

        [Header("Waves"), SerializeField]
        private float waveDelay = 12.0f;
        [SerializeField]
        private int enemiesPerWave = 4;
        [SerializeField]
        private int waveGrowth = 2;
        [Header("Enemies"), SerializeField]
        private float enemyHealth = 40.0f;
        [SerializeField]
        private float enemyHealthGrowth = 5.0f;
        [SerializeField]
        private float enemySpeed = 2.2f;
        [SerializeField]
        private float enemyDamagePerSecond = 12.0f;
        [Header("Player Weapon"), SerializeField]
        private float playerShotDamage = 20.0f;
        [SerializeField]
        private float playerShotInterval = 0.2f;
        [Header("Bullets"), SerializeField]
        private float bulletSpeed = 30.0f;
        [SerializeField]
        private float bulletRadius = 0.15f;
        [SerializeField]
        private float bulletRange = 30.0f;

        [Header("Energy Pickups"), SerializeField]
        private int pickupEnergy = 12;

        [Header("Turrets"), SerializeField]
        private int buildCost = 40;
        [SerializeField]
        private int upgradeCost = 30;
        [SerializeField]
        private int maxTurretLevel = 8;
        [SerializeField]
        private float turretDamage = 5.0f;
        [SerializeField]
        private float turretRange = 15.0f;
        [SerializeField]
        private float turretShotInterval = 0.8f;
        [Header("Repairs"), SerializeField]
        private int repairCost = 20;
        [SerializeField]
        private int repairAmount = 60;
        [SerializeField]
        private int healCost = 15;
        [SerializeField]
        private int healAmount = 50;

        [Header("Interface"), SerializeField, Range(MinHudZoomPercent, MaxHudZoomPercent)]
        private float hudZoomPercent = 150.0f;
        [SerializeField, Tooltip("CSS style: green, pink, or blue. Also applied to world panels.")]
        private string theme = "green";
        [SerializeField]
        private float worldPanelWidth = 299.0f;
        [SerializeField]
        private float enemyPanelWidth = 184.0f;

        private readonly Enemy[] enemies = new Enemy[EnemyPoolSize];
        private readonly Pickup[] pickups = new Pickup[PickupPoolSize];
        private readonly Turret[] turrets = new Turret[TurretCount];
        private readonly Bullet[] bullets = new Bullet[BulletPoolSize];
        private readonly OutpostSnapshot snapshot = new OutpostSnapshot();
        private OutpostArena arena;
        private OutpostHud hud;
        private OutpostPanel generatorPanel;
        private float health;
        private float currentGeneratorHealth;
        private int energy;
        private int score;
        private int matchId;
        private int collectedEnergy;
        private int wave;
        private int remainingSpawns;
        private int aliveEnemies;
        private float countdown;
        private float spawnTimer;
        private float shotTimer;
        private float panelTimer;
        private bool isPaused = true;
        private bool isDefeated;
        private bool isWaveActive;
        private bool isBuildClick;
        private int hoveredPad = -1;
        private float padHoverTimer;

        public float HudZoomPercent => hudZoomPercent;
        public string Theme => theme;
        public float WorldPanelWidth => worldPanelWidth;
        public float EnemyPanelWidth => enemyPanelWidth;

        private void Start()
        {
            arena = gameObject.AddComponent<OutpostArena>();
            arena.Initialize(arenaHalfSize);
            arena.SetCameraZoom(cameraZoom);
            CreateEventSystem();
            CreatePools();
            generatorPanel = arena.CreatePanel(this, arena.Generator, "generator", 0);
            var hudObject = new GameObject("CeffyHud", typeof(RectTransform));
            hudObject.SetActive(false);
            hudObject.transform.SetParent(arena.UiRoot, false);
            hud = hudObject.AddComponent<OutpostHud>();
            hud.Initialize(this);
            hudObject.SetActive(true);
            ResetMatch();
        }

        private void OnValidate()
        {
            cameraZoom = Mathf.Clamp(cameraZoom, MinCameraZoom, MaxCameraZoom);
        }

        private void Update()
        {
            if (hud == null || !hud.IsReady)
                return;

            if (OutpostInput.WasPausePressed())
                SetPaused(!this.isPaused);
            if (!this.isPaused && !isDefeated)
                UpdateMatch(Time.deltaTime);

            panelTimer += Time.unscaledDeltaTime;
            if (panelTimer < UiUpdateInterval)
                return;

            panelTimer = 0.0f;
            UpdatePanels();
        }

        private void CreateEventSystem()
        {
            var eventObject = new GameObject("EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(arena.SystemsRoot, false);
#if ENABLE_INPUT_SYSTEM
            eventObject.AddComponent<InputSystemUIInputModule>();
#else
            eventObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private void CreatePools()
        {
            // Pools retain world-label iframes instead of creating a browser slot for every kill/spawn.
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                var body = arena.CreateEnemy();
                var panel = arena.CreatePanel(this, body, "enemy", i);
                enemies[i] = new Enemy { Body = body, Panel = panel };
            }

            for (var i = 0; i < PickupPoolSize; i++)
                pickups[i] = new Pickup { Body = arena.CreatePickup() };
            for (var i = 0; i < BulletPoolSize; i++)
                bullets[i] = new Bullet { Body = arena.CreateBullet(bulletRadius) };
            for (var i = 0; i < TurretCount; i++)
            {
                var angle = i * Mathf.PI * 2.0f / TurretCount;
                var position = new Vector3(Mathf.Sin(angle), 0.0f, -Mathf.Cos(angle)) * 4.0f;
                var body = arena.CreateTurret(position);
                var panel = arena.CreatePanel(this, body, "turret", i);
                turrets[i] = new Turret { Body = body, Panel = panel, Pad = arena.CreateBuildPad(position) };
            }
        }

        private void UpdateMatch(float deltaTime)
        {
            UpdatePlacement();
            UpdatePlayer(deltaTime);
            UpdateWaves(deltaTime);
            UpdateEnemies(deltaTime);
            UpdateTurrets(deltaTime);
            UpdateBullets(deltaTime);
            UpdatePickups(deltaTime);
            if (health <= 0.0f || currentGeneratorHealth <= 0.0f)
            {
                isDefeated = true;
                hud.Notify("Outpost lost. Restart to try another defense.");
            }
        }

        private void UpdatePlayer(float deltaTime)
        {
            var player = arena.Player;
            var position = player.position + OutpostInput.GetMovement() * (moveSpeed * deltaTime);
            position.x = Mathf.Clamp(position.x, -arenaHalfSize + 1.0f, arenaHalfSize - 1.0f);
            position.z = Mathf.Clamp(position.z, -arenaHalfSize + 1.0f, arenaHalfSize - 1.0f);
            player.position = position;
            var mouse = WebBrowserInput.GetMousePosition();
            var ray = arena.Camera.ScreenPointToRay(mouse);
            var ground = new Plane(Vector3.up, player.position);
            if (ground.Raycast(ray, out var distance))
            {
                var direction = ray.GetPoint(distance) - position;
                if (direction.sqrMagnitude > 0.0f)
                    player.rotation = Quaternion.LookRotation(direction);
            }

            shotTimer -= deltaTime;
            if (shotTimer > 0.0f || !WebBrowserInput.GetMouseButton(0)
                || isBuildClick || hud.IsPointerOverUi(mouse))
                return;

            shotTimer = playerShotInterval;
            FirePlayerShot();
        }

        private void FirePlayerShot()
        {
            SpawnBullet(arena.Player.position, arena.Player.forward, playerShotDamage);
        }

        private void SpawnBullet(Vector3 origin, Vector3 direction, float damage)
        {
            for (var i = 0; i < BulletPoolSize; i++)
            {
                var bullet = bullets[i];
                if (bullet.IsActive)
                    continue;

                origin.y = BulletHeight;
                direction.y = 0.0f;
                bullet.Body.position = origin;
                bullet.Direction = direction.normalized;
                bullet.Body.rotation = Quaternion.LookRotation(bullet.Direction);
                bullet.Damage = damage;
                bullet.DistanceLeft = bulletRange;
                bullet.IsActive = true;
                bullet.Body.gameObject.SetActive(true);
                return;
            }
        }

        private void UpdateBullets(float deltaTime)
        {
            for (var i = 0; i < BulletPoolSize; i++)
            {
                var bullet = bullets[i];
                if (!bullet.IsActive)
                    continue;

                var distance = Mathf.Min(bulletSpeed * deltaTime, bullet.DistanceLeft);
                var origin = bullet.Body.position;
                var target = FindBulletTarget(origin, bullet.Direction, distance);
                if (target != null)
                {
                    DamageEnemy(target, bullet.Damage);
                    HideBullet(bullet);
                    continue;
                }

                bullet.Body.position = origin + bullet.Direction * distance;
                bullet.DistanceLeft -= distance;
                if (bullet.DistanceLeft <= 0.0f)
                    HideBullet(bullet);
            }
        }

        private Enemy FindBulletTarget(Vector3 origin, Vector3 direction, float distance)
        {
            Enemy target = null;
            var nearestDistance = distance;
            var radius = EnemyRadius + bulletRadius;
            // Sweep the traveled segment so a fast bullet cannot skip an enemy between frames.
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                var enemy = enemies[i];
                if (!enemy.IsAlive)
                    continue;

                var offset = enemy.Body.position - origin;
                offset.y = 0.0f;
                var projected = Vector3.Dot(offset, direction);
                var perpendicularSquared = offset.sqrMagnitude - projected * projected;
                if (perpendicularSquared > radius * radius)
                    continue;

                var halfIntersection = Mathf.Sqrt(radius * radius - perpendicularSquared);
                var entry = Mathf.Max(0.0f, projected - halfIntersection);
                if (projected + halfIntersection < 0.0f || entry > nearestDistance)
                    continue;

                nearestDistance = entry;
                target = enemy;
            }

            return target;
        }

        private void HideBullet(Bullet bullet)
        {
            bullet.IsActive = false;
            bullet.Body.gameObject.SetActive(false);
        }

        private void UpdatePlacement()
        {
            isBuildClick = false;
            var mouse = WebBrowserInput.GetMousePosition();
            var pointerTarget = hud.GetPointerTarget(mouse);
            var hovered = FindHoveredPad(mouse, pointerTarget);
            var selected = KeepHoveredPad(hovered);
            if (selected != hoveredPad)
            {
                hoveredPad = selected;
                UpdatePadHighlights();
                UpdateTurretPanels();
            }

            if (hovered >= 0 && pointerTarget == null && WebBrowserInput.GetMouseButtonDown(0))
            {
                isBuildClick = true;
                BuildTurretAt(hovered);
            }
        }

        private int KeepHoveredPad(int hovered)
        {
            if (hovered >= 0)
            {
                padHoverTimer = PadHoverDelay;
                return hovered;
            }

            if (hoveredPad < 0 || turrets[hoveredPad].Level > 0 || padHoverTimer <= 0.0f)
                return -1;

            padHoverTimer -= Time.deltaTime;
            return hoveredPad;
        }

        private int FindHoveredPad(Vector2 mouse, GameObject pointerTarget)
        {
            if (pointerTarget != null)
                return FindPanelPad(pointerTarget);

            var ray = arena.Camera.ScreenPointToRay(mouse);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out var distance))
                return -1;

            var position = ray.GetPoint(distance);
            for (var i = 0; i < TurretCount; i++)
            {
                var turret = turrets[i];
                if (turret.Level > 0)
                    continue;

                var offset = turret.Body.position - position;
                offset.y = 0.0f;
                if (offset.sqrMagnitude <= BuildPadRadius * BuildPadRadius)
                    return i;
            }

            return -1;
        }

        private int FindPanelPad(GameObject pointerTarget)
        {
            for (var i = 0; i < TurretCount; i++)
            {
                var turret = turrets[i];
                if (turret.Level == 0 && turret.Panel.gameObject == pointerTarget)
                    return i;
            }

            return -1;
        }

        private void UpdatePadHighlights()
        {
            for (var i = 0; i < TurretCount; i++)
                arena.SetPadHighlight(turrets[i].Pad, i == hoveredPad);
        }

        private void UpdateWaves(float deltaTime)
        {
            if (!isWaveActive)
            {
                countdown -= deltaTime;
                if (countdown <= 0.0f)
                    StartWave();
                return;
            }

            spawnTimer -= deltaTime;
            if (remainingSpawns > 0 && spawnTimer <= 0.0f)
                SpawnEnemy();
            if (remainingSpawns > 0 || aliveEnemies > 0)
                return;

            isWaveActive = false;
            countdown = waveDelay;
            hud.Notify("Wave cleared. Collect energy and prepare your defenses.");
        }

        private void SpawnEnemy()
        {
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                var enemy = enemies[i];
                if (enemy.IsAlive)
                    continue;

                var angle = Random.value * Mathf.PI * 2.0f;
                enemy.Body.position = new Vector3(Mathf.Sin(angle) * (arenaHalfSize - 1.0f),
                    0.85f, Mathf.Cos(angle) * (arenaHalfSize - 1.0f));
                enemy.MaxHealth = enemyHealth + (wave - 1) * enemyHealthGrowth;
                enemy.Health = enemy.MaxHealth;
                enemy.IsAlive = true;
                enemy.Body.gameObject.SetActive(true);
                remainingSpawns--;
                aliveEnemies++;
                spawnTimer = SpawnInterval;
                return;
            }
        }

        private void UpdateEnemies(float deltaTime)
        {
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                var enemy = enemies[i];
                if (!enemy.IsAlive)
                    continue;

                var playerOffset = arena.Player.position - enemy.Body.position;
                playerOffset.y = 0.0f;
                var attacksPlayer = playerOffset.sqrMagnitude < PlayerAggroRange * PlayerAggroRange;
                var target = attacksPlayer ? arena.Player : arena.Generator;
                var offset = target.position - enemy.Body.position;
                offset.y = 0.0f;
                if (offset.sqrMagnitude <= AttackDistance * AttackDistance)
                {
                    if (attacksPlayer)
                        health = Mathf.Max(0.0f, health - enemyDamagePerSecond * deltaTime);
                    else
                        currentGeneratorHealth = Mathf.Max(0.0f, currentGeneratorHealth - enemyDamagePerSecond * deltaTime);
                    continue;
                }

                enemy.Body.rotation = Quaternion.LookRotation(offset);
                enemy.Body.position += offset.normalized * (enemySpeed * deltaTime);
            }
        }

        private void UpdateTurrets(float deltaTime)
        {
            for (var i = 0; i < TurretCount; i++)
            {
                var turret = turrets[i];
                if (turret.Level == 0)
                    continue;

                turret.Timer -= deltaTime;
                if (turret.Timer > 0.0f)
                    continue;
                var enemy = FindNearestEnemy(turret.Body.position);
                if (enemy == null)
                    continue;

                var offset = enemy.Body.position - turret.Body.position;
                offset.y = 0.0f;
                if (offset.sqrMagnitude <= 0.0f)
                    continue;

                turret.Body.rotation = Quaternion.LookRotation(offset);
                SpawnBullet(turret.Body.position, offset, turretDamage * turret.Level);
                turret.Timer = turretShotInterval;
            }
        }

        private Enemy FindNearestEnemy(Vector3 position)
        {
            Enemy nearest = null;
            var nearestDistance = turretRange * turretRange;
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                var enemy = enemies[i];
                if (!enemy.IsAlive)
                    continue;

                var distance = (enemy.Body.position - position).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = enemy;
            }

            return nearest;
        }

        private void DamageEnemy(Enemy enemy, float damage)
        {
            enemy.Health -= damage;
            if (enemy.Health > 0.0f)
                return;

            enemy.IsAlive = false;
            enemy.Body.gameObject.SetActive(false);
            aliveEnemies--;
            score += PointsPerEnemy;
            DropEnergy(enemy.Body.position);
        }

        private void DropEnergy(Vector3 position)
        {
            for (var i = 0; i < PickupPoolSize; i++)
            {
                var pickup = pickups[i];
                if (pickup.IsActive)
                    continue;

                pickup.Body.position = new Vector3(position.x, 0.5f, position.z);
                pickup.IsActive = true;
                pickup.Lifetime = PickupLifetime;
                pickup.Body.gameObject.SetActive(true);
                return;
            }

            // Overflow awards currency directly, so a full visual pool never loses a kill reward.
            CollectEnergy();
        }

        private void UpdatePickups(float deltaTime)
        {
            for (var i = 0; i < PickupPoolSize; i++)
            {
                var pickup = pickups[i];
                if (!pickup.IsActive)
                    continue;

                pickup.Lifetime -= deltaTime;
                var distance = (arena.Player.position - pickup.Body.position).sqrMagnitude;
                if (distance <= PickupRange * PickupRange)
                    CollectEnergy();
                else if (pickup.Lifetime > 0.0f)
                    continue;

                pickup.IsActive = false;
                pickup.Body.gameObject.SetActive(false);
            }
        }

        private void CollectEnergy()
        {
            energy += pickupEnergy;
            collectedEnergy += pickupEnergy;
        }

        private void UpdatePanels()
        {
            var canAct = !this.isPaused && !isDefeated;
            generatorPanel.SetState("GENERATOR", currentGeneratorHealth, generatorHealth, $"Repair · {repairCost}",
                canAct && energy >= repairCost && currentGeneratorHealth < generatorHealth, true);
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                var enemy = enemies[i];
                enemy.Panel.SetState("RAIDER", enemy.Health, enemy.MaxHealth, "", false, enemy.IsAlive);
            }

            UpdateTurretPanels();
        }

        private void UpdateTurretPanels()
        {
            var canAct = !this.isPaused && !isDefeated;
            for (var i = 0; i < TurretCount; i++)
            {
                var turret = turrets[i];
                if (turret.Level == 0)
                {
                    turret.Panel.SetState($"BUILD PAD {i + 1}", 0.0f, maxTurretLevel, $"Build turret · {buildCost}",
                        canAct && energy >= buildCost, hoveredPad == i);
                    continue;
                }

                turret.Panel.SetState($"TURRET {i + 1} · LV {turret.Level}", turret.Level, maxTurretLevel,
                    $"Upgrade · {upgradeCost}", canAct && energy >= upgradeCost && turret.Level < maxTurretLevel, true);
            }
        }

        public OutpostSnapshot GetState()
        {
            // Reuse the DTO; bridge serialization runs synchronously on the main thread.
            snapshot.Health = Mathf.CeilToInt(health);
            snapshot.MaxHealth = playerHealth;
            snapshot.GeneratorHealth = Mathf.CeilToInt(currentGeneratorHealth);
            snapshot.MaxGeneratorHealth = generatorHealth;
            snapshot.Energy = energy;
            snapshot.Score = score;
            snapshot.Wave = wave;
            snapshot.Enemies = aliveEnemies + remainingSpawns;
            snapshot.Countdown = Mathf.CeilToInt(Mathf.Max(0.0f, countdown));
            snapshot.Turrets = CountTurrets();
            snapshot.BuildCost = buildCost;
            snapshot.UpgradeCost = upgradeCost;
            snapshot.RepairCost = repairCost;
            snapshot.HealCost = healCost;
            snapshot.IsPaused = this.isPaused;
            snapshot.IsDefeated = isDefeated;
            snapshot.IsWaveActive = isWaveActive;
            snapshot.HudZoomPercent = hudZoomPercent;
            snapshot.Theme = theme;
            snapshot.MatchId = matchId;
            snapshot.CollectedEnergy = collectedEnergy;
            return snapshot;
        }

        private int CountTurrets()
        {
            var count = 0;
            for (var i = 0; i < TurretCount; i++)
                if (turrets[i].Level > 0)
                    count++;
            return count;
        }

        private bool CanSpend(int cost)
        {
            return !this.isPaused && !isDefeated && energy >= cost;
        }

        public string BuildTurret()
        {
            return BuildTurretAt(hoveredPad);
        }

        public string BuildTurretAt(int index)
        {
            if (index < 0 || index >= TurretCount)
                return Announce("Hover an empty build pad to choose where to place your turret.");
            if (!CanSpend(buildCost))
                return Announce("Resume play and collect enough energy to build.");
            var turret = turrets[index];
            if (turret.Level > 0)
                return Announce("This build pad is already occupied.");

            energy -= buildCost;
            turret.Level = 1;
            turret.Body.gameObject.SetActive(true);
            UpdateTurretPanels();
            return Announce("Turret built on your selected pad. Click its world panel to upgrade it.");
        }

        public string UpgradeTurret(int index)
        {
            if (index < 0 || index >= TurretCount)
                return Announce("Unknown turret.");
            var turret = turrets[index];
            if (!CanSpend(upgradeCost) || turret.Level == 0 || turret.Level >= maxTurretLevel)
                return Announce("This turret cannot be upgraded right now.");

            energy -= upgradeCost;
            turret.Level++;
            return Announce("Turret upgraded. Each level increases its damage.");
        }

        public string RepairGenerator()
        {
            if (!CanSpend(repairCost) || currentGeneratorHealth >= generatorHealth)
                return Announce("Generator repairs are unavailable right now.");

            energy -= repairCost;
            currentGeneratorHealth = Mathf.Min(generatorHealth, currentGeneratorHealth + repairAmount);
            return Announce("Generator repaired.");
        }

        public string HealPlayer()
        {
            if (!CanSpend(healCost) || health >= playerHealth)
                return Announce("Healing is unavailable right now.");

            energy -= healCost;
            health = Mathf.Min(playerHealth, health + healAmount);
            return Announce("Suit repaired.");
        }

        private string Announce(string message)
        {
            hud.Notify(message);
            return message;
        }

        public void StartWave()
        {
            if (this.isPaused || isDefeated || isWaveActive)
                return;

            wave++;
            remainingSpawns = enemiesPerWave + (wave - 1) * waveGrowth;
            spawnTimer = 0.0f;
            isWaveActive = true;
            hud.Notify($"Wave {wave} incoming. Defend the generator!");
        }

        public void SetPaused(bool isPaused)
        {
            this.isPaused = isPaused;
        }

        public void SetInputRegions(OutpostInputRegion[] regions)
        {
            hud.SetInputRegions(regions);
        }

        public void SetHudZoomPercent(float percent)
        {
            hudZoomPercent = Mathf.Clamp(percent, MinHudZoomPercent, MaxHudZoomPercent);
            hud.ApplyZoomPercent(hudZoomPercent);
        }

        public void SetTheme(string value)
        {
            if (value != "green" && value != "pink" && value != "blue")
                return;

            theme = value;
            UpdatePanels();
        }

        public void Restart()
        {
            ResetMatch();
            this.isPaused = false;
            hud.Notify("New defense started.");
        }

        private void ResetMatch()
        {
            matchId++;
            collectedEnergy = 0;
            isBuildClick = false;
            health = playerHealth;
            currentGeneratorHealth = generatorHealth;
            energy = startingEnergy;
            score = 0;
            wave = 0;
            remainingSpawns = 0;
            aliveEnemies = 0;
            countdown = waveDelay;
            shotTimer = 0.0f;
            isDefeated = false;
            isWaveActive = false;
            hoveredPad = -1;
            arena.Player.position = new Vector3(0.0f, 0.6f, -3.0f);
            for (var i = 0; i < EnemyPoolSize; i++)
            {
                enemies[i].IsAlive = false;
                enemies[i].Body.gameObject.SetActive(false);
            }

            for (var i = 0; i < PickupPoolSize; i++)
            {
                pickups[i].IsActive = false;
                pickups[i].Body.gameObject.SetActive(false);
            }

            for (var i = 0; i < TurretCount; i++)
            {
                turrets[i].Level = 0;
                turrets[i].Timer = 0.0f;
                turrets[i].Body.gameObject.SetActive(false);
            }

            for (var i = 0; i < BulletPoolSize; i++)
                HideBullet(bullets[i]);

            UpdatePadHighlights();
            UpdatePanels();
        }

        private class Enemy
        {
            public Transform Body;
            public OutpostPanel Panel;
            public float Health;
            public float MaxHealth;
            public bool IsAlive;
        }

        private class Pickup
        {
            public Transform Body;
            public float Lifetime;
            public bool IsActive;
        }

        private class Turret
        {
            public Transform Body;
            public OutpostPanel Panel;
            public Renderer Pad;
            public int Level;
            public float Timer;
        }

        private class Bullet
        {
            public Transform Body;
            public Vector3 Direction;
            public float Damage;
            public float DistanceLeft;
            public bool IsActive;
        }
    }
}
