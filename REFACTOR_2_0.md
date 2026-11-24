# Delivery Driver 2.0 — Core Refactoring & Performance Overhaul

Integration branch: `refactor/delivery-driver-2.0-core-performance`
Baseline: `7c6903699dc0a5e7f8c3915c99c859a888de0526` (main).
Unity: 6000.3.12f1; URP 17.3.0; Input System 1.19.0; Cinemachine 3.1.6.
Target: preserve the existing phone mission, delivery parking, multi-stop, economy, driving, traffic and navigation experience.

## Change policy

- Keep main stable; no merge before verification. Work in small, reviewable commits.
- Preserve serialized field names, .meta GUIDs, scene/prefab references and physics tuning unless explicitly migrating them.
- Keep packages pinned during core refactoring. Avoid LFS scene changes unless necessary.
- Record baseline and post-change measurements under identical conditions; no unmeasured performance claims.
- Existing JSON, SQLite and PlayerPrefs data must remain readable. Do not reset real player data during tests.

## Phases

| Phase | Scope | Status / exit gate |
|---|---|---|
| 0 | Baseline, automated tests, build and profiler capture | Started; Editor and build verification pending |
| 1 | Save integrity, quest completion/failure guards, safe vehicle switching | Initial implementation and vehicle rollback committed; PlayMode verification pending |
| 2 | SQLite transaction boundaries, event separation, data ownership/migration | Typed SQLite, snapshots, balance precedence and pending absolute-balance replay committed; full cross-store migration and atomic rewards pending |
| 3 | Low-allocation improvements, profiling and traffic fixed-tick scheduler | FIFO pool, ground queries, fuel binding and physics-tick scheduling committed; profiler and traffic regression pending |
| 4 | Split NpcCarAgent, QuestManager, DeliveryManager by responsibility | First independent modules extracted; motor/AI, lifecycle and spawn orchestration still pending |
| 5 | Road graph baking, navigation, world culling and camera rendering | Stable heap, cache, minimap filter, dead search cleanup and cooperative time budget committed; RTA* study, baking and GPU validation pending |
| 6 | UI presenters, assembly boundaries, package audit and CI | Planned; no scene or visual redesign |

## First implementation

- GameSaveFileStore writes a temporary file, validates it and replaces the primary with a backup when possible. Unsupported filesystem replacement fails rather than using a destructive fallback.
- SaveManager checks progression and quest initialization, recovers from backup, preserves the void SaveGame API and reports manual-save success via TrySaveGame.
- QuestManager accepts terminal transitions only for active registered quest objects. DeliveryQuestFlow delegates terminal state ownership to QuestManager.
- TrafficSimulationOptimizer uses TryGetValue for hot dictionary reads without altering scheduling.
- Save format remains version 3. No JSON/SQLite/PlayerPrefs authority migration has been performed.
- File-level validation is not a full schema or cross-store consistency check. I/O remains synchronous. Validate File.Replace on the intended player backend.
- The initial guards are in-memory protections, not durable quest-ID-based payment deduplication across crashes or reloads.

## Second implementation wave

- SQLite commands use the existing Mono.Data.Sqlite provider directly rather than per-call reflection. A quest snapshot shares one connection and transaction, including location inserts and stop updates. `last_insert_rowid()` now runs on the insertion connection. Stop rows are upserted instead of deleted/recreated, and completed stop state is retained.
- QuestDatabaseAutoSync rebinds after scene changes and throttles routine snapshots to 15 seconds. Pickup, stop count/index, new quest and terminal events bypass that cadence. Existing UI updates remain immediate. Failed snapshots remain eligible for retry on later updates.
- Wallet history skips repeat QUEST_REWARD/PENALTY inserts for the same player and quest. This does not make live currency mutations crash-atomic or repair pre-existing duplicate history.
- Vehicle switching validates the required Rigidbody/WheelColliders, preserves the previous object until binding succeeds and attempts to restore it on failure. Fuel is flushed before replacement; retired FuelSystem instances no longer overwrite the active vehicle's persisted fuel. Full rollback of arbitrary listener side effects is not guaranteed.
- CarController caches the FuelSystem reference through its existing installer and FuelSystem initialization. NpcSpawner uses a FIFO queue. NPC grounding uses a bounded reusable raycast buffer and falls back to a complete query if saturated.
- NPC scheduling now uses an agent-local physics tick and stable phase offset, rather than render-frame count. High-risk dynamic vehicles bypass distance throttling. Existing distance tiers, physics parameters and full-step throttle structure remain; a separate motor/AI cadence is still planned. This change needs 30/60/120 FPS driving validation.

## Third implementation wave

### Gameplay modules

