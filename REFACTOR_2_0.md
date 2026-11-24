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
| 1 | Save integrity, quest completion/failure guards, safe vehicle switching | First implementation committed; PlayMode verification pending |
| 2 | SQLite transaction boundaries, event separation, data ownership/migration | Planned; establish authoritative data policy before migration |
| 3 | Low-allocation improvements, profiling and traffic fixed-tick scheduler | Dictionary lookups simplified; scheduler unchanged pending baseline |
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
- [ ] Profile same route, vehicle, weather and NPC count at 30/60/120 FPS. Record main thread, render thread, GPU frame times, GC allocations, memory, P95/P99 frame time and startup time. Compare equivalent Development builds.

## Pending decisions

Choose authoritative economy/storage model only after inspecting real save and SQLite migration cases. Separate UI timer events from persistence events, with transaction rollback tests. Fix NPC scheduling using physics ticks and high-risk traffic overrides only after establishing a driving baseline. Avoid broad DOTS/ECS migration or package upgrades until measurable need.

## Verification note

Source changes and Git commits can be reviewed remotely. Unity Editor, player builds, NUnit runs and live profiler captures have not been executed in the remote authoring environment. Treat this branch as a draft until these gates pass.
