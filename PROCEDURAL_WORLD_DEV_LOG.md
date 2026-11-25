# Delivery Driver — Procedural World Development Log

## How to continue
Read this file first when resuming city-generator work, then the relevant version report and source files. Append dated test/implementation entries with code commit, scene, settings/fingerprint, observed metrics, stage acceptance/rollback reasons, limitations, artifact names and next actions. Distinguish measured Unity results, source inspection and targets. Never overwrite an earlier measured baseline with a target. Keep `main` stable; work on `feature/procedural-world-generator-mvp`. Do not enable Generate, alter scene/terrain assets, or introduce physical spline/mesh generation during preview validation.

Version history: [MVP](PROCEDURAL_WORLD_GENERATOR_MVP.md) · [V7](PROCEDURAL_WORLD_V7_REPORT.md) · [V7.1](PROCEDURAL_WORLD_V71_REPORT.md) · [V7.2](PROCEDURAL_WORLD_V72_REPORT.md). General project guidance: [AGENTS.md](AGENTS.md).

## 2026-11-25 — V7.2 test handoff (source inspected; Unity test pending)

- V7.1 code reference: `1096b53da412508699a53841f905cc46498a39fe`.
- V7.2 implementation: `cc3d9141122f4da26a1bed9d4cc995fde62a617c`.
- Verified V7.2 source branch head before this documentation commit: `a838bbbbac43496c525de637824dc670bdd87f32`.
- V7.1 user-reported real scene result is the reference. V7.2 has not yet been compiled or tested in the user's Unity session for this handoff. No new C# code or scene changes in this entry.
- V7.1/V7.2 baseline is regenerated from inputs, not automatically resumed from a saved JSON graph. Preserve the original settings and archived V7.1 checkpoints.
- V7.1 shared junction/profile solver and balanced earthworks remain the foundation. Local stages 5a–5e run as cloned candidates after Stage 5; only safe measurable improvements are accepted. Stage 6 screens lots and parks. Inspect stage report, not merely the aggregate score.

### Reference metrics and goals