- `DeliveryParkingValidator` owns the existing full-footprint parking check. Collider and renderer lists and four footprint corners are reused; eight bounds corners are evaluated without allocating an array. Active-child discovery, trigger/wheel exclusion, renderer fallback, parking dimensions and tolerance are preserved.
- `QuestUiUpdateTracker` owns presentation dirtiness and retains the previous second rounding, pickup/index checks and fragile cargo threshold. `QuestRouteOrdering` owns the same nearest-neighbor ordering with the previous equal-distance order and no input list mutation.
- `NpcGroundingService` owns the bounded nonalloc ground query; `NpcTimeOfDayPolicy` preserves existing local-hour speed multipliers, including rush-hour boundary values. Existing MonoBehaviour files, GUIDs, serialized fields and callback ownership are retained.

### Navigation and world

- Both A* search paths use `StableRoadPriorityQueue`, a reusable binary min-heap. Equal costs compare by insertion sequence, as in the old SortedList/Queue implementation. Heuristic, transfer penalties, iteration limits and route projection are unchanged. A heap has O(log n) insertion/removal and does not shift a sorted array of priority buckets; benchmark real graphs before claiming a speedup.
- RoadGraphBuilder reuses one scene MeshFilter scan across SimplePoly extraction/recovery within a single synchronous build. It prewarms the initial 6m navigation tier instead of the later 24m tier.
- RoadGraph retains two recent spatial indices, invalidates both and evicts the less recently used entry. This trades bounded extra memory for fewer index rebuilds; measure memory on the actual Game scene. Graph mutations still require index invalidation.
- `MinimapNpcRendererFilter` owns renderer hide/restore, reuses a component list and throttles even an empty scene scan. On scene change, disable and destruction it restores and clears its state. Existing minimap render texture size, culling mask and presentation are unchanged. This reduces CPU/GC work, not proven GPU cost; a dedicated render layer or render frequency change remains a separate task.

### Economy

- `PlayerBalanceReadPolicy` gives successfully read SQLite balance precedence, then explicitly persisted PlayerPrefs balance, then JSON. Zero and smaller balances are valid; no maximum-value reconciliation. Existing stores and schema remain unchanged.
- `TryGetPlayerBalance` distinguishes a successful zero balance from missing rows or query errors. JSON save readiness waits for startup database initialization or its existing timeout, rather than capturing the provisional balance.
- Failed SQLite writes are logged; the existing local fallback remains. A failed write followed by a successful read of stale SQLite can still lose the newer fallback balance. Durable pending-write reconciliation, migration of ambiguous legacy profiles and cross-store crash-atomic rewards are not yet implemented.
- Added characterization tests for parking, quest presentation, route ordering, traffic time policy, heap ordering (5,000 interleaved operations), two-entry cache eviction/invalidation, and balance precedence.

## Fourth implementation wave

- Persist a pending absolute-balance write marker alongside the existing PlayerPrefs balance before SQLite write. On startup/refresh, replay that balance before accepting an older DB value. Retry every five unscaled seconds if ready. Successful commit clears the marker. Explicit startup profile balance reset clears the old marker.
- SetPlayerBalance now uses one SQLite upsert instead of EnsurePlayer plus UPDATE, preserving existing player metadata when the row exists. Replay is idempotent for the absolute balance, not an atomic reward/XP/quest/history transaction. PlayerPrefs is not a transactional journal; crash recovery and external writer behavior still need integration tests. Legacy conflicts without an explicit pending marker use the existing source precedence.
- Remove 309 lines of unreferenced private synchronous pathfinding implementations before additions. The sole active incremental A* search receives a configurable default 1.5 ms cooperative per-step budget alongside the existing iteration limit. No route cost or edge rules changed.
- Add navigation diagnostics for expanded nodes, queue pops, compute/wall time and largest individual setup/step. These are per completed request/attempt, not whole-frame timing; setup and individual expansions can exceed the slice budget.
- See [RTA_STAR_EVALUATION.md](RTA_STAR_EVALUATION.md) for literature review, repository-specific suitability, and experiment gates. RTA* is not deployed; evaluate it or RTAA*/TBA* against the current planner before replacing production navigation.
- Add four balance precedence cases and a directed-graph route equivalence test for tiny vs unlimited search slices.

## Validation gates (not yet run)

Open Unity Hub with editor 6000.3.12f1 and ensure Git LFS has materialized Assets/Scenes/Game.unity. In Window > General > Test Runner, select EditMode and run DeliveryDriver.EditMode.Tests.

