# Gameplay HUD Demo: Outpost

A self-contained top-down wave-defense game demonstrating a transparent Ceffy HUD and actual world-space HTML panels. The world uses Unity primitives and the active render pipeline's default material. There are no downloaded assets, web fonts, custom gameplay shaders, physics packages, or network requests. Ceffy's own browser-display material remains part of the package.

## Run

1. Install Ceffy and import **Gameplay HUD Demo** from Package Manager → Ceffy → Samples.
2. Open `GameplayHudDemo.unity` and press Play. The scene builds its arena, camera, lighting, input system, pools, and UI at runtime.
3. Click **Begin defense**. Hover the build pad you want and click it, or use its floating **Build turret** button, before the countdown expires.

The package's sample importer copies the web files, including the `themes/` directory, to `Assets/StreamingAssets/Demos/GameplayHud/`. Relative links keep HTML, CSS, and JavaScript together. After import, edit those copied web files; the importer removes the sample's original StreamingAssets folder. Reimporting replaces the copied files.

Use Ceffy's supported Windows graphics/native runtime configuration from the main package README. Input works with legacy input or the Input System; no custom input-action asset is required. The scene owns its camera and EventSystem: open it by itself rather than loading it additively into another game.

## Play

| Control | Action |
| --- | --- |
| WASD | Move the blue player capsule |
| Mouse | Aim on the arena plane |
| Hold left click | Shoot; clicks on HTML controls do not fire |
| Escape / HUD Help | Pause or resume and show the controls |
| Walk over gold cubes | Collect energy dropped by raiders |
| Hover / click an empty pad | Highlight that pad and build a turret there |
| Empty pad world panel | Build a turret on that specific pad |
| Turret world panel | Buy a damage upgrade, up to level eight |
| Generator world panel / HUD | Repair the cyan generator |
| Repair suit | Restore player health |
| Deploy next wave | Skip the preparation countdown |
| Restart defense | Reset the match without recreating browsers |
| Style / Size | Persistent settings on the left during gameplay, beside or below the briefing while paused |
| HUD zoom slider | Drag to select 75–200%; release to apply native browser zoom |
| Green / Pink / Blue | Switch between terminal, soft light-card, and angular tech styles on the HUD and world panels |

Raiders pursue the player when nearby; otherwise they attack the generator. Waves grow in size and enemy health. Defeat occurs when either the player or generator reaches zero health. There is no final wave: try to beat your score. Energy expires after 35 seconds, so collect it during the preparation period.

Gameplay tuning is serialized on the scene's `Outpost` component and grouped under Arena, Camera, Player, Generator, Starting Resources, Waves, Enemies, Player Weapon, Bullets, Energy Pickups, Turrets, Repairs, and Interface headers. Bullets expose speed, radius, and range. Interface settings expose the initial browser zoom, CSS theme, and world-panel widths in screen pixels. These defaults apply only to this sample scene. Editing them does not change existing package prefabs or other samples.

The default arena is 44 × 44 world units and uses a perspective camera positioned relative to the arena size. Under Camera, `Camera Zoom` defaults to 1.4 for a closer view: 1 restores the original distance, and higher values move closer without changing the viewing angle. Set it before entering Play Mode. Camera zoom is independent of HUD browser zoom; world panels retain their configured screen widths. Bullets travel at 30 world units per second. Turrets have eight levels, deal 5 damage per level, have a range of 15 world units, and fire every 0.8 seconds. The scene and component defaults use the same tuning.

Generated objects are grouped beneath `Outpost`: `Map` contains the floor, `Grid`, and `BuildPads`; `Actors` contains the player and generator; `Enemies`, `EnergyPickups`, `Turrets`, and `Bullets` contain their pooled bodies. `CameraRig` contains the camera and light, `Systems` contains the EventSystem, and `UI` contains the HUD and `WorldPanels`, with separate enemy and turret panel groups. World panels remain outside pooled bodies so hiding a body preserves its browser slot.

