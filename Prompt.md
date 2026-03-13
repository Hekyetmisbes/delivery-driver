# Delivery Driver Full Script Refactor / Optimization Prompt

## Progress Log

- [x] Step 1 - Audited the prompt, architecture notes, and the current hotspot files. Focus selected for this pass: repeated player/service lookups, per-frame rebinding, and duplicate UI canvas creation in the navigation/minimap/player-vehicle path.
- [x] Step 2 - Implemented the first optimization pass in `PlayerVehicleManager`, `NavigationService`, `MinimapCamera`, `CompassUI`, and `EdgeIndicator`. Added an authoritative active-vehicle event, replaced repeated rebinding lookups with cached references, throttled fallback resolution, and moved the legacy edge indicator away from always creating its own overlay canvas when the global UI canvas is available.
- [x] Step 3 - Optimized `MinimapUI` so it no longer tries to create `NavigationService` from the UI layer, now listens for authoritative active-vehicle changes, and throttles player, navigation-service, and road-graph fallback discovery instead of repeating those searches every frame.
- [x] Step 4 - Verified the edited C# files with `dotnet build Assembly-CSharp.csproj -nologo`. Result: build succeeded with 0 errors and 5 pre-existing warnings outside the edited files (`LocalizationTable.cs` and `NpcCarAgent.cs`).
- [x] Step 5 - Reduced `RoadGraphPathfinder` allocation churn by reusing candidate-search and A* working buffers instead of creating fresh list/dictionary/hashset/queue state on each path search.
- [x] Step 6 - Reworked `NotificationQueue` and `TooltipUI` so they reuse the authoritative global UI hierarchy when available instead of always creating separate persistent overlay canvases.
- [x] Step 7 - Removed dead `SettingsScene` and `CreditsScene` bootstrap branches from `MenuSceneBootstrap`, keeping runtime menu injection aligned with the actual build settings (`MainMenu` and `Game` only).
- [x] Step 8 - Re-verified the expanded change set with `dotnet build Assembly-CSharp.csproj -nologo`. Result: build succeeded again with 0 errors and the same 5 pre-existing warnings in `LocalizationTable.cs` and `NpcCarAgent.cs`.
- [x] Step 9 - Replaced the company installer's editor-only prefab dependency with a runtime-loadable `Resources/Company/VehiclePrefabCatalog` asset and kept editor asset-path loading only as a fallback for development workflows.
- [x] Step 10 - Isolated `MemoryProfiler`, `PerformanceRegressionDetector`, and `PerformanceBenchmark` behind editor/debug-build runtime gates so profiling and regression tooling no longer stays active in normal gameplay builds.
- [x] Step 11 - Re-verified the newest company/diagnostics changes with `dotnet build Assembly-CSharp.csproj -nologo`. Result: build succeeded again with 0 errors and the same 5 pre-existing warnings in `LocalizationTable.cs` and `NpcCarAgent.cs`.
- [x] Step 12 - Split `RoadGraphBuilder` responsibilities by extracting graph connection construction into `RoadGraphConnectionBuilder` and mesh/centerline waypoint sampling into `RoadGraphMeshSampler`, leaving `RoadGraphBuilder` as the orchestration layer for graph assembly.
- [x] Step 13 - Re-verified the `RoadGraphBuilder` refactor with `dotnet build Assembly-CSharp.csproj -nologo`. Result: build succeeded again with 0 errors and the same 5 pre-existing warnings in `LocalizationTable.cs` and `NpcCarAgent.cs`.

Rewrite, optimize, and reorganize all C# scripts under `Assets/Scripts/` in this Unity project. The goal is not to patch individual issues. The goal is to rebuild the scripting architecture so it is faster, cleaner, easier to maintain, easier to extend, and significantly more readable.

## Project context

- Project type: Unity delivery / driving game
- Unity version: `6000.3.9f1`
- Build scenes:
  - `Assets/Scenes/MainMenu.unity`
  - `Assets/Scenes/Game.unity`
- The codebase mixes runtime bootstrap systems with scene-authored objects
- Navigation, quest, delivery, UI, company/vehicle, NPC traffic, save/load, and performance systems are tightly coupled

