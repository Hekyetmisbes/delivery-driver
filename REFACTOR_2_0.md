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
| 2 | SQLite transaction boundaries, event separation, data ownership/migration | Typed SQLite, snapshot transaction and cadence committed; cross-store migration pending |
| 3 | Low-allocation improvements, profiling and traffic fixed-tick scheduler | FIFO pool, ground queries, fuel binding and physics-tick scheduling committed; profiler and traffic regression pending |
| 4 | Split NpcCarAgent, QuestManager, DeliveryManager by responsibility | Planned; preserve behaviors with characterization tests |
| 5 | Road graph baking, navigation, world culling and camera rendering | Planned; compare route correctness and frame times |
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
- [ ] Profile same route, vehicle, weather and NPC count at 30/60/120 FPS. Record main thread, render thread, GPU frame times, GC allocations, memory, P95/P99 frame time and startup time. Compare equivalent Development builds.

## Pending decisions

Choose authoritative economy/storage model only after inspecting real save and SQLite migration cases. Transaction rollback and cross-store economy reconciliation still need runtime integration tests. Establish a driving baseline before further traffic scheduling or motor/AI separation. Avoid broad DOTS/ECS migration or package upgrades until measurable need.

## Verification note

Source changes and Git commits can be reviewed remotely. Unity Editor, player builds, NUnit runs and live profiler captures have not been executed in the remote authoring environment. Treat this branch as a draft until these gates pass.