Player and turret weapons launch pooled sphere objects. Damage occurs when a bullet reaches a raider, with a sweep over its traveled distance to avoid skipping targets between frames. Bullets stop moving during pause, disappear on impact or at their configured range, and are cleared on restart.

## Read the integration

| File | Responsibility |
| --- | --- |
| `OutpostGame.cs` | Match state, pooled enemies/pickups/bullets, combat, hovered-pad selection, economy, purchase validation |
| `OutpostArena.cs` | Procedural geometry, built-in materials, camera, bullet bodies, pad highlights, panel construction |
| `OutpostHud.cs` | Dedicated browser, typed RPC bridge, page handshake, snapshots, Unity raycast filtering |
| `OutpostPanel.cs` | Fixed-size shared browser slots on world-space Canvases, following targets, JSON messaging |
| `IOutpostCommands.cs` | `[UnityMethods]` contract: JavaScript → gameplay, with Promise return values |
| `IOutpostUi.cs` | `[UiMethods]` contract: gameplay → HTML state and notifications |
| `OutpostSnapshot.cs` / `OutpostInputRegion.cs` | Shared DTOs; bridge JSON uses camelCase |
| `OutpostInput.cs` | WASD/Escape compatibility with both input backends |
| `index.html`, `hud.css`, `hud.js` | HUD structure, styling, and typed bridge client |
| `numbers.js` | Shared JavaScript animation loop for counters and health bars |
| `panel.html`, `panel.css`, `panel.js` | Reusable world-panel structure, styling, and raw message client |
| `themes/green.css`, `themes/pink.css`, `themes/blue.css` | Shared colors, typography, panel shapes, button styling, and progress-bar effects |

### Page readiness instead of delays

The local HUD sends `outpost:ready` after its script has loaded and the native `ceffy.SendToUnity` transport exists. C# injects the bridge runtime and the bound method stubs in response. JavaScript registers its UI handlers on `ceffy:ready` and waits for the command stubs before requesting its first snapshot. No texture-ready assumption or arbitrary startup sleep is used. Gameplay starts paused so initial browser loading cannot cost health.

`OutpostHud.Update` drains `CeffyBridge.ProcessMessages` on the main thread. Snapshots are pushed at 10 Hz, independent of gameplay pause. Buttons await the generated `ceffy.unity` methods and refresh state after their responses. C# owns currency and validates every purchase, including commands originating in world panels. UI availability is feedback, not authorization.

### Animating numbers in JavaScript

`numbers.js` animates energy, score, suit health, and generator health with one shared `requestAnimationFrame` loop. New targets ease from the currently displayed value, so rapid snapshots and overlapping pickups do not jump back or repeatedly restart unchanged animations. Health bars follow the same interpolated values. The first snapshot and match restarts snap immediately to their authoritative values.

Pickup feedback uses the Web Animations API to pulse the energy number and float a `+N E` badge. Overlapping pickups accumulate into one badge rather than creating unbounded DOM elements. The snapshot includes a cumulative collected-energy total, so a pickup is still shown when a purchase between snapshots makes the net currency change negative. The match identifier prevents restart balances from being mistaken for pickups. Animations respect reduced-motion preferences and settle immediately when the page is hidden. Purchase availability always uses the real snapshot values, not the animated counters.

### Transparency and pointer ownership

A transparent HTML background does **not** make a Unity RawImage pass clicks through. `hud.js` reports normalized rectangles for `[data-hit]` elements whenever their layout changes. `OutpostHud` implements `ICanvasRaycastFilter` to accept only those regions. An open modal claims the full page; closing it restores gameplay click-through. The operations panel claims its scrollable area, including its expandable integration notes.

Before firing, gameplay raycasts through the same EventSystem used by Ceffy. A HUD control or interactive world panel suppresses firing. Passive enemy labels have disabled GraphicRaycasters: the view still uses its topmost-hit input gate, but the label never becomes a hit. Browser keyboard forwarding is disabled so WASD remains gameplay input even after clicking an HTML button.

