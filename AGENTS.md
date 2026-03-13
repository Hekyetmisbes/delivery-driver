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

- `Assets/Scripts/`
- `Assets/Scripts/Quest/`
- `Assets/Scripts/Quest/UI/`
- `Assets/Scripts/Quest/SaveSystem/`
- `Assets/Scripts/Navigation/`
- `Assets/Scripts/UI/`
- `Assets/Scripts/Company/`
- `Assets/Scripts/Neighborhood/`
- `Assets/Scripts/Performance/`
- `Assets/Scenes/`
- `Assets/Resources/`
- `Assets/Prefabs/`
- `Assets/StreamingAssets/Database/`
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
- `RoadGraphBuilder`: road graph generation
- `RoadGraphPathfinder`: graph path search
- `NpcCarAgent`, `NpcSpawner`, `NpcRecovery`: traffic stack

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
- `Assets/Scripts/CarController.cs`
- `Assets/Scripts/CameraFollow.cs`
- `Assets/Scripts/DeliveryManager.cs`
- `Assets/Scripts/RoadGraphBuilder.cs`
- `Assets/Scripts/RoadGraphPathfinder.cs`
- `Assets/Scripts/NpcCarAgent.cs`
- `Assets/Scripts/Quest/QuestManager.cs`
- `Assets/Scripts/Quest/PlayerProgressionManager.cs`
- `Assets/Scripts/Quest/GameSettings.cs`
- `Assets/Scripts/Quest/SaveSystem/SaveManager.cs`
- `Assets/Scripts/Quest/UI/MinimapUI.cs`
- `Assets/Scripts/Quest/UI/QuestUIManager.cs`
- `Assets/Scripts/Quest/UI/PauseMenuUI.cs`
- `Assets/Scripts/UI/GlobalUiCoordinator.cs`
- `Assets/Scripts/UI/MainMenuRuntimeUI.cs`
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

## Git Workflow Expectations

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