## Current audited state

Base refactor decisions on these actual findings:

- Total script count: `126`
- Files using `Update()`: `41`
- Files using `LateUpdate()`: `6`
- Files using `FixedUpdate()`: `2`
- Files without namespaces: `24`
- `FindFirstObjectByType` usages: `56`
- `FindAnyObjectByType` usages: `27`
- `GameObject.Find*` usages: `24`
- `Resources.Load` usages: `8`
- `GetComponent<...>` usages: `475`
- Only one asmdef currently exists under `Assets/Scripts`, and its `rootNamespace` is empty
- Multiple persistent UI/service roots currently coexist through `GlobalUiCoordinator`, `NotificationQueue`, `TooltipUI`, `UIAudioFeedback`, and `SceneTransitionManager`
- Some bootstrap code still references `SettingsScene` and `CreditsScene` even though only `MainMenu` and `Game` are enabled in build settings
- Profiling and regression tooling is mixed into runtime gameplay code instead of being isolated behind development-only boundaries
- Files over `1000` lines:
  - `Assets/Scripts/NpcCarAgent.cs` `2917` lines
  - `Assets/Scripts/Quest/QuestManager.cs` `2774` lines
  - `Assets/Scripts/DeliveryManager.cs` `2229` lines
  - `Assets/Scripts/Quest/UI/MinimapUI.cs` `1797` lines
  - `Assets/Scripts/RoadGraphBuilder.cs` `1385` lines
  - `Assets/Scripts/CameraFollow.cs` `1207` lines
  - `Assets/Scripts/Quest/UI/PauseMenuUI.cs` `1114` lines

These findings indicate monolithic classes, heavy runtime lookups, too much per-frame work, scattered bootstrap logic, and inconsistent folder and namespace discipline.

## Primary goals

Achieve all of the following together:

1. Reorganize all scripts by clear domain ownership.
2. Break large multi-purpose classes into smaller single-responsibility classes.
3. Remove unnecessary runtime `Find*`, `GameObject.Find*`, `Resources.Load`, `AddComponent`, and `new GameObject` usage.
4. Reduce per-frame work by moving to event-driven, cached, throttled, or service-based flows.
5. Standardize folders and namespaces.
6. Make the code easier to read, debug, modify, and review.
7. Preserve gameplay behavior while improving structure and performance.

## Non-negotiable behavior to preserve

Do not break these behaviors:

- The phone-based mission acceptance flow in `DeliveryManager` must remain intact
- The enforced runtime behavior `requirePhoneMissionAccept = true` and `useQuestSystem = true` must remain intact unless intentionally redesigned with full compatibility
- `NavigationService` must remain the authoritative navigation state source
- `MinimapUI`, `CompassUI`, `EdgeIndicator`, `ObjectiveMarker3D`, and `WorldRouteRenderer` must remain synchronized with navigation state
- `GlobalUiCoordinator` must continue to own the persistent global canvas flow
- The company page flow in the `Game` scene must still work
- Vehicle selection, player vehicle application, and camera/player rebinding must still work
- `SaveManager`, `GameSettings`, `QuestDatabaseService`, and `QuestDatabaseAutoSync` compatibility must be preserved
- JSON save and SQLite-backed systems must continue to coexist correctly

## Major refactor problems to solve

### 1. Monolithic classes

The following classes currently carry too many responsibilities and must be split:

- `Assets/Scripts/DeliveryManager.cs`
  - mission offer flow
  - spawn point generation
  - road-graph location selection
  - neighborhood logic
  - UI binding
  - quest bridging
  - player binding
  - phone mission UI setup and callbacks
- `Assets/Scripts/Quest/QuestManager.cs`
  - quest lifecycle
  - reward and penalty calculation
  - marker management
  - save/load integration
  - progression integration
  - reflection fallback logic
  - audio and effects
- `Assets/Scripts/Quest/UI/MinimapUI.cs`
  - runtime UI construction
  - navigation subscription
  - player binding
  - road overlay rendering
  - route projection
  - marker pooling and creation
  - camera resolving
