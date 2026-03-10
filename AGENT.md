# Delivery Driver - Agent Guide

## Purpose

This file is the single working reference for agents operating in this repository. It combines the short operational notes from `AGENTS.md` with the broader architectural guidance from `AGENT_GUIDE.md`.

When information conflicts, prefer the most recent dated snapshot in this document over older implementation notes.

## Current Project Snapshot (2026-03-07)

- Active branch at snapshot time: `feature/ui-ux-improvements`
- Snapshot state: local and `origin/feature/ui-ux-improvements` were in sync
- Latest delivered commits:
  - `f7b3300` Integrate balance HUD into global UI flow
  - `6412d1f` Add configurable speed units across HUD and settings
  - `9d806c5` Tune hard brake detection thresholds

## Recent Functional Progress

- Added speed unit preference (`KMH` / `MPH`) in settings flow.
- Refactored and improved in-game speedometer UI behavior and visuals.
- Integrated `BalanceHudUI` into the global UI coordinator lifecycle.
- Tuned hard-brake notification thresholds to reduce noisy triggers.

## Project Overview

`Delivery Driver` is a Unity-based 3D driving and cargo delivery game. The project combines vehicle physics, quest generation, NPC traffic, progression systems, save/load support, and layered UI feedback.

### Core game loop

1. Player reviews available delivery quests.
2. Player accepts a quest and drives to pickup.
3. Cargo is loaded and affects vehicle handling.
4. Player delivers within the time limit.
5. Rewards, XP, and unlocks are granted.
6. The loop repeats with harder or more varied quests.

### Technology stack

- Unity 2022.3+ LTS
- C# 10
- Unity Input System
- Unity PhysX with `WheelCollider`
- EasyRoads3D integration where available
- `JsonUtility` plus custom save structures
- Event-driven, manager-centric architecture
- SQLite-backed quest telemetry and persistence helpers

## Scene Entry Points