- [ ] Compile cleanly; no missing scripts or new console errors.
- [ ] All EditMode tests pass, including backup retention, invalid snapshot and future-version protection.
- [ ] Test clean profile, existing v3 profile, corrupt primary with backup, read-only save location and reset. Use a copy of a profile, not live save files.
- [ ] Boot MainMenu > Game; return to menu and re-enter; confirm singleton bindings.
- [ ] Quit during startup, confirm an existing save is not overwritten by incomplete state.
- [ ] Complete/fail same quest repeatedly; reward, penalty, streak and events apply once.
- [ ] Phone offer, pickup, parking, multi-stop, timed/fragile delivery, rewards and route updates remain intact.
- [ ] Save/reload active quest and check money, fuel, vehicle ownership and stats; existing cross-store discrepancies must be recorded before migration.
- [ ] Build Windows player and smoke-test SQLite/native plugin and file replacement.
- [ ] On an isolated database, verify new location IDs, multi-stop upserts, completed stops, rollback on a forced SQL failure, and repeated wallet history events.
- [ ] Observe SQLite snapshot frequency during countdown, pickup, each stop, completion/failure and Game > MainMenu > Game.
- [ ] Switch van/truck repeatedly; verify velocity, fuel, camera, HUD, route and active-quest restriction. Simulate a prefab missing a required wheel reference and verify the original vehicle remains.
- [ ] Compare traffic decision cadence across render FPS, obstacle response, intersection behavior, recovery and pooling; validate crowded grounding buffer fallback.
- [ ] Compare original and new A* route sequences and costs on representative real graphs, including ties, disconnected segments, wider transfer retries and cancelled searches.
- [ ] Measure graph warmup, index rebuild frequency and memory with the two-entry cache; compare SimplePoly recovery results.
- [ ] Test parking at rotated bays, collider/renderer fallback, triggers, vehicle switching and boundary tolerances.
- [ ] Verify minimap with zero NPCs, pooled NPCs, rapid scene changes, and intentionally disabled renderers; compare CPU/GC and GPU render time separately.
- [ ] Exercise balance precedence with legacy saves, legitimate lower/zero balances, unavailable SQLite and failed writes on copied profiles. Do not migrate live profiles yet.
- [ ] Force a failed DB write, close/reopen with pending balance, restore DB and verify replay does not duplicate money; check explicit profile reset and external DB writers.
- [ ] Compare search diagnostics and complete-route equivalence under 1.5 ms, node-only and tiny budgets. Record projection/index build separately and compare frame-time percentiles. Use the RTA* evaluation protocol before changing algorithm.
- [ ] Profile same route, vehicle, weather and NPC count at 30/60/120 FPS. Record main thread, render thread, GPU frame times, GC allocations, memory, P95/P99 frame time and startup time. Compare equivalent Development builds.

## Fifth implementation wave — source simplification

- Replaced bootstrap reflection-based command invocation/property lookup with typed Mono.Data.Sqlite commands and deterministic disposal; schema and seed order, native provider probing, serialized settings and required-table checks remain.
- Removed unreferenced private NPC recovery/obstacle/personality helpers and obsolete delivery roadside/stub methods. Active collision, lane, spawn and delivery logic is unchanged. Retained serialized settings and public enum/API compatibility, including the obsolete obstacle classification toggle until scene/prefab migration is explicitly reviewed.
- Consolidated three main-menu settings row builders and repeated apply/save calls while retaining control dimensions, alignment, callbacks, labels and input behavior.
- RTAA* feasibility is documented separately; no navigation algorithm or GPS/minimap behavior changes in this wave. New pull requests and PR updates are deferred until final review.
- Validate startup on fresh and existing SQLite databases (including provider unavailable), menu slider/dropdown/toggle behavior and full NPC/delivery smoke tests before merge. Private method removals are based on source references; Unity scene/LFS and reflective invocation checks remain a runtime validation gate.

## Deferred navigation research

The project owner's candidate is RTAA* (Real-Time Adaptive A*), not classic RTA*. The feasibility study and future Adaptive GPS & Navigation Overhaul plan are in [RTA_STAR_EVALUATION.md](RTA_STAR_EVALUATION.md). Do not implement a new navigation algorithm or alter GPS behavior during the current code cleanup. Benchmark stale route publication, map matching, high-speed tracking and minimap independently before an explicitly approved prototype.

## Pending decisions

The current read order is SQLite > PlayerPrefs > JSON; choose final durable economy/storage ownership only after inspecting real save and SQLite migration cases. Transaction rollback and cross-store economy reconciliation still need runtime integration tests. Establish a driving baseline before further traffic scheduling or motor/AI separation. Avoid broad DOTS/ECS migration or package upgrades until measurable need.

## Verification note

Source changes and Git commits can be reviewed remotely. Unity Editor, player builds, NUnit runs and live profiler captures have not been executed in the remote authoring environment. Treat this branch as a draft until these gates pass.
