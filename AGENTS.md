# Delivery Driver - General Agent Guide

## Purpose

This document is the concise operating guide for agents working in this repository.

Use it for:
- quick repository orientation
- current project constraints
- safe change guidelines
- high-risk system awareness

For deeper architectural context and a fuller project snapshot, also read `CLAUDE.md`.

If guidance conflicts, prefer the more detailed and more recently updated information in `CLAUDE.md`.

---

## Repository Summary

`Delivery Driver` is a Unity driving and delivery game project focused on:
- vehicle handling and driving feedback
- delivery and quest gameplay
- player progression and rewards
- NPC traffic and road-graph navigation
- layered runtime-built UI
- save/load plus SQLite-backed quest history

The repository is script-heavy and mixes scene-authored objects with runtime bootstrap systems.

---

## Current Verified Project State

### Unity and packages
- Unity Editor version: `6000.3.9f1`
- Input System package present
- Cinemachine package present
- worldbuilding package present

### Build scenes
Only these scenes are enabled in build settings:
- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/Game.unity`

Do not assume playable `SettingsScene` or `CreditsScene` assets exist just because some scripts reference those names.

### Git/LFS
- `Assets/Scenes/Game.unity` is tracked with Git LFS
- Treat scene edits as high-risk and avoid noise-only scene changes

### Current branch snapshot reflected in repository metadata
- branch: `feature/ui-ux-improvements`

Recent project direction includes:
- speed unit settings (`KMH` / `MPH`)
- balance HUD integration into the global UI flow
- minimap and navigation UI work
- company page and vehicle selection flow

---

## Important Top-Level Files

- `AGENTS.md`: concise operating guide
- `CLAUDE.md`: detailed project guide and architecture notes
- `README.md`: user-facing repository overview

When updating docs, keep these aligned with the real repository state.

---

## Important Directories

- `Assets/Art/` (`Materials/`, `Terrains/`, `Textures/`)
- `Assets/Editor/` (`Build/`, `City/`, `Quest/`, `Setup/`, `Traffic/`)
- `Assets/Prefabs/`
- `Assets/Resources/`
- `Assets/Scenes/`
- `Assets/Scripts/` (`City/`, `Company/`, `Core/`, `Gameplay/`, `Navigation/`, `Optimization/`, `Performance/`, `Quest/`, `Traffic/`, `UI/`)
- `Assets/Settings/` (`Input/`)
- `Assets/StreamingAssets/Database/`
- `Assets/ThirdParty/` (`EasyRoads3D/`, `KenneyUI/`, `LowPolyCity/`, `Vehicles/`, etc.)
- `Assets/Plugins/x86_64/`

---

## Major Runtime Systems

### Gameplay and vehicle
- `CarController`: core player vehicle controller
- `CameraFollow`: player camera behavior
- `DeliveryManager`: delivery flow, mission offer logic, pickup and dropoff spawning
- `SpeedometerUI`: speed HUD
- `ReverseCameraHUD`: reverse camera support

### Quest and progression
- `QuestManager`: central quest lifecycle
- `PlayerProgressionManager`: money, XP, level, statistics, achievements
- `DriverProgressionSystem`: progression bonuses and skill-style systems
- `SaveManager`: JSON save/load entry point
- `GameSettings`: PlayerPrefs-backed settings singleton

### Navigation and traffic
- `NavigationService`: central route/objective service
- `ObjectiveMarker3D`: world objective marker
- `EdgeIndicator`: edge-of-screen navigation indicator
- `WorldRouteRenderer`: world-space route line
- **`RoadNetworkManager` (NEW):** Central authority for the Spline-based road network (replaces the old `RoadGraphBuilder`).
- **`SplineRoad` & `RoadIntersection` (NEW):** The core data structures for the new traffic system. Spline curves provide smooth paths for realistic physics-based steering, while intersections handle right-of-way and yielding.
- **`SplinePathfinder` (NEW):** Calculates A* routes over the simplified graph of intersections and roads (replaces the old `RoadGraphPathfinder`).
- `NpcCarAgent`, `NpcSpawner`, `NpcRecovery`: traffic stack (updated to use Spline "Pure Pursuit" steering instead of discrete waypoints for much better performance and realistic vehicle turning).

### UI
- `GlobalUiCoordinator`: persistent global canvas adoption layer
- `MainMenuRuntimeUI`: main menu is largely built in code
- `PauseMenuUI`: pause/settings flow
- `QuestUIManager`: quest HUD coordination
- `MinimapUI`: minimap, route preview, marker management
- `BalanceHudUI`: balance panel attached to global UI flow

### Company flow
- `CompanyPageUI`: company setup/overlay in game scene
- `PlayerVehicleManager`: runtime player vehicle switching/setup
- `GameSceneCompanyPageInstaller`: scene bootstrap for company page flow

### Cinematic & Video Recording (NEW)
- `CinematicCameraController`: multi-angle cinematic camera rig (Hood, Wheel, Drone, Flyby, Profile, Exhaust) supporting 16:9 Trailer and 9:16 Reels formats
- `CinematicDirector`: automated intelligent camera cutting based on driving dynamics
- `CinematicRecorderController`: Unity Recorder 5.1.7 deterministic 60 FPS MP4 video recorder
- `CinematicCaptureHUD`: in-game status HUD with clean footage toggle and hotkeys
- `CinematicCaptureWindow`: Editor studio window under `Delivery Driver > Cinematic Capture & Director`

---

## Scene and Bootstrap Reality

This project uses many runtime-created systems.

### Known bootstrap behavior
The following systems are created automatically at runtime or ensured on scene load:
- `GameSettings`
- `QuestDatabaseBootstrap`
- `QuestDatabaseService`
- `MenuSceneBootstrap`
- `GlobalUiCoordinator`
- `ProgressionSceneInstaller`
- `QuestDatabaseAutoSync`
- `SceneTransitionManager`
- `GameSceneCompanyPageInstaller`
- `CinematicCaptureInstaller`

### Practical rule
If something seems missing in a scene, do not assume the feature is absent. Check whether it is created at runtime.

### Important consequence
Do not casually add duplicate singleton-like objects to scenes. Many systems use runtime discovery and duplicates can cause inconsistent behavior, especially after scene reloads.

---

## Architecture Notes That Matter During Edits

### Delivery flow vs quest flow
There are two related systems:
- `DeliveryManager` handles mission offer UX, pickup/dropoff spawning, and moment-to-moment delivery setup
- `QuestManager` handles authoritative quest state, rewards, penalties, progression hooks, and save-facing state

When changing mission acceptance, objective visibility, or delivery completion, inspect both systems.

### Current enforced delivery behavior
`DeliveryManager` forces these values at runtime in `Start()`:
- `requirePhoneMissionAccept = true`
- `useQuestSystem = true`

Do not document or implement flows that bypass phone acceptance unless you intentionally change this behavior.

### Navigation ownership
`NavigationService` is the central source of route and objective state.
UI and world guidance systems subscribe to it:
- `MinimapUI`
- `CompassUI`
- `EdgeIndicator`
- `ObjectiveMarker3D`
- `WorldRouteRenderer`

If route guidance changes, validate the whole stack rather than just one widget.

### UI ownership
`GlobalUiCoordinator` owns a persistent global canvas and may adopt scene canvases beneath it.
Many UI bugs come from:
- duplicate persistent UI roots
- canvas sort order problems
- runtime-created overlays
- scene reload listener duplication

---

## Namespace Map

This repository mixes global-namespace and namespaced classes.

### Common namespaces
- global namespace: several gameplay and UI bootstrap classes such as `CarController`, `DeliveryManager`, `MainMenuRuntimeUI`, `GlobalUiCoordinator`
- `DeliveryDriver.Quest`
- `DeliveryDriver.Quest.UI`
- `DeliveryDriver.Navigation`
- `DeliveryDriver.UI`
- `DeliveryDriver.Company`
- `DeliveryDriver.City`
- `DeliveryDriver.Optimization`
- `TrafficSystem`

When moving code, preserve namespace intent and fix imports carefully.

---

## Data and Persistence

### JSON save
- manager: `SaveManager`
- file: `savegame.json`
- location: `Application.persistentDataPath`

### Settings
- manager: `GameSettings`
- storage: `PlayerPrefs`

Known settings include:
- audio
- graphics
- UI scale
- language
- accessibility
- minimap zoom
- speed unit preference

### SQLite
- bootstrap: `QuestDatabaseBootstrap`
- service: `QuestDatabaseService`
- sync: `QuestDatabaseAutoSync`
- database file: `quest.db`
- schema path: `Assets/StreamingAssets/Database/schema.sql`
- seed path: `Assets/StreamingAssets/Database/seed.sql`

Do not assume JSON save replaces SQLite history or vice versa. Both exist.

---

## File and Folder Guidance

### Often edited files
- `Assets/Scripts/Gameplay/Vehicle/CarController.cs`
- `Assets/Scripts/Gameplay/Camera/CameraFollow.cs`
- `Assets/Scripts/Gameplay/Delivery/DeliveryManager.cs`
- `Assets/Scripts/Navigation/NavigationService.cs`
- `Assets/Scripts/Traffic/Spline/SplineRoad.cs`
- `Assets/Scripts/Traffic/Spline/RoadNetworkManager.cs`
- `Assets/Scripts/Traffic/NpcCarAgent.cs`
- `Assets/Scripts/Traffic/NpcSpawner.cs`
- `Assets/Scripts/Quest/QuestManager.cs`
- `Assets/Scripts/Quest/PlayerProgressionManager.cs`
- `Assets/Scripts/Core/Settings/GameSettings.cs`
- `Assets/Scripts/Core/Save/SaveManager.cs`
- `Assets/Scripts/UI/Minimap/MinimapUI.cs`
- `Assets/Scripts/UI/Quest/QuestUIManager.cs`
- `Assets/Scripts/UI/Menus/PauseMenuUI.cs`
- `Assets/Scripts/UI/Core/GlobalUiCoordinator.cs`
- `Assets/Scripts/UI/Menus/MainMenuRuntimeUI.cs`
- `Assets/Scripts/Company/CompanyPageUI.cs`
- `Assets/Scripts/Company/PlayerVehicleManager.cs`

### Resource assets currently relevant
- `Assets/Resources/CargoLibrary.asset`
- `Assets/Resources/QuestDatabase.asset`
- `Assets/Resources/Minimap/`
- `Assets/Resources/UI/`

---

## Coding Conventions

- Use PascalCase for classes, methods, enums, and events
- Use camelCase for fields and locals
- Match file names to class names
- Keep public serialized fields explicit and readable
- Prefer low-allocation code in gameplay loops
- Avoid repeated expensive component lookups in per-frame code
- Use one main class per file unless a small helper is justified

### Preferred Unity member order
1. `Awake`
2. `Start`
3. `Update` / `FixedUpdate` / `LateUpdate`
4. public methods
5. private methods
6. coroutines
7. event handlers / cleanup

---

## Editing Rules

### Before changing gameplay logic
Check whether the behavior is controlled by:
- scene references
- runtime bootstrap
- singleton instance lookup
- both delivery and quest systems
- both navigation service and UI listeners

### Before changing UI
Check:
- whether UI is scene-authored or runtime-built
- whether `GlobalUiCoordinator` reparents it
- whether the element persists across scenes
- whether duplicate event systems or duplicate canvases can exist

### Before changing company or vehicle flow
Check:
- `GameSceneCompanyPageInstaller`
- `CompanyPageUI`
- `PlayerVehicleManager`
- selected vehicle persistence in quest/database systems
- active quest restrictions around vehicle switching

### Before changing route, minimap, or NPC logic
Validate together:
- road graph generation
- pathfinding
- objective updates
- minimap markers
- world route rendering
- NPC route usability

### Before changing save/progression/settings
Check compatibility across:
- `SaveManager`
- `GameSettings`
- `QuestDatabaseService`
- `QuestDatabaseAutoSync`

---

## Known Risk Areas

- duplicate singleton objects after scene reload
- runtime UI layering conflicts
- road graph extraction edge cases
- NPC recovery loops in rare scenarios
- non-16:9 UI layout issues
- performance degradation with high NPC counts
- editor-only asset loading paths in company vehicle setup if moved into player builds without adjustment

That last point matters because some company vehicle prefab loading currently relies on editor-only asset database access.

---

## Testing Checklist

Use the subset relevant to your change.

### Always important
- project still compiles
- no new console errors
- `MainMenu` still loads
- start game flow still reaches `Game`
- no duplicate persistent manager/UI behavior after scene transitions

### Delivery and quest changes
- phone mission offer appears correctly
- accepting a mission starts the intended flow
- pickup works
- delivery objective updates correctly
- completion/failure updates progression and UI
- rewards and penalties still apply correctly

### Navigation changes
- route objective is set and cleared correctly
- minimap markers update
- world marker updates
- edge indicator updates
- route rendering behaves correctly

### Settings/UI changes
- settings persist across restart when applicable
- speed unit changes propagate to HUD/UI
- pause/settings flow still works
- global canvas layering remains correct

### Save/database changes
- first boot works without existing save data
- save file can be created and loaded
- SQLite setup still initializes
- quest sync still binds correctly

### Company/vehicle changes
- company page appears in `Game`
- selected vehicle applies correctly
- runtime vehicle setup does not break player control or camera binding
- vehicle switching restrictions still behave during active quests

---

## Autonomous Execution and Git Policy

### Autonomy and approvals
- Perform all read-only inspection operations (reading files, searching codebase, directory listings, checking git status/diff) automatically without asking for confirmation.
- Handle routine Git workflows autonomously (`git status`, `git diff`, `git add`, `git commit`). Do not stop or ask for confirmation before staging or committing changes for completed tasks.
- Keep commits logically grouped and write descriptive, concise commit messages (e.g., `feat:`, `fix:`, `refactor:`, `perf:`, `docs:`).
- Only pause and ask for confirmation on destructive or high-risk actions (`git reset --hard`, `git push --force`, deleting branches/scenes/data, schema migrations).
- Carry tasks through from implementation to verification and git commit end-to-end without unnecessary pauses for trivial approvals.

### Git Workflow Expectations
- Keep commits logically grouped
- Prefer user-visible or system-scope commit messages
- Treat `main` as stable
- Use care with scene and prefab churn
- Avoid committing editor noise

### Before push
- verify branch
- verify working tree
- verify LFS state if scene/assets changed

### If the user asks for commit/push all
Prefer multiple logical commits instead of a single giant one.

---

## Developer Guidelines and Performance Rules

### Language and communication
- Explain thoughts, rationale, and summaries in Turkish.
- Keep all code, Unity API names, engine terms, class names, component names, and technical terminology in English.

### Unity C# and mobile optimization
- Target stable 60 FPS and low memory/build footprint for mobile.
- Zero/Low GC allocations in per-frame loops (`Update`, `FixedUpdate`, `LateUpdate`). Avoid LINQ, string concatenations, or `new` allocations in hot loops.
- Cache component and object references in `Awake`/`Start`; avoid repeated `GetComponent` or `Find*` in update loops.
- Treat `Assets/Scenes/Game.unity` as high-risk (Git LFS tracked). Avoid accidental or noise-only scene modifications.

---

## Documentation Maintenance Rules

When updating repository docs:
- keep `AGENTS.md`, `CLAUDE.md`, and `README.md` consistent
- do not claim scenes, files, or systems that are not actually present
- clearly separate verified current state from future intentions
- prefer concrete file/class names over vague descriptions

---

## Quick Decision Rules

- If it affects mission offers or delivery objective flow, inspect `DeliveryManager` and `QuestManager` together
- If it affects navigation, inspect `NavigationService` and all main consumers together
- If it affects HUD or overlays, inspect local widget logic and `GlobalUiCoordinator`
- If it affects persistence, inspect JSON save, PlayerPrefs settings, and SQLite sync together
- If it affects the main menu, remember the menu is code-built
- If it affects the game scene startup, expect runtime bootstrap systems to be involved