- `Assets/Scripts/RoadGraphBuilder.cs`
  - road discovery
  - EasyRoads reflection
  - mesh extraction
  - waypoint generation
  - graph connection building
  - debug and fallback test-road generation
- `Assets/Scripts/NpcCarAgent.cs`
  - path following
  - obstacle avoidance
  - speed control
  - reverse recovery
  - lane logic
  - weather logic
  - wheel visuals
  - physics tuning
- `Assets/Scripts/CameraFollow.cs`
  - player camera
  - minimap camera
  - reverse camera
  - minimap bounds and surface creation
  - road graph lookup
- `Assets/Scripts/Quest/UI/PauseMenuUI.cs`
  - full pause/settings runtime UI generation
  - event system and canvas creation
  - control logic and view building in one class

Split these into smaller modules with explicit responsibilities.

### 2. Per-frame lookup and dependency resolution

Clean up patterns like these:

- `NavigationService` uses a fallback chain to resolve the player
  - `Assets/Scripts/Navigation/NavigationService.cs:504`
  - `Assets/Scripts/Navigation/NavigationService.cs:578`
  - `Assets/Scripts/Navigation/NavigationService.cs:588`
- `MinimapCamera` resolves the player from `LateUpdate()`
  - `Assets/Scripts/Quest/UI/MinimapCamera.cs:55`
  - `Assets/Scripts/Quest/UI/MinimapCamera.cs:212`
- `CompassUI` tries to bind the navigation service every frame
  - `Assets/Scripts/Quest/UI/CompassUI.cs:53`
  - `Assets/Scripts/Quest/UI/CompassUI.cs:78`
- `EdgeIndicator` looks up the minimap camera at runtime and creates its own overlay canvas
  - `Assets/Scripts/Navigation/EdgeIndicator.cs:61`
  - `Assets/Scripts/Navigation/EdgeIndicator.cs:190`
  - `Assets/Scripts/Navigation/EdgeIndicator.cs:245`
- `PlayerVehicleManager` performs repeated scene lookups to rebind other systems
  - `Assets/Scripts/Company/PlayerVehicleManager.cs:298`
  - `Assets/Scripts/Company/PlayerVehicleManager.cs:372`
- `DeliveryManager`, `QuestManager`, `MinimapUI`, `QuestUIManager`, `CameraFollow`, and other systems all resolve player/service references through different fallback chains

Target architecture:

- Create one authoritative player context or gameplay context
- When the active player vehicle changes, all dependent systems should update through one centralized event or context update
- UI and services should rely on injection, installers, serialized references, registries, or well-defined bootstrap flows instead of ad hoc runtime searches

### 3. Navigation and minimap architecture is too scattered

Keep navigation authoritative, but simplify the consumers.

Important problem points:

- `Assets/Scripts/Navigation/NavigationService.cs:136`
  - route refresh, off-route detection, route publishing, and reroute handling are concentrated in one `Update()`
- `Assets/Scripts/Quest/UI/MinimapUI.cs:314`
  - UI code should not create `NavigationService` via `EnsureInstance()`
- `Assets/Scripts/Quest/UI/MinimapUI.cs:1615`
  - road graph lookup and readiness waiting logic should not live inside the minimap UI class
- `Assets/Scripts/RoadGraphPathfinder.cs:71`
  - reduce list/dictionary allocation pressure per path search
- `Assets/Scripts/RoadGraphBuilder.cs:95`
  - split discovery, extraction, validation, fallback, and debug responsibilities

Use a navigation architecture like this:

- `Navigation/Core`
  - navigation state
  - route model
  - objective model
  - navigation events
- `Navigation/Services`
  - route planner
  - player route tracker
  - reroute policy
  - graph availability resolver
- `Navigation/Graph`
  - graph builder
  - graph validator
  - graph extractor adapters
  - graph diagnostics
  - pathfinder
- `Navigation/Presentation`
  - minimap route presenter
  - world route presenter
  - compass presenter
  - objective marker presenter
  - edge indicator presenter

### 4. Runtime UI builders are oversized

These classes build large runtime UI trees in code and need to be decomposed:

- `Assets/Scripts/Quest/UI/MinimapUI.cs`
- `Assets/Scripts/Quest/UI/PauseMenuUI.cs`
- `Assets/Scripts/UI/MainMenuRuntimeUI.cs`
- `Assets/Scripts/Company/CompanyPageUI.cs`
- `Assets/Scripts/PhoneMissionUI.cs`
- `Assets/Scripts/SpeedometerUI.cs`

Separate these concerns:

- view construction
- theme and styling
- data binding
- input handling
- screen state management
- service interaction

Preferred UI structure:

- `UI/Common`
  - shared factory, helpers, layout utilities
  - reusable button, text, panel builders
- `UI/Themes`
  - colors, spacing, typography, sprites, reusable styles
- `UI/Screens`
  - `MainMenu`
  - `PauseMenu`
  - `CompanyPage`
  - `PhoneMission`
  - `HUD`
- `UI/Presenters`
  - presenter and view-model coordination
- `UI/RuntimeBootstrap`
  - runtime instantiation only where truly necessary

UI classes should not freely spawn extra `Canvas`, `EventSystem`, or `GraphicRaycaster` instances. Reuse `GlobalUiCoordinator` and establish one consistent UI composition flow.

### 5. Delivery and quest responsibilities overlap

Clarify the contract between `DeliveryManager` and `QuestManager`:

- `DeliveryManager`
  - delivery-specific orchestration
  - pickup/dropoff world objects
  - phone mission offer flow
- `QuestManager`
  - authoritative quest state
  - rewards
  - penalties
  - progression hooks
  - save-facing state

Do not allow the same state to be duplicated or partially owned by both systems after the refactor.

### 6. Company and vehicle flow is lookup-heavy and editor-dependent

Specific problems:

- `Assets/Scripts/Company/PlayerVehicleManager.cs:298`
  - repeated scene lookups while rebinding systems
- `Assets/Scripts/Company/GameSceneCompanyPageInstaller.cs:94`
  - `AssetDatabase` usage is risky for player builds
- `Assets/Scripts/Company/CompanyPageUI.cs:349`
  - UI directly looks up managers
- `Assets/Scripts/Company/CompanyPageUI.cs:424`
  - event system and canvas fallback logic lives inside the UI screen

Target design:

- Move vehicle selection and application to an application service or dedicated coordinator
- Centralize spawn/apply/bind logic for the active player vehicle
- Trigger camera, delivery, navigation, minimap, and UI rebinding through a vehicle-switched event flow
- Replace editor-only prefab loading with a runtime-safe asset resolution approach

### 7. Reflection and fallback logic is too common

Examples:

- `Assets/Scripts/Quest/QuestManager.cs:2136`
- `Assets/Scripts/Quest/QuestManager.cs:2182`
- `Assets/Scripts/RoadGraphBuilder.cs`

Reflection should live only in isolated compatibility or adapter layers when absolutely necessary. Core gameplay logic should not depend on reflection or string-based method discovery.

### 8. Allocation pressure and collection churn must be reduced

Reduce unnecessary allocations and GC pressure in:

- `RoadGraphPathfinder`
- `MinimapRoadTextureBuilder`
- `MinimapUI`
- `NavigationService`
- runtime UI builders
- NPC and traffic per-frame logic

Use appropriate techniques where needed:

- object pooling
- reusable buffers
- preallocated collections
- cached references
- dirty-flag updates
- batched refreshes
- frame-budget throttling

### 9. Persistent UI roots and bootstrap ownership are fragmented

Fix the ownership overlap between:

- `Assets/Scripts/UI/GlobalUiCoordinator.cs`
- `Assets/Scripts/UI/NotificationQueue.cs`
- `Assets/Scripts/UI/TooltipUI.cs`
- `Assets/Scripts/UI/UIAudioFeedback.cs`
- `Assets/Scripts/UI/SceneTransitionManager.cs`
- `Assets/Scripts/UI/MenuSceneBootstrap.cs`
- `Assets/Scripts/Quest/UI/PauseMenuUI.cs`
- `Assets/Scripts/UI/MainMenuRuntimeUI.cs`

Target state:

- one authoritative persistent UI/app root
- one event-system policy
- one canvas ownership policy
- no duplicate `DontDestroyOnLoad` UI trees
- no hidden scene-name bootstrap paths for scenes that are not in build settings

