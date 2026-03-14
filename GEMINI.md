# Delivery Driver - Detailed Project Guide

## Purpose

This document is the detailed working guide for agents operating in this repository.

It should help with:
- understanding the current verified project state
- navigating the main runtime systems
- making safe changes in high-risk areas
- avoiding outdated assumptions from older docs
- keeping repository documentation aligned with the real codebase

If this file conflicts with `AGENTS.md`, prefer the more specific statement here when it is based on the currently verified repository structure.

---

## Current Verified Repository Snapshot

### Repository identity
`Delivery Driver` is a Unity driving and delivery game project centered on:
- player vehicle control
- delivery mission flow
- quest and progression systems
- road-graph navigation and minimap guidance
- NPC traffic behaviors
- runtime-built and runtime-managed UI
- JSON save data and SQLite-backed quest/company data

### Verified Unity version
- Unity Editor: `6000.3.9f1`

### Verified package highlights
From `Packages/manifest.json`, notable packages currently present include:
- `com.unity.cinemachine` `3.1.6`
- `com.unity.inputsystem` `1.18.0`
- `com.unity.feature.worldbuilding` `1.0.1`

### Verified build settings scenes
Only these scenes are enabled in build settings:
- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/Game.unity`

Important rule:
- Do not assume playable `SettingsScene` or `CreditsScene` assets exist.
- Some scripts reference settings or credits runtime UI concepts, but the actual build settings only include `MainMenu` and `Game`.

### Verified Git/LFS detail
- `Assets/Scenes/Game.unity` is tracked with Git LFS.

### Verified branch context at inspection time
- Active branch: `feature/ui-ux-improvements`

### Verified recent repository direction
Recent repository state and code structure indicate active work around:
- speed unit preference (`KMH` / `MPH`)
- minimap and navigation presentation
- balance HUD integration
- company page and player vehicle selection flow
- camera and vehicle binding improvements

---

## Top-Level Documentation Reality

The repository currently contains these top-level markdown files:
- `AGENTS.md`
- `CLAUDE.md`
- `README.md`

When updating docs:
- keep all three aligned
- avoid claiming files or systems that do not actually exist
- distinguish verified current state from intended future work

---

## Project Overview

`Delivery Driver` combines several gameplay and infrastructure layers:

1. The player drives a vehicle through a city environment.
2. Missions are offered through a phone-style mission flow.
3. Delivery objectives create pickup and delivery locations.
4. Quest state is coordinated through a separate quest authority layer.
5. Route guidance is provided through a central navigation service.
6. UI is split between runtime-built screens and persistent global overlays.
7. Progress, settings, and portions of quest/company data are persisted.

This is not a simple scene-only Unity project. A meaningful part of the game bootstraps itself at runtime.

---

## Core Runtime Systems

## Gameplay and driving

### `CarController`
Primary player vehicle controller.
Responsibilities include:
- wheel-collider based driving
- braking and acceleration behavior
- speed/steering tuning
- center of mass handling
- hard-brake detection events
- input integration via Input System asset references

### `CameraFollow`
Main player-facing follow camera behavior.

### `DeliveryManager`
Owns mission delivery setup flow, including:
- mission offer timing
- pickup and delivery point generation
- phone mission acceptance integration
- road-graph-aware spawn selection
- neighborhood-aware delivery choices
- delivery mission variety handling
- bridge behavior into navigation and quest layers

### `DeliveryBox`, `DeliveryIndicator`, `DeliveryUI`
Support moment-to-moment delivery feedback and visuals.

### `SpeedometerUI`
Displays vehicle speed and participates in player-facing driving HUD.

### `ReverseCameraHUD`
Reverse camera-related HUD support.

---

## Quest, progression, and persistence

### `QuestManager`
The authoritative quest lifecycle coordinator.
Responsibilities include:
- active / available / completed quest state
- reward and penalty handling
- marker spawning and pooling
- cargo and delivery reward logic
- progression hooks
- audio and effect hooks
- quest save serialization entry points

### `PlayerProgressionManager`
Handles player progression state such as:
- money
- XP
- levels
- achievement tracking
- delivery statistics
- player-facing progression events

### `DriverProgressionSystem`
Handles progression features beyond raw XP/currency, such as skill-like progression bonuses and related unlock behavior.

### `GameSettings`
Persistent settings singleton backed by `PlayerPrefs`.
Currently verified settings areas include:
- master/music/SFX audio
- UI scale
- quest difficulty preference
- quality level
- resolution and fullscreen
- target FPS
- speed unit preference
- language
- accessibility settings
- minimap zoom

### `SaveManager`
JSON save/load manager.
Handles:
- save file path creation
- serializing current game state to JSON
- loading from JSON
- auto-save timing
- delete/reset save behavior

### `QuestDatabaseBootstrap`
Bootstraps SQLite database creation and schema application.

### `QuestDatabaseService`
Primary SQLite access service for quest/company-oriented persistence.

### `QuestDatabaseAutoSync`
Mirrors quest lifecycle/state into the SQLite-backed data layer.

---

## Navigation and guidance stack

### `NavigationService`
Central route/objective authority for navigation behavior.
This is the key owner of:
- current objective state
- route calculation orchestration
- route consumers for UI/world guidance

Treat it as the single source of truth for navigation state.

### `RoadGraphBuilder`
Builds road graph data from scene road content.
Verified capabilities include:
- auto-detecting road content
- EasyRoads3D-related support
- SimplePoly road extraction support
- delayed startup build
- graph availability state

### `RoadGraphPathfinder`
Consumes road graph data to calculate path results used by navigation systems.

### `ObjectiveMarker3D`
World-space objective marker subscribed to navigation state.

### `EdgeIndicator`
Screen-edge directional indicator tied to current objective state.

### `WorldRouteRenderer`
World-space route line visualization.

### `MinimapUI`
Main minimap layer.
Responsibilities include:
- minimap camera binding
- marker handling
- route preview display
- minimap zoom behavior
- road overlay rendering
- navigation subscription behavior

### `CompassUI`
Compass guidance UI tied into the navigation layer.

### `MinimapCamera`
Minimap camera follow/setup component.

---

## Traffic and world systems

### `NpcCarAgent`
Core NPC traffic driving behavior.

### `NpcSpawner`
NPC vehicle spawning and management.

### `NpcRecovery`
Recovery logic for problematic NPC states.

### `TrafficSimulationOptimizer`
Traffic-focused optimization behavior.

### `WeatherManager`
Weather-linked runtime behavior, including traffic-related interactions.

### `Neighborhood` systems
Under `Assets/Scripts/Neighborhood/`, the project includes city/neighborhood support such as:
- `Neighborhood`
- `NeighborhoodManager`
- `NeighborhoodZone`
- `NeighborhoodUI`
- neighborhood naming helpers

Namespace for these systems is `DeliveryDriver.City`.

---

## UI architecture

## Big picture

UI ownership is split across:
- scene-authored UI
- runtime-built UI
- persistent global UI infrastructure

A large portion of UI behavior is driven by code rather than only by scene hierarchy.

---

## Main menu and general UI runtime

### `MainMenuRuntimeUI`
The main menu is largely built in code.
Important consequences:
- menu changes may require editing runtime construction code, not only a scene
- settings controls in the main menu are code-built
- background, overlays, and panels are assembled dynamically

### `MenuSceneBootstrap`
Hooks into scene loading and ensures menu runtime UI behavior is injected when appropriate.

### `GlobalUiCoordinator`
One of the most important UI systems in the project.
Responsibilities include:
- ensuring a persistent global UI root
- maintaining a persistent root canvas
- adopting or reparenting scene canvases
- ensuring certain globally-scoped UI components exist
- refreshing scene bindings after scene load

This is a common source of subtle bugs if duplicate persistent roots exist.

### `PauseMenuUI`
Pause/settings runtime UI layer. It also prefers global persistent UI ownership when needed.

### `BalanceHudUI`
Balance display UI integrated into the quest/global HUD flow.

### Additional general UI components
The repository also includes UI systems such as:
- `AccessibilityManager`
- `ConfirmationDialog`
- `GameplayHUD`
- `LoadingScreenUI`
- `NotificationQueue`
- `TooltipUI`
- `UIAudioFeedback`
- `UIBuilderHelper`
- `UIButtonEnhancer`
- `UIThemeConstants`

Some of these are utility/framework-style building blocks rather than standalone screens.

---

## Quest UI layer

Under `Assets/Scripts/Quest/UI/`, the repository currently includes systems such as:
- `QuestUIManager`
- `QuestUISetup`
- `QuestListUI`
- `ActiveQuestUI`
- `QuestCompleteUI`
- `QuestStatisticsUI`
- `PauseMenuUI`
- `SettingsMenuUI`
- `SaveLoadUI`
- `ProgressionUI`
- `ProgressionSkillTreeUI`
- `TutorialUI`
- `MinimapUI`
- `CompassUI`
- `BalanceHudUI`
- `RouteLineGraphic`
- `MinimapRoadGraphic`
- `MinimapSpriteRegistry`

Important note:
- There is a `QuestUISetup` class.
- There is not a verified `QuestUISetupHelper` class in the current script set.
- Do not document or rely on a non-existent `QuestUISetupHelper` script unless it is added later.

---

## Company flow and vehicle selection

A newer, important area of the repository exists under `Assets/Scripts/Company/`.

### `CompanyPageUI`
Displays an in-game company page/overlay and pauses gameplay while the page is active.

### `PlayerVehicleManager`
Owns runtime player vehicle setup and switching logic.
Verified concerns in this area include:
- active vehicle references
- prefab-based vehicle switching
- runtime synchronization with the scene vehicle
- camera and specialized binding refresh
- restrictions while active quests are running

### `GameSceneCompanyPageInstaller`
Bootstrapper that ensures company-page-related systems exist in the `Game` scene.

### `VehicleType`
Defines player vehicle type choices such as van/truck.

Important implementation caveat:
- current prefab loading in `GameSceneCompanyPageInstaller` uses editor-only asset database loading for configured vehicle prefab paths
- if this flow is intended for player builds, that loading approach is a risk and should be revisited

---

## Verified Directory Structure

## High-value directories

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

## Verified notable asset/data directories

### `Assets/Resources/`
Currently verified entries include:
- `CargoLibrary.asset`
- `QuestDatabase.asset`
- `Minimap/`
- `UI/`
- `Localization/`

### `Assets/StreamingAssets/Database/`
Currently verified files:
- `schema.sql`
- `seed.sql`

### `Assets/Prefabs/`
Currently verified subdirectories:
- `NPCs/`
- `Quest/`
- `Vehicle/`

### `Assets/Scenes/`
Currently verified scene assets:
- `MainMenu.unity`
- `Game.unity`

### `Assets/Plugins/x86_64/`
Native plugin location used by the project. This is relevant to SQLite/native integration expectations.

---

## Namespace Map

The project mixes global namespace usage with explicit namespaces.

### Verified namespace families

- global namespace  
  Examples include several classes such as `CarController`, `DeliveryManager`, `MainMenuRuntimeUI`, `GlobalUiCoordinator`, `MenuSceneBootstrap`

- `DeliveryDriver.Quest`
- `DeliveryDriver.Quest.UI`
- `DeliveryDriver.Navigation`
- `DeliveryDriver.UI`
- `DeliveryDriver.Company`
- `DeliveryDriver.City`
- `DeliveryDriver.Optimization`
- `TrafficSystem`

Important rule:
- when moving code, duplicating code, or creating new components, preserve the intended namespace boundary
- if you relocate classes between folders, update imports carefully because this repository is not uniformly namespaced

---

## Runtime Bootstrap Map

Several systems are auto-created or auto-ensured.

## Verified bootstrap-style systems

### Before or around scene load
- `GameSettings`
- `QuestDatabaseBootstrap`
- `QuestDatabaseService`
- `MenuSceneBootstrap`

### After scene load / persistent setup
- `GlobalUiCoordinator`
- `ProgressionSceneInstaller`
- `QuestDatabaseAutoSync`
- `RuntimeOptimizationBootstrap`
- `GameSceneCompanyPageInstaller`

### On-demand / service-style UI flow
- `SceneTransitionManager`

## Practical implications

### 1. Missing scene references do not always mean missing functionality
The game often self-heals missing runtime pieces by creating them.

### 2. Duplicates are dangerous
Because many systems:
- use `FindFirstObjectByType`
- use `FindAnyObjectByType`
- persist with `DontDestroyOnLoad`

you can get inconsistent second-load behavior if duplicate systems exist.

### 3. Scene reload bugs are often lifecycle bugs
When behavior differs between:
- first launch
- returning to main menu
- entering `Game` again

suspect duplicate persistent objects, stale listeners, or rebind order problems.

---

## System Boundaries That Matter

## Delivery flow vs quest flow

These are related but not identical layers.

### `DeliveryManager`
Owns:
- mission offer UX
- pickup/dropoff point logic
- phone acceptance timing
- mission generation setup
- some navigation objective handoff behavior

### `QuestManager`
Owns:
- quest lifecycle authority
- rewards/penalties
- progression-facing completion/failure state
- save-facing quest state
- event distribution to other systems

### Important verified behavior
`DeliveryManager.Start()` forces:
- `requirePhoneMissionAccept = true`
- `useQuestSystem = true`

This means:
- phone acceptance is not optional in the current runtime flow
- the quest system is expected to be part of delivery flow
- docs should not describe an inspector-only path that bypasses those flags

If a change touches mission offers, objective timing, delivery completion, or reward state, inspect both systems together.

---

## Navigation as a central service

The navigation architecture is service-centered.

### `NavigationService` consumers include:
- `MinimapUI`
- `CompassUI`
- `ObjectiveMarker3D`
- `EdgeIndicator`
- `WorldRouteRenderer`

### Rule
If a route or objective bug appears in one place, do not assume the problem is local to that widget. The root cause may be:
- objective state not being set
- graph path failure
- subscriber binding order
- stale navigation listeners
- player transform binding issues

---

## UI ownership boundaries

### `GlobalUiCoordinator`
Persistent owner of global UI structure.

### Runtime-built screens
Examples:
- `MainMenuRuntimeUI`
- parts of pause/settings/menu flow

### Hybrid flows
Some screens can be scene-present, runtime-repaired, or runtime-created depending on what exists.

### Practical rule
If a UI bug appears:
- inspect canvas hierarchy
- inspect sorting order
- inspect whether the UI is reparented at runtime
- inspect whether duplicate persistent canvases or event systems exist

Many UI issues here are integration/lifecycle problems, not isolated widget bugs.

---

## Persistence Model

## JSON save layer

### Manager
- `SaveManager`

### Path
- save file: `savegame.json`
- location: `Application.persistentDataPath`

### Expected data categories
The save flow currently serializes:
- player progression data
- quest save data
- save date/version metadata

---

## Settings layer

### Manager
- `GameSettings`

### Backend
- `PlayerPrefs`

### Verified settings categories
- audio
- gameplay difficulty preference
- graphics and resolution
- fullscreen and target FPS
- speed unit
- language
- accessibility
- minimap zoom
- UI scale

---

## SQLite layer

### Main components
- `QuestDatabaseBootstrap`
- `QuestDatabaseService`
- `QuestDatabaseAutoSync`

### Paths/resources
- database file: `Application.persistentDataPath/quest.db`
- schema: `Assets/StreamingAssets/Database/schema.sql`
- seed: `Assets/StreamingAssets/Database/seed.sql`

### Important rule
Do not assume:
- JSON save replaces SQLite
- SQLite replaces JSON save

Both layers exist and should be considered together when modifying progression, quest persistence, or company-facing data.

---

## Commonly Touched Files

These are especially likely to matter during feature work or debugging:

- `Assets/Scripts/CarController.cs`
- `Assets/Scripts/CameraFollow.cs`
- `Assets/Scripts/DeliveryManager.cs`
- `Assets/Scripts/PhoneMissionUI.cs`
- `Assets/Scripts/RoadGraphBuilder.cs`
- `Assets/Scripts/RoadGraphPathfinder.cs`
- `Assets/Scripts/NpcCarAgent.cs`
- `Assets/Scripts/NpcSpawner.cs`
- `Assets/Scripts/NpcRecovery.cs`
- `Assets/Scripts/Quest/QuestManager.cs`
- `Assets/Scripts/Quest/PlayerProgressionManager.cs`
- `Assets/Scripts/Quest/DriverProgressionSystem.cs`
- `Assets/Scripts/Quest/GameSettings.cs`
- `Assets/Scripts/Quest/QuestDatabaseBootstrap.cs`
- `Assets/Scripts/Quest/QuestDatabaseService.cs`
- `Assets/Scripts/Quest/SaveSystem/SaveManager.cs`
- `Assets/Scripts/Quest/UI/MinimapUI.cs`
- `Assets/Scripts/Quest/UI/QuestUIManager.cs`
- `Assets/Scripts/Quest/UI/PauseMenuUI.cs`
- `Assets/Scripts/UI/GlobalUiCoordinator.cs`
- `Assets/Scripts/UI/MainMenuRuntimeUI.cs`
- `Assets/Scripts/UI/SceneTransitionManager.cs`
- `Assets/Scripts/Company/CompanyPageUI.cs`
- `Assets/Scripts/Company/PlayerVehicleManager.cs`
- `Assets/Scripts/Company/GameSceneCompanyPageInstaller.cs`

---

## Inspector / Configuration Awareness

Even in a runtime-bootstrap-heavy project, inspector values still matter.

## Particularly important areas to inspect

### `DeliveryManager`
Watch fields such as:
- `roadGraphBuilder`
- `cargoLibrary`
- `phoneMissionUI`
- road-graph spawn settings
- neighborhood restrictions
- `speedometerUI`

### `QuestManager`
Watch fields such as:
- `questDatabase`
- `cargoLibrary`
- `roadGraphBuilder`
- `playerTransform`
- `playerController`
- quest marker prefabs
- quest zone prefab
- audio clips
- particle prefabs

### `RoadGraphBuilder`
Watch fields such as:
- `autoDetectRoads`
- `sampleStepMeters`
- `connectionThresholdMeters`
- `includeSimplePolyRoads`
- `generateDualLaneSegmentsForSimplePoly`
- `buildOnStart`
- `startupBuildDelay`

### `MinimapUI`
Watch fields such as:
- minimap camera and render texture bindings
- marker prefabs
- route preview settings
- zoom ranges
- road overlay configuration

### `PlayerVehicleManager`
Watch:
- vehicle prefab configuration state
- active quest switching restrictions
- runtime synchronization behavior with the existing scene player vehicle

---

## Coding Conventions

## Naming
- PascalCase for classes, methods, enums, events
- camelCase for fields and locals

## Structure
- one main class per file unless a small helper is justified
- match file names to class names
- keep serialized fields readable and explicit
- prefer cohesive folders over unrelated large files

## Performance-aware style
- avoid unnecessary per-frame allocations
- avoid repeated expensive lookups in hot loops
- cache references where reused often
- preserve low-allocation patterns in gameplay and traffic code when possible

## Preferred Unity member order
1. `Awake`
2. `Start`
3. `Update`, `FixedUpdate`, `LateUpdate`
4. public methods
5. private methods
6. coroutines
7. cleanup/event handlers

---

## Performance Guidance

The repository includes a dedicated performance-oriented area and several optimization systems.

### Verified optimization-related systems include:
- `PerformanceOptimizationManager`
- `RuntimeOptimizationBootstrap`
- `WorldChunkManager`
- `WorldChunk`
- `HLODGroup`
- `HLODProxy`
- `AdvancedObjectPool`
- `UnifiedOptimizationController`

### Guidance
- preserve pooling behavior where it already exists
- be careful with per-frame UI allocations
- validate navigation and minimap changes for hidden update cost
- traffic-heavy logic should be evaluated with realistic NPC counts
- route rendering and road overlay refresh logic should not be casually turned into more frequent work

---

## Known Risk Areas

## High-risk technical areas
- duplicate persistent singletons after scene reload
- global canvas duplication or incorrect reparenting
- navigation subscribers not rebinding correctly
- road graph extraction edge cases
- NPC recovery loops or traffic stalls
- UI layout on non-16:9 aspect ratios
- runtime-created system order dependencies
- editor-only asset loading in company vehicle setup
- scene churn on `Game.unity`, which is LFS-tracked

## Practical debugging rule
If behavior is inconsistent between clean boot and a second load, suspect:
- `DontDestroyOnLoad` duplication
- stale event subscriptions
- multiple hidden service instances
- order-of-initialization differences

---

## Change Guidance by Area

## If you change delivery flow
Inspect together:
- `DeliveryManager`
- `QuestManager`
- `PhoneMissionUI`
- `NavigationService`
- related HUD listeners

Validate:
- offer timing
- acceptance/rejection flow
- pickup transition
- delivery objective update
- completion/failure reward handling

## If you change navigation or minimap
Inspect together:
- `NavigationService`
- `RoadGraphBuilder`
- `RoadGraphPathfinder`
- `MinimapUI`
- `ObjectiveMarker3D`
- `EdgeIndicator`
- `WorldRouteRenderer`
- `CompassUI`

Validate:
- route calculation
- objective set/clear
- marker placement
- route rendering
- player-follow behavior

## If you change UI
Inspect:
- whether the UI is runtime-built or scene-authored
- `GlobalUiCoordinator`
- canvas hierarchy and sorting order
- event system duplication
- persistence across scene transitions

## If you change company or vehicle flow
Inspect:
- `GameSceneCompanyPageInstaller`
- `CompanyPageUI`
- `PlayerVehicleManager`
- any selected-vehicle persistence path
- active quest restrictions

## If you change save/settings/progression
Inspect:
- `SaveManager`
- `GameSettings`
- `PlayerProgressionManager`
- `QuestDatabaseService`
- `QuestDatabaseAutoSync`

Validate cross-system compatibility rather than only the one file you edited.

---

## Testing Checklist

Use the subset relevant to the change.

## Always worth checking
- no compile errors
- no new console errors
- `MainMenu` still loads
- transition into `Game` still works
- no duplicate persistent UI/manager behavior after transitions

## Delivery / quest changes
- phone mission offer appears
- mission can be accepted/rejected
- pickup works
- delivery target updates
- completion/failure updates UI and progression
- rewards/penalties remain sensible

## Navigation changes
- route objective is created and cleared correctly
- minimap markers update
- world marker updates
- edge indicator updates
- route render updates
- player-follow navigation remains stable

## Settings / UI changes
- changed settings persist when expected
- speed unit changes propagate correctly
- global UI layering remains correct
- pause/settings flow still works

## Save / database changes
- first boot works with no save
- save file can be created and loaded
- SQLite initialization still succeeds
- auto-sync still binds correctly

## Company / vehicle changes
- company page appears in `Game`
- selected vehicle applies correctly
- player control still works
- camera and bindings still work
- active quest vehicle-switch restrictions still hold

---

## Git and Asset Handling Guidance

### Branching expectations
- treat `main` as stable
- keep work logically grouped
- prefer clear, user-visible commit intent

### Scene and prefab safety
- avoid noise-only scene changes
- be extra careful with `Assets/Scenes/Game.unity`
- review prefab churn before committing

### LFS note
Because `Game.unity` is LFS-tracked, avoid casual scene edits that do not materially support the intended change.

---

## Documentation Maintenance Rules

When editing docs in this repository:

1. Do not claim scenes that are not in build settings.
2. Do not invent helper scripts that are not present.
3. Prefer verified file/class names over broad assumptions.
4. Keep `AGENTS.md`, `CLAUDE.md`, and `README.md` synchronized.
5. Separate current truth from roadmap language.
6. Call out implementation caveats when a system is partially editor-only or otherwise risky.

---

## Quick Decision Rules

- If the change affects mission offers, inspect `DeliveryManager` and `QuestManager` together.
- If the change affects route guidance, inspect `NavigationService` and all major consumers together.
- If the change affects HUD behavior, inspect both local widget logic and `GlobalUiCoordinator`.
- If the change affects persistence, inspect JSON save, `PlayerPrefs`, and SQLite together.
- If the change affects the main menu, remember it is code-built.
- If the change affects startup behavior in `Game`, expect runtime bootstrap systems to be involved.
- If a bug only appears after reloading scenes, suspect duplicate persistent objects first.