| Metric | V7.1 measured (user report) | V7.2 target | V7.2 measured |
|---|---:|---:|---|
| Urban quality | 89.8/100 | Diagnostic; not sole acceptance gate | Pending |
| Ankara / Istanbul coverage | 12/12 + 12/12 | 12/12 + 12/12 | Pending |
| Retained roads | 2871 | Preserve original topology; splits may increase count | Pending |
| Detached segments | 10 | 0 | Pending |
| Ankara sampled mean cut / fill | 0.60 / 0.48 m | No increase | Pending |
| Peak adjustment | 19.1 m | <15 m | Pending |
| Extra grading | 6.1% | No increase | Pending |
| Horizontal radius flags | 19 | 0 | Pending |
| Short stubs | 22 | Diagnose; no automatic pruning | Pending |
| Kizilay authored proposals | 4 | >=4 | Pending |
| Bridges / separation | 2 / 1242 m (~31%) | 2 connected / 65% (~2600 m) | Pending |
| Measured prefab fits | 1987 | Preserve useful access; report counts | Pending |
| Screened park access | 68/73 | All screened parks accessible | Pending |
| Vertical profile / junction grade flags | Not measured | Measure and reduce | Pending |
| Virtual sections and overlap/protected/parcel/missing/steep samples | Not measured | Diagnose, no silent acceptance | Pending |
| Planning time | 73.0 s (V7.2 report's V7.1 reference) | Measure fresh run | Pending |

Sampled height differences are not earthwork volumes. V6 quality score used a different parcel scan and is not directly comparable.

### First controlled V7.2 test
1. Check local branch and working-tree status before pulling; do not overwrite local work. Pull the feature branch, allow Unity to compile, report compiler errors if any. Use the same saved scene as the successful V7.1 run (user report calls it GameScene; record actual scene path). Open Tools > Delivery Driver > City > Procedural World Generator. Click Detect Terrain Layout & City Roots.
2. Preserve all V7.1 settings, including street spacing, city toggles, authored anchors, widths, park/prefab and district settings. Seed 4281; max grade 10%; normal cut/fill 6 m; district grading limit 30 m; terrain shoulder blend 10 m; sea surface Y -0.00002; bridge clearance 8 m; bridge separation target 65%; both cities, two bridges, Protect existing Kizilay enabled. V7 quality design, V7.1 staged optimization, V7.2 local optimization, Try alternative bridge gates enabled. Profile sweeps 16; local repair budget 180; preferred grading cap 15 m; local turn radius 18 m; vertical curve radius 80 m; virtual section spacing 20 m; shoulder slope limit 0.67. Auto-save staged checkpoints enabled.
3. For the first measured run, disable Reuse verified previews to avoid a cache hit (or clear session preview caches before planning). Click Preview Layout (no scene changes). Do not use Generate or Clear Generated Roads / Restore Terrain.
4. Return to final optimized plan if viewing a historical stage. Use Copy preview report and send its full unedited text, including settings, timings, Stage 0–6 and 5a–5e results, candidate metrics and warnings. Save preview JSON as V72_seed4281_final.json outside Library for archival. Keep original stage checkpoints; send final.json or exported JSON if feasible.
5. Capture overview, Urban quality/mandatory checks, Hotspots (Focus largest hotspot if available), VerticalProfile, VirtualTerrain and one flagged section (Inspect virtual section / Focus selected section, original vs planned Y). Enable Show terrain analysis layer and select each relevant layer separately. If no flag/section, report that rather than manufacturing one.
6. Optionally run Tools > Delivery Driver > City > Run Procedural Planner Smoke Test in a disposable editor session; send PASS or full error stack. If V7.2 regresses or results are ambiguous, repeat with only V7.2 local optimization disabled to obtain a same-scene V7.1 control; retain all other settings. Cache test is separate.
7. Checkpoint path in source: Library/ProceduralWorldCheckpoints/<UTC timestamp>-<fingerprint prefix>/ (numbered accepted/rejected snapshots and final.json). Library is local/temporary; copy valuable checkpoints outside Library. Do not commit large raw snapshots or machine-specific data to this public repository without review.

### Source map and limits
- `Assets/Editor/City/ProceduralWorldGeneratorWindow.cs`: editor menu, preview controls, terrain layers, Generate disabled.
- `Assets/Editor/City/ProceduralWorldGeneratorWindow.Quality.cs`: quality fields, stage comparison, Copy preview report, Save preview JSON, checkpoint storage, section inspection.
- `Assets/Editor/City/ProceduralWorldPlanner.Staged.cs`: stage sequence, candidate guard and mandatory checks.
- `Assets/Editor/City/ProceduralWorldPlanner.Local.cs`: hotspots, interior T-junction repair, curvature, alternative bridge gates and virtual cross sections.
- `Assets/Editor/City/ProceduralWorldPlanner.Preview.cs`: fingerprint/snapshot/refinement integration.
- `Assets/Editor/City/ProceduralWorldSmokeTest.cs`: editor smoke tests including V7.2 coverage/graph/terrain invariance.
- Source guards use small numeric tolerances; the targets above are stricter evaluation goals, not a guarantee. A split must retain the original path and connectivity, not simply inflate road count. Virtual terrain is sparse sampled cross-sections, not a continuous heightmap or validated mesh/collider. New authored Kizilay bridge corridors, stub cleanup and Istanbul neighborhood redesign remain deferred.
- Generate remains locked even if mandatory preview checks pass.

### Next decision
Await actual Unity compiler result, final preview report and JSON/screenshots. Compare each accepted candidate with its input; inspect rollback reasons and sample warnings. Preserve the V7.1 baseline. Fix only demonstrated issues; if V7.2 is adequate, proceed to V7.3 production readiness (continuous virtual terrain, junction/spline/corridor validation) before V8 controlled physical generation and driving tests.