### 10. Optimization ownership is currently overlapping

Unify the responsibilities currently spread across:

- `Assets/Scripts/RuntimeOptimizationBootstrap.cs`
- `Assets/Scripts/PerformanceOptimizationManager.cs`
- `Assets/Scripts/UnifiedOptimizationController.cs`
- `Assets/Scripts/TrafficSimulationOptimizer.cs`
- `Assets/Scripts/WorldChunkManager.cs`
- `Assets/Scripts/HLODGroup.cs`
- `Assets/Scripts/HLODProxy.cs`
- `Assets/Scripts/Performance/MemoryProfiler.cs`
- `Assets/Scripts/Performance/PerformanceRegressionDetector.cs`

Target state:

- one authoritative optimization orchestrator
- one visibility/streaming scheduling policy
- one NPC throttling policy
- profiling and benchmark code isolated behind development-only compile flags, debug assemblies, or explicit tooling gates

## Target folder structure

Reorganize `Assets/Scripts/` into a structure like this:

```text
Assets/Scripts/
  Core/
    Bootstrap/
    Infrastructure/
    Events/
    Extensions/
    Utilities/
    Configuration/

  Gameplay/
    Delivery/
      Runtime/
      Spawning/
      MissionFlow/
      WorldObjects/
      Integration/
    Vehicle/
      Runtime/
      Camera/
      Input/
      HUD/
    Traffic/
      Agents/
      Behaviors/
      Spawning/
      Recovery/
      Optimization/
    Navigation/
      Core/
      Graph/
      Routing/
      Presentation/
    World/
      Neighborhoods/
      Streaming/
      Weather/

  Progression/
    Quests/
    Player/
    Rewards/
    Achievements/
    Tutorials/

  Persistence/
    SaveSystem/
    Settings/
    Database/
    Sync/

  UI/
    Core/
    Themes/
    Shared/
    HUD/
    Screens/
      MainMenu/
      PauseMenu/
      Company/
      PhoneMission/
      Quest/
    Navigation/

  Company/
    Core/
    Vehicles/
    UI/
    Installers/

  Performance/
    Runtime/
    Diagnostics/
    Profiling/

  Editor/
```

## Namespace rules

Standardize all namespaces to match folder ownership:

- `DeliveryDriver.Core.*`
- `DeliveryDriver.Gameplay.Delivery.*`
- `DeliveryDriver.Gameplay.Vehicle.*`
- `DeliveryDriver.Gameplay.Traffic.*`
- `DeliveryDriver.Gameplay.Navigation.*`
- `DeliveryDriver.Gameplay.World.*`
- `DeliveryDriver.Progression.*`
- `DeliveryDriver.Persistence.*`
- `DeliveryDriver.UI.*`
- `DeliveryDriver.Company.*`
- `DeliveryDriver.Performance.*`
- `DeliveryDriver.Editor.*`

Remove global-namespace scripts. File placement and namespace should match.

## Technical implementation rules

Follow these rules during the rewrite:

1. No class should do everything.
2. Every class must have a narrow, explicit purpose.
3. Keep `Update()` only where per-frame behavior is truly required.
4. Move `FindFirstObjectByType`, `FindAnyObjectByType`, and `GameObject.Find*` usage to bootstrap time or remove it entirely.
5. Move `Resources.Load` into a centralized asset/config resolution layer.
6. Limit runtime `new GameObject` and `AddComponent` usage to bootstrap or factory layers.
7. Separate UI construction from presenter/state logic.
8. Move large serialized tuning blocks into ScriptableObject-based configs where appropriate.
9. Define ownership clearly for each subsystem.
10. Publish player and navigation state from one authoritative context.
11. Make event subscription and cleanup explicit and safe.
12. Eliminate duplicate singleton and duplicate persistent UI risks.
13. Separate editor-only code from runtime/build-safe code.
14. Push reflection-based compatibility into isolated adapter layers.
15. Separate debug/test fallback logic from production behavior.
16. Normalize asmdef ownership; do not keep the entire project under a single generic asmdef if clearer module assembly boundaries can be introduced safely.
17. Set non-empty root namespaces for assemblies and keep them aligned with folder/module ownership.
18. Remove reflection-based field wiring helpers such as `QuestUISetup` in favor of explicit serialized references, installers, prefabs, or editor tooling.