### Build settings scenes

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/Game.unity`

There are currently only two enabled scenes in build settings. Do not assume separate playable `SettingsScene` or `CreditsScene` assets exist just because some runtime bootstrap code mentions those names.

### Runtime scene behavior

- `MainMenu.unity` is heavily runtime-built through `MainMenuRuntimeUI`.
- `Game.unity` contains core scene objects such as `QuestManager` and `GlobalUIRoot`.
- `Game.unity` also contains `QuestUISetupHelper`, so quest UI can be built or repaired at runtime.

## Runtime Bootstrap Map

Several systems create themselves automatically. Missing scene references do not always fail loudly because fallback creation is common.

### Systems created before or during scene load

- `GameSettings`: auto-created before scene load and persisted with `DontDestroyOnLoad`
- `QuestDatabaseBootstrap`: auto-created before scene load to initialize `quest.db`
- `QuestDatabaseService`: auto-created before scene load for SQLite access
- `MenuSceneBootstrap`: listens for scene loads and injects menu runtime UI when needed
- `GlobalUiCoordinator`: ensured after scene load and persisted across scenes
- `ProgressionSceneInstaller`: ensured after scene load; spawns progression systems if missing
- `QuestDatabaseAutoSync`: ensured after scene load; mirrors quest lifecycle into SQLite
- `SceneTransitionManager`: created on demand when a scene transition is requested

### Practical implications

- Do not add duplicate singleton-like scene objects casually; many systems resolve dependencies via `FindFirstObjectByType` or `FindAnyObjectByType`.
- A scene can appear to work even when serialized references are incomplete, because fallback auto-creation masks the problem.
- When debugging startup issues, check both scene wiring and runtime bootstrap order.

## Implementation Status

### Stable or mostly implemented

- Core quest data, generation, lifecycle, and zone logic
- Cargo weight and cargo visuals
- Reward and progression flow
- Save/load foundations
- Audio and particle feedback foundations
- Balance HUD integration
- Speed unit configuration across HUD and settings

### Still worth validating during changes

- Traffic edge-case recovery
- Vehicle physics tuning
- Minimap and route feedback
- Tutorial or onboarding flow
- Full optimization, debugging tools, and balancing pass

### Known risks

- Road graph extraction can be unreliable in more complex road layouts.
- NPC recovery can still loop in rare cases.
- UI can need extra validation on non-16:9 layouts.
- Performance can degrade with high NPC counts on weaker hardware.

## High-Level Architecture

### Main runtime systems

- `CarController`: player vehicle physics and input response
- `CameraFollow`: player camera behavior
- `DeliveryManager`: phone-driven mission offer flow, pickup and delivery point spawning, objective wiring
- `QuestManager`: central quest lifecycle and reward coordinator
- `PlayerProgressionManager`: money, XP, levels, achievements
- `DriverProgressionSystem`: skill tree, level rewards, performance XP modifiers
- `SaveManager`: persistence entry point
- `RoadGraphBuilder`: road network extraction for traffic and routing
- `NpcCarAgent`, `NpcSpawner`, `NpcRecovery`: traffic simulation stack
- `QuestUIManager` and related UI classes: HUD, quest list, popups, settings-adjacent flow
- `GlobalUiCoordinator`: cross-scene UI root and canvas adoption layer

### Main design patterns

- Singleton managers for globally coordinated systems
- Observer/event-style hooks for UI and progression reactions
- ScriptableObject-backed data sources such as quest and cargo databases
- Component-based Unity behaviours for gameplay actors
- Object pooling where repeated effect spawning matters

## System Boundaries

### Delivery flow versus quest flow

There are two tightly related but distinct gameplay layers:

- `DeliveryManager` owns mission offer UX, pickup and delivery spawn points, phone acceptance flow, and some objective marker behavior.
- `QuestManager` owns quest state, rewards, penalties, save data, progression hooks, audio feedback, and quest events.

Important runtime behavior:

- `DeliveryManager.Start()` forces `requirePhoneMissionAccept = true`.
- `DeliveryManager.Start()` also forces `useQuestSystem = true`.
- `QuestManager` is the authoritative source for quest lifecycle events consumed by UI, progression, tutorial, and SQLite sync.

If a change affects mission acceptance, pickup state, delivery targets, or objective visibility, inspect both systems together instead of only one.

### Route and minimap stack

Navigation follows a GPS-style architecture with a central service:

- `NavigationService` (singleton): owns route and objective state, single consumer of `RoadGraphPathfinder`
- `ObjectiveMarker3D`: subscribes to NavigationService, renders spinning 3D cylinder marker
- `EdgeIndicator`: subscribes to NavigationService, shows screen-edge directional arrow
- `WorldRouteRenderer`: subscribes to NavigationService, renders world-space route LineRenderer
- `MinimapUI`: subscribes to NavigationService for minimap markers and route preview overlay
- `CompassUI`: subscribes to NavigationService for compass needle direction
- `MinimapCamera`: minimap camera follow behavior
- `RoadGraphPathfinder`: path search utility (only called by NavigationService)
- `RoadGraphBuilder`: source of road graph data

DeliveryManager calls `NavigationService.Instance.SetObjective()` / `ClearObjective()` to drive navigation.

### UI ownership

- `GlobalUiCoordinator` owns a persistent global canvas and reparents many scene canvases under it.
- Canvases already using `ScaleWithScreenSize` are intentionally skipped to preserve their own scaler behavior.
- `BalanceHudUI` is injected onto the global UI root and rebuilds its own panel if needed.
- `PhoneMissionUI` can create its own canvas at runtime if no scene canvas is supplied.

Many UI bugs here are canvas layering or duplicate-runtime-object issues, not pure widget logic bugs.

## Code Organization

### Important directories

- `Assets/Scripts/`
- `Assets/Scripts/Quest/`
- `Assets/Scripts/Quest/UI/`
- `Assets/Scripts/Quest/SaveSystem/`
- `Assets/Scripts/Neighborhood/`
- `Assets/Scripts/UI/`
- `Assets/Scripts/Performance/`
- `Assets/Prefabs/`
- `Assets/Resources/`
- `Assets/StreamingAssets/Database/`
- `Assets/Scenes/`

### Files commonly touched

- `Assets/Scripts/CarController.cs`: vehicle handling
- `Assets/Scripts/CameraFollow.cs`: camera behavior
- `Assets/Scripts/DeliveryManager.cs`: mission spawning and phone offer flow
- `Assets/Scripts/Navigation/NavigationService.cs`: central GPS-style route and objective service
- `Assets/Scripts/Navigation/ObjectiveMarker3D.cs`: world 3D objective marker
- `Assets/Scripts/Navigation/EdgeIndicator.cs`: screen-edge directional arrow
- `Assets/Scripts/Navigation/WorldRouteRenderer.cs`: world-space route line
- `Assets/Scripts/RoadGraphBuilder.cs`: road extraction and waypoint generation
- `Assets/Scripts/RoadGraphPathfinder.cs`: road graph path search used by minimap route preview
- `Assets/Scripts/NpcCarAgent.cs`: NPC driving logic
- `Assets/Scripts/NpcRecovery.cs`: recovery rules
- `Assets/Scripts/Quest/QuestManager.cs`: quest generation and lifecycle
- `Assets/Scripts/Quest/QuestEnums.cs`: quest types, status, difficulty
- `Assets/Scripts/Quest/PlayerProgressionManager.cs`: XP, level, achievements
- `Assets/Scripts/Quest/DriverProgressionSystem.cs`: skill tree and reward unlocks
- `Assets/Scripts/Quest/GameSettings.cs`: PlayerPrefs-backed settings
- `Assets/Scripts/Quest/QuestDatabaseBootstrap.cs`: SQLite schema and seed initialization
- `Assets/Scripts/Quest/QuestDatabaseService.cs`: SQLite access layer
- `Assets/Scripts/Quest/SaveSystem/SaveManager.cs`: save/load orchestration
- `Assets/Scripts/Quest/UI/`: in-game quest, HUD, minimap, popup, and settings-adjacent UI logic
- `Assets/Scripts/UI/GlobalUiCoordinator.cs`: global canvas and persistent UI integration
- `Assets/Scripts/UI/MainMenuRuntimeUI.cs`: main menu UI is mostly built in code

### Inspector references that usually matter

#### `QuestManager`

- `questDatabase`
- `cargoLibrary`
- `roadGraphBuilder`
- `playerTransform`
- `playerController`
- pickup and delivery marker prefabs
- quest zone prefab
- quest audio clips
- quest particle prefabs

#### `RoadGraphBuilder`

- `autoDetectRoads`
- `sampleStepMeters`
- `connectionThresholdMeters`
- `includeSimplePolyRoads`
- `generateDualLaneSegmentsForSimplePoly`
- `buildOnStart`
- `startupBuildDelay`

#### `PlayerProgressionManager`

- starting money
- starting level

#### `DeliveryManager`

- `roadGraphBuilder`
- `cargoLibrary`
- `phoneMissionUI`
- `useRoadGraphSpawnPoints`
- `spawnOnlyInNeighborhoods`
- `miniMapMarker`
- `speedometerUI`

#### `MinimapUI`

- `minimapCamera`
- render texture and camera bindings
- pickup, delivery, and player marker prefabs
- `showRoutePreview`
- zoom limits and marker container

## Namespace Map

- Global namespace: driving, mission, and some UI bootstrap classes such as `CarController`, `DeliveryManager`, `PhoneMissionUI`, `GlobalUiCoordinator`
- `DeliveryDriver.Quest`: quest state, progression, save, database, tutorial
- `DeliveryDriver.Quest.UI`: quest HUD, minimap, pause, settings, statistics
- `DeliveryDriver.UI`: general-purpose runtime UI framework and menu systems
- `TrafficSystem`: road graph, NPC traffic, weather, signals, route helpers
- `DeliveryDriver.City`: neighborhoods and city-zone metadata
- `DeliveryDriver.Optimization`: chunking, HLOD, performance controllers

When moving or duplicating code, preserve namespace boundaries or update imports carefully. This repository mixes namespaced and global classes.

## Coding Conventions

- Use PascalCase for classes, methods, enums, and event names.
- Use camelCase for fields and locals.
- Keep one main class per file unless a small nested helper is justified.
- Match file names to class names exactly.
- Group related systems into folders instead of large mixed directories.
- Cache component references in `Awake` or `Start` when reused often.
- Prefer low-allocation patterns in gameplay loops.
- Use `sqrMagnitude` for frequent distance checks when exact distance is unnecessary.
- Keep public APIs and serialized fields explicit and readable.

### Unity lifecycle order

Prefer a consistent class layout:

1. Unity messages: `Awake`, `Start`, `Update`, `FixedUpdate`, `LateUpdate`, `OnDestroy`
2. Public methods
3. Private methods
4. Coroutines
5. Event handlers

## Feature Work Guidelines

### Adding a quest type

1. Add the enum entry in `QuestEnums.cs`.
2. Add generation logic in `QuestManager.cs`.
3. Hook it into weighted quest selection.
4. Update quest UI icon mapping if the type needs a dedicated visual.

### Adding cargo

1. Add the data to the cargo library asset or supporting code.
2. Add or map visuals if needed.
3. Add special-case logic only if the cargo truly behaves differently.

### Adding achievements

1. Define the achievement data.
2. Add unlock conditions in progression checks.
3. Ensure quest completion or other relevant systems trigger those checks.

### Changing save, settings, or progression

Inspect all relevant persistence layers before editing:

- `SaveManager` and `SaveData`: JSON save file at `Application.persistentDataPath/savegame.json`
- `GameSettings`: `PlayerPrefs` for audio, graphics, language, accessibility, speed unit, minimap zoom
- `QuestDatabaseBootstrap` and `QuestDatabaseService`: SQLite database at `Application.persistentDataPath/quest.db`
- `QuestDatabaseAutoSync`: quest lifecycle mirroring into SQLite

Changing progression or quest data often requires checking both JSON save compatibility and SQLite sync behavior.

### Changing road, routing, or traffic logic

Validate all of these together:

1. `RoadGraphBuilder` can still build a graph
2. `QuestManager` startup quest generation still waits and succeeds
3. `DeliveryManager` can still derive valid spawn points
4. `NavigationService` route calculation still works
5. `MinimapUI` route preview, `WorldRouteRenderer`, `ObjectiveMarker3D`, and `EdgeIndicator` still update
6. NPC traffic still follows usable routes

## Performance Guidance

- Pool frequently spawned effects and other short-lived objects.
- Avoid per-frame allocations in gameplay code.
- Avoid repeated `GetComponent` calls in update loops.
- Run expensive checks on intervals when frame-perfect updates are unnecessary.
- Validate traffic-heavy scenes with realistic NPC counts before considering work done.
- Route preview code should be treated as potentially expensive; cache and threshold-based recompute behavior already exists and should usually be preserved.

## Git and Commit Workflow

### Branching and commit expectations

- Use logical commit grouping by scope: gameplay, settings/UI, scene/assets.
- Use commit messages that describe the user-visible impact.
- Treat `main` as stable and production-oriented.
- Use `feature/[name]` for feature work and `bugfix/[name]` for targeted fixes.

### Before pushing

- Verify `git status -sb` is clean.
- Verify the current branch and intended upstream target.
- Run `git lfs status` when scene or asset files changed.

### If the user asks for "commit/push all"

- Prefer multiple logical commits over one large squash commit.

### If push rejects with `cannot lock ref ... expected ...`

Check these first before any force operation:

- `git rev-parse HEAD`
- `git rev-parse origin/<branch>`

Re-evaluate from there.

## Git LFS Policy

- Keep LFS enabled unless the project owner explicitly requests otherwise.
- Current tracked file of note: `Assets/Scenes/Game.unity`
- Observed LFS usage in history was low at the time of the snapshot, so no immediate migration is required.

### LFS safety rules

- Do not remove `Game.unity` from LFS unless explicitly requested.
- Push `Game.unity` only when there is a real scene, gameplay, or UI layout change.
- Avoid committing editor-only or noise-only scene changes.
- Re-check LFS usage periodically, especially before larger releases.
- If LFS usage approaches a risk threshold, discuss a controlled migration before acting.

## Testing Checklist

Before major commits, verify what is relevant to the change:

- No compile errors
- No unexpected console errors during play
- Main menu still opens and starts the game scene
- First boot still works with no existing save file or PlayerPrefs assumptions
- Quest generation still works
- Phone mission offer appears and can be accepted or rejected
- Quest acceptance and completion still work
- Pickup and delivery triggers still work
- Rewards and progression still update correctly
- Save/load still preserves required state
- Settings changes persist across restart when relevant
- Minimap route preview and objective markers still behave correctly
- UI reflects the changed gameplay state correctly
- NPC vehicles do not introduce obvious regressions
- Performance remains acceptable for the affected scene
- Temporary debug logs are removed unless intentionally kept

## Agent Operating Notes

- Prefer the latest dated snapshot in this file over older assumptions.
- Treat scene and asset changes carefully; they are higher risk than script-only edits.
- When changing UI, validate interaction flow as well as visuals.
- When changing traffic or road systems, test both nominal driving and recovery paths.
- When working near save, progression, or quest lifecycle code, think through migration and backward compatibility risks.
- Many systems use runtime fallback discovery. If behavior is inconsistent between clean boot and scene reload, suspect duplicate persistent objects first.
- Because `DontDestroyOnLoad` usage is widespread, always think about second-load behavior, duplicate listeners, and stale singleton state.
- The main menu is code-built, not just scene-authored. UI changes may require editing runtime menu builders rather than scene hierarchy.
- The project has both JSON save data and SQLite quest history. Do not assume one replaces the other.

## Quick Decision Rules

- If a change affects player-facing balance, inspect `QuestManager` and progression logic together.
- If a change affects route guidance or NPC movement, inspect road graph, pathfinder, NPC agent, and minimap or marker UI together.
- If a change affects HUD behavior, inspect both the local widget and the global UI coordinator lifecycle.
- If a change touches scene layout, consider LFS, scene noise, and inspector wiring before committing.
- If a change affects mission offers, objective visibility, or delivery state, inspect `DeliveryManager`, `QuestManager`, and the relevant UI listener together.
- If a change affects settings, inspect `GameSettings`, the relevant UI, and any runtime subscribers reacting to setting-change events.
