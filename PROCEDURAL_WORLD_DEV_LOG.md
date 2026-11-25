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


## 2026-11-25 — V7.2 first real GameScene preview (seed 4281)

User supplied full Copy preview report and Unity screenshot (Unity 6000.3.12f1). Selected layout V7.2 optimized; elapsed 113.9 s, score 90.1/100. Actual local Git SHA not supplied; expected source baseline a838bbbb (docs-only commit 3271fa9). Compilation sufficient to run preview; Console and smoke-test outcomes not provided. Final JSON and rejected-stage snapshots not yet provided.

| Metric | V7.2 final observed |
|---|---:|
| Coverage Ankara / Istanbul | 12/12 + 12/12 |
| Retained / detached | 2871 / 10 |
| Ankara mean cut / fill | 0.60 / 0.48 m |
| Peak / extra grading | 19.1 m / 6.1% |
| Sharp streets / short stubs | 19 / 22 |
| Authored proposals / bridges | 4 / 2 |
| Bridge gap | 1242 m |
| Prefab fits / lots | 1987/1987 (36 models) |
| Park access | 72/73 (four additional entrances) |
| Vertical flags / near straight-through junction pairs | 1748 / 268 |
| Terrain seams >1m | 0/17 |

Stage 0 V6 baseline: 51.3 s, cut/fill 2.84/1.49 m, peak 21.9 m, extra 31.8%, radius 20, vertical 2380, junction 805. Stage 1 accepted (12.2 s; cut/fill 1.44/0.87, vertical 1974, junction 346). Stage 2 accepted (11.1 s; reference earthworks restored, vertical 2067, junction 282). Stage 3 rollback (2.6 s; 40 direct + 8 routed attempts, none promoted). Stage 4 accepted (2.9 s; radius 20->19, vertical 2064). Stage 5 rollback (1.9 s; 14 fixed-gate approaches, 187 coastal spline ends audit only).

| V7.2 stage | Decision | Observed candidate / finding |
|---|---|---|
| 5a hotspot | Rollback, no measurable improvement, 2.2 s | 0/5 eligible roads improved; fixed endpoints |
| 5b T-junction | Rollback, network/access regression, 1.4 s | 2 groups, 2 splits, all 10 promoted, 2885 retained / 0 detached candidate only; 5 attempts, 3 routed, 3 connector rejects; candidate cut/fill 0.59/0.61 m, extra 7.0%, radius 22, vertical 2079, junction 286; peak 19.1 m |
| 5c bend | Rollback, no measurable improvement, 1.6 s | 2/19 roads improved locally but count remained 19 |
| 5d vertical | Accepted, 2.9 s | 122/180 roads improved; flags 2064->1748, junction 282->268, earthwork metrics unchanged at report precision |
| 5e bridge | Rollback, network/access regression, 1.8 s | 126 tests, 126 protected rejects, 0 feasible, 0 replaced |
| 6 urban | Accepted, 3.4 s | 1987 prefab fits, 72/73 parks |

Virtual terrain: 11866 valid sections, nominal spacing adjusted to 24.2 m, 209 missing sections, 0 missing shoulder samples, 234 incompatible 4m cells, 313 steep sections (wall candidates), 0 protected/existing-road samples, 3 parcel shoulder samples. 50 hotspot intervals; peak 19.11 m at (13.0,41.1,895.4), another cluster near (2697.4,64.3,1134.0) at 17.70 m. These are sampled diagnostics, not certified mesh collisions or wall counts. Mandatory checks NOT MET; Generate remains locked.

Settings verified: seed 4281, local budget 180, vertical radius 80, spacing 20, shoulder slope 0.67, profile sweeps 16, preferred cap 15, radius 18, grade .1, maxCutFill 6, district limit 30, blend 10, seaY -2E-05, bridge clearance 8, separation .65, protection on, both cities/two bridges on. Additional settings: arterial/collector/local spacing 600/250/95, width 8.5, sidewalk 2.2, nodesPerTile 180, maxNodesPerCity 170, arterial/collector length target .15/.25, contour .85, curve .35, districtVariation .65, earthworkPreference .8, gradingOptimization .9, loops .75, minLocalStub 45, openSpaceFraction .12, parcelAccessGrade .15, straightPreference .7, terrainInfluence .65, ankaraLow false, all prior V6/V7/terrain/staged/local/bridge gates toggles true. Report archived metrics without machine-specific paths or scene object IDs.

Checkpoint folder identifier: 20261125-193817-821-BD0657ED (under local Library/ProceduralWorldCheckpoints). Source stage order implies 07-rejected.json = 5b, 10-rejected.json = 5e; verify labels in JSON. Archive entire directory outside Library. Request 06-rejected.json (5a) only if needed, 07-rejected.json (5b), 10-rejected.json (5e), and final.json; prioritize 07 and final.

### Source inspection and next V7.2.1 proposal (not implemented)
- Staged.cs checks StageGraphSafe first, then NoTerrainRegression, then local quality thresholds. 5b would also fail terrain and geometry goals even if graph guard were fixed; do not loosen them or promote current candidate.
- StageGraphSafe returns only a generic boolean; PreservedNetwork checks all plan node IDs for uniqueness, then exact retained edge geometry and split chains. Preview Capture/Restore includes authored endpoint nodes (Urban.cs can create manual endpoint id=-1); multiple authored anchors or duplicated IDs are a plausible guard failure, not established by report. Inspect snapshot node IDs and exact old-edge lineage; bridge branch can also reject old bridge endpoints without a land edge even if unchanged. Distinguish actual failure from no-op. Add explicit per-invariant guard reasons and tests with authored endpoints, unchanged bridge candidates, exact splits, and duplicate identities; maintain real graph and authored-link safety.
- 5b accepts first locally viable option per detached group before aggregate guard; improve by independent group transactions, candidate ranking across several safe connectors, local profile/bend/junction repair, guard at group level. Preserve exact original retained road shape for split; if subsequent refinement is needed, separately validate topology and corridor. No global guard relaxation. 5c partial radius severity gains currently discarded if flag count unchanged; introduce measured severity without accepting regressions. 5a fixed-endpoint limitations require local connected junction neighborhood candidates (not blanket node shifts).
- 5d successful; keep and later improve with severity/distribution and junction-surface checks, not simply larger attempt budget.
- 5e 100% protected rejection means explicit authored-corridor audit/verified approach design required; no protection bypass.
- Virtual terrain missing sections need road/sample coordinates and reason categories (road footprint vs outer blend crossing tile/shore bounds); classify same-junction overlaps vs independent road conflicts; preserve unresolved warnings; continuous surface later. No physical generation or scene/terrain writes.
- Next obtain snapshots and focused screenshots (Hotspots, VerticalProfile, VirtualTerrain). Prioritize 5b diagnostic/repair V7.2.1, then hotspots/geometry and virtual terrain checks before V7.3. No C# edits performed for this test entry.