## Files to prioritize first

Start with these files:

- `Assets/Scripts/DeliveryManager.cs`
- `Assets/Scripts/Quest/QuestManager.cs`
- `Assets/Scripts/Quest/UI/MinimapUI.cs`
- `Assets/Scripts/RoadGraphBuilder.cs`
- `Assets/Scripts/RoadGraphPathfinder.cs`
- `Assets/Scripts/NpcCarAgent.cs`
- `Assets/Scripts/CameraFollow.cs`
- `Assets/Scripts/Quest/UI/PauseMenuUI.cs`
- `Assets/Scripts/Company/PlayerVehicleManager.cs`
- `Assets/Scripts/Company/CompanyPageUI.cs`
- `Assets/Scripts/Navigation/NavigationService.cs`
- `Assets/Scripts/Quest/UI/MinimapCamera.cs`
- `Assets/Scripts/Quest/UI/CompassUI.cs`
- `Assets/Scripts/Navigation/EdgeIndicator.cs`
- `Assets/Scripts/UI/MainMenuRuntimeUI.cs`
- `Assets/Scripts/UI/GlobalUiCoordinator.cs`
- `Assets/Scripts/UI/MenuSceneBootstrap.cs`
- `Assets/Scripts/UI/NotificationQueue.cs`
- `Assets/Scripts/UI/TooltipUI.cs`
- `Assets/Scripts/UI/UIAudioFeedback.cs`
- `Assets/Scripts/Quest/UI/QuestUISetup.cs`
- `Assets/Scripts/RuntimeOptimizationBootstrap.cs`
- `Assets/Scripts/PerformanceOptimizationManager.cs`
- `Assets/Scripts/UnifiedOptimizationController.cs`
- `Assets/Scripts/TrafficSimulationOptimizer.cs`
- `Assets/Scripts/WorldChunkManager.cs`
- `Assets/Scripts/Performance/MemoryProfiler.cs`
- `Assets/Scripts/Performance/PerformanceRegressionDetector.cs`

## Expected output

The final rewrite should provide:

1. A reorganized script folder structure
2. A file move and namespace migration plan
3. A breakdown of which large classes were split and into which new classes
4. A record of which runtime lookup patterns were removed
5. A description of the new event/context/service architecture
6. A summary of the performance improvements that were implemented
7. A summary of which old gameplay behaviors were preserved
8. A note of any remaining risks
9. A summary of assembly-definition and namespace changes

## Quality bar

When the rewrite is done, the project should:

- perform fewer scene lookups
- allocate less memory during gameplay
- do less unnecessary per-frame work
- create fewer duplicate UI or service objects
- have clearer folders and namespaces
- contain smaller and more readable classes
- preserve gameplay behavior
- improve runtime and build safety

## Validation checklist

After the rewrite, validate all of the following:

- The project compiles
- No new console errors or warnings appear
- `MainMenu` still loads
- The flow still reaches `Game`
- No duplicate persistent managers or duplicate canvases appear after scene transitions
- The phone mission offer flow still works
- Accepting a mission still starts the pickup/delivery flow
- `NavigationService` still publishes objective and route state
- `MinimapUI`, `CompassUI`, `EdgeIndicator`, `ObjectiveMarker3D`, and `WorldRouteRenderer` remain synchronized
- The company page still works in the `Game` scene
- Vehicle selection still applies correctly without breaking camera/player bindings
- `SaveManager`, `GameSettings`, `QuestDatabaseService`, and `QuestDatabaseAutoSync` still work together
- JSON save and SQLite init/sync behavior still work correctly
- Development-only profiler or regression tools no longer affect normal runtime gameplay paths unless explicitly enabled

## Final instruction

Do not do this as a shallow cleanup. Do not produce temporary hacks, cosmetic moves, or partial fixes. Perform a true architectural refactor of the scripting layer so the project ends up with optimized systems, clean ownership boundaries, consistent organization, and a codebase that another developer can confidently read and extend.