Hovering an empty pad highlights it and reveals its build panel. The panel remains visible briefly while moving the cursor from the ground to its button. A build request includes that pad's index; there is no automatic choice of the first free pad. Hovering the ground pad does not interrupt held fire; only a fresh build click consumes that frame's shot. Clicking an HTML build or upgrade control retains normal UI click ownership.

### Browser zoom and CSS themes

The HUD defaults to 150% and uses `CeffyInstance.SetZoomPercent`, the same native API used by the Zoom Demo. The HTML slider displays its chosen value during dragging and applies it on release, preventing browser reflow from moving the slider beneath the pointer. Appearance controls are disabled until connected and while a HUD command is pending. The settings panel moves into the briefing layout while paused, sitting beside it on wide views and below it in a shared scrollable area on narrow views. During gameplay it returns to the left side. The operations panel follows the measured toolbar height, and hit regions are recomputed after zoom, theme loading, and layout changes.

Style buttons call `SetTheme` through the typed bridge. Each page switches its stylesheet link to a local file under `themes/`; theme files supply color and font variables plus visual overrides for panel shapes, buttons, and progress bars. Essential world-panel numbers retain the same font size across themes. The selected theme is included in both HUD snapshots and world-panel messages. Zoom and theme choices survive match restart; a new Play session starts with the scene's Interface settings.

### One shared atlas, multiple world panels

The generator, three turret/build panels, and twelve pooled enemy labels use `UseSharedInstance = true`, fixed 320 × 144 viewports, and one shared atlas. Each panel has a world-space Canvas with the gameplay camera assigned, preserving Ceffy's local-space mouse projection. Their rotation matches the fixed camera, and their position follows their target in LateUpdate. Their world scale is calculated from the perspective camera's field of view and each panel's depth, preserving the configured screen-pixel width across distance and Game view resizing. Shared panels use larger CSS text rather than unsupported shared-browser zoom.

Enemy labels show health. The generator exposes repair; hovered empty pads expose build; built turrets expose upgrade. Each iframe sends `panel:ready`, then receives JSON through `ceffy.onMessageFromUnity` and sends its supplied command through `ceffy.SendToUnity`. It does not need its own typed bridge. State is resent on the handshake and thereafter only when the panel or theme changes.

Hiding a panel disables its Canvas and raycaster, preserving the Ceffy component, iframe, and atlas slot for reuse. Enemy, pickup, and bullet bodies are also pooled. Restart reuses all of them. The default 2048 × 2048 shared atlas has ample room for sixteen 320 × 144 panels plus padding; smaller global overrides may prevent allocation.

### Lifecycle and extending the sample

All game state belongs to the scene instance; there are no mutable static sample fields. Bridges and message handlers are disposed/unsubscribed on destruction, and generated materials are destroyed with the arena. Play-session resets remain the responsibility of the package's existing runtime initialization hooks.

To add an ability, extend `IOutpostCommands`, implement the method on `OutpostGame`, and add a button calling its camelCase JavaScript stub. To add a new display value, extend `OutpostSnapshot` and update the DOM from `updateState`. The package's TypeScript generator can discover these contracts after sample import; configure its output in Ceffy's bridge settings if you want generated declarations. No generated declaration file is needed to run this JavaScript sample.

## Verification checklist

- Begin a match; move, aim, and shoot. Gold pickups increase energy and kills increase score.
- Buy a turret, upgrade it from its world panel, damage and repair the generator, and repair the suit.
- Click HUD/world buttons while holding fire: no gameplay shot should originate from a UI click.
- Expand integration notes, resize the Game view, and test clicks near panel edges.
- Pause and resume by both Escape and HTML controls; the countdown and damage must stop while paused.
- Clear a wave, skip a countdown, lose a match, and restart. Score, health, energy, waves, and turret levels reset.
- Enter Play twice with domain reload disabled; verify no stale handlers, labels, or commands.
- Check legacy input and Input System projects, and your project's active render pipeline.

The scene intentionally illustrates gameplay UI rather than every package API. Navigation, remote websites, and drag/drop remain covered by their dedicated samples.
