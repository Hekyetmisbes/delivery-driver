# Delivery Driver — Procedural World Generator V7 implementation report

**Branch:** feature/procedural-world-generator-mvp  
**V6 reference commit:** 7b5f8625210e539742b1950fc091774068b9aacf  
**Mode:** read-only Unity Editor preview. Generate remains disabled. No scene, terrain, mesh, water, prefab instance or runtime-road graph bake. No new EasyRoads dependency.

## 1. Baseline from the real GameScene test

The user-provided V6 screenshots show 24 land terrain tiles, 1000 x 1000 m each, and a 1000 m empty sea corridor. Ankara raw sampled altitude 1.4–107.4 m; Istanbul flat at 0 m. V6 was retained by the V5 coverage guard.

| Metric | Observed V6 | V7 target (unverified) |
|---|---:|---:|
| Retained road segments | 2871 | sufficient connected coverage |
| Detached road segments | 10 | 0 |
| Existing-road proposals | 4 | >=4 |
| Road length (V6 panel) | 293.82 km | adequate network length |
| Park / plaza reservations | 73 | accessible, useful open space |
| Sampled building lots | 2260 | actual prefab-fit and access screened |
| Ankara sampled mean cut | 2.84 m | <2 m |
| Ankara sampled mean fill | 1.49 m | ~1 m |
| Ankara sampled peak adjustment | 21.9 m | <=15 m preferred |
| Ankara road length above normal grading allowance | 31.8% | <15–20% |
| Bridges | 2 | 2 |
| Bridge lateral corridor separation | 1242 m (31%) | 2600 m / 65% where feasible |
| Planning duration | 99 s with guards | 30–45 s aspiration; cache repeat faster |

The screenshot did not show numeric terrain seam results or final tile-by-tile coverage. The red earthworks band is evidence of concentrated cut, not proof of its underlying seam cause.

## 2. Implementation scope

| Priority | Change | Implemented behavior |
|---|---|---|
| P0 | Regional grading optimization | Grade solver tries normal, 10 m, preferred cap, then existing hard allowance; slope-dependent virtual node shifts; high earthwork penalties on center and shoulders; GradingZones layer. |
| P0 | Backbone repair | Final root-directed component/priority-target search before pruning; reports reached/unresolved targets, bounded direct and routed repair. |
| P0 | Detached segments | Final targeted repair of small fragments; unresolved diagnostics retained in purple, not hidden. |
| P0 | Authored road links | Early V6 reservations retained, manual anchors supported; V7 guard compares per-city proposal counts and successful manual-anchor count. Actual spline splitting is still deferred. |
| P1 | Road geometry | Sampled circumcircle radius (local 18 m default, collector x1.2, arterial x1.5), minimum non-bridge junction angle 52 degrees, rounded terrain routes and final grade/radius checks. |
| P1 | Short streets | Remove nonessential local dead-end stubs under 45 m; keep gate, authored target, priority target, park-near and sole tile-coverage edges. |
| P1 | Road hierarchy | Measure horizontal road length by class; downgrade eligible non-spine chains toward 15% arterial / 25% collector / remainder local. These are targets, not hard quotas or actual mesh widths. |
| P1 | Prefab-sized lots | Reuse RoadsideBuildingPlacementTool's building filter and precise root-local bounds method; account for root scale, padding, raw foundation relief, overlap and sampled access slope. |
| P1 | Park and lot access | Screen candidate approaches from planned road/sidewalk edge to front/park boundary; check sampled grade, other road clearance, protected land, existing road obstacles and park approach through proposed lots. Unresolved parks shown amber. |
| P1 | Bridge approaches | Additional diagonal candidates, protected-rectangle prefilter, stronger separation score, bounded curved land approaches (sin² offsets; straight sea segment), actual segment-length grade check and full lateral envelopes. |
| P2 | Terrain cache | Raw analysis raster shared between candidate/comparator builds on one detected layout; fresh outer build invalidates raw cache. Exact verified analysis result reused in the editor session. |
| P2 | Plan cache | Up to three session plans keyed by settings, scene, terrain heightmap contents, spline samples, zone geometry and building asset dependencies. Repeat unchanged preview reuses its plan. |
| P2 | Regional replan | Select NeighborhoodZone or terrain tile; optimize up to 180 whole interior non-spine road paths, fixed endpoints/topology, external roads and authored/bridge paths unchanged. Global parcel/access overlays refreshed. |
| P2 | Save/load | Versioned JSON snapshot of graph, paths, diagnostics, areas, analysis, quality and original input signature. Load read-only. Mismatched input signature disables refinement; save before experiments. |
| P2 | Urban quality evaluator | Weighted heuristic scores for connectivity, terrain, sampled geometry, measured prefab-fit lots, bridge separation and park access, plus detailed measurements; editable weights. Not a substitute for mandatory grade/protected-area checks. |

## 3. V6 preservation and performance

V7 runs a fresh candidate and, when Preserve V6 quality is enabled, compares against same-seed raw V6 with V7 switched off. If raw V6 fails, its previous fallback workflow is tried. V7 guard rejects lower tile coverage, bridge connectivity/separation regression, fewer authored proposals per city, fewer successful manual anchors, more detached roads, >25% network length loss, or material sampled grading regression. Baseline fit scores for parks/parcels are not comparable to the V7 screening rules. The selected version is labeled explicitly (V7, V6 fallback, or earlier fallback). Guard off shows raw V7 and reports failures rather than silently reverting. This is not comparison to a frozen copy of the user's previous scene; save a V6 JSON snapshot if recreating baseline locally.

V7 avoids the normal three-level nested guard when the raw V6 comparison is available, prefilters protected straight bridge routes and caches raw samples, geometry and prior plans. First-time candidate computation may still take longer than V6 because repair, curved approaches, strict profile attempts, prefab and access checks and full input hashing add work. Actual timing needs Unity. A cache hit includes input fingerprint verification and is not instant for very large heightmaps.

## 4. How to test

1. Pull feature/procedural-world-generator-mvp and let Unity compile. Open GameScene. If the Console has compile errors, report them before running the tool.
2. Tools > Delivery Driver > City > Procedural World Generator. Detect Terrain Layout & City Roots. Verify 12+0+12, sea width, city side, sea Y and protect Kizilay.
3. Keep the V6 layout/terrain settings that produced the observed result; enable V7 and Preserve V6 quality. Defaults below.
4. Click Preview Layout (no scene changes). Note Selected layout. If fallback, copy the full report before testing raw V7 with Preserve V6 quality off. Do not change grade or cut/fill first.
5. Inspect clean plan (analysis off), Earthworks, GradingZones, Seams, then building lots / parks. In GradingZones green <=3 m, yellow up to ordinary allowance, orange <=10 m, red <= preferred cap, purple above preferred cap. A purple grading line is NOT the same as detached-road purple; toggle one layer at a time.
6. Copy preview report button includes metrics, notes, warnings and the calculation settings. Save preview JSON to Library or an external folder. The default folder is Library. Saving to Assets is not necessary.
7. Re-run unchanged Preview Layout and confirm cache hit and elapsed time. Change seed or a heightmap input only in a disposable test scene to verify invalidation. Load saved snapshot; exact scene/settings match allows regional refinement, mismatch is view-only.
8. Select a district NeighborhoodZone or a terrain tile, refine. Verify other road geometry remains fixed and compare resulting grade report. Refinement does not rebuild new district topology or change bridges/anchors; original full plan remains in session cache if enabled.
9. Save chosen plan before running Tools > Delivery Driver > City > Run Procedural Planner Smoke Test. Temporary additive scene may clear the preview window. Tests include existing V4/V5/V6 scenarios plus V7 raw flat-city radius/grade, snapshot geometry/overlay round trip, heightmap cache-key invalidation and unchanged terrain. Not executed remotely.
10. Keep Generate disabled. Report exact selected layout, terrain coverage, bridge separation, detached/priority target count, four authored proposals, sampled mean/peak/extra grading, measured prefab-fit lots, park access, plan/cache timings and warnings.

### Default V7 settings

| Setting | Value |
|---|---:|
| Enable V7 quality design / Preserve V6 quality | On / On |
| Seed | 4281 |
| Both cities / two bridges / Protect Kizilay | On |
| Preferred grading cap | 15 m |
| Local turn radius | 18 m |
| Short local stub | 45 m |
| Arterial / collector length target | 15% / 25% |
| Access slope limit | 15% |
| Connectivity / terrain / geometry / parcel / bridge / park weights | 30 / 25 / 15 / 15 / 10 / 5 |
| V6 Terrain Adaptive / contour / earthwork / open space | on / .85 / .90 / 12% |
| Arterial / collector / local spacing | 600 / 250 / 95 m |
| Normal max grade / cut-fill / district grading limit | 10% / 6 m / 30 m |
| Bridge separation / clearance | 65% / 8 m |
| Fast overview rendering / reuse verified previews | On / On |

## 5. Known limitations and blockers

- Unity Editor, compiler and runtime unavailable in the connector environment; no claim of successful compilation, passed smoke test, real performance improvement, V7 target attainment or vehicle driveability.
- V7 still uses the V6 hierarchy/subregion scaffold. Regional refinement is fixed-topology path rerouting, not independent regeneration of block topology. Boundary constraints and parks can leave some routes unresolved.
- Grading is a virtual sampled profile choice, not a physical terrace/earthwork/retaining-wall mesh or volumetric estimate. The preferred cap can be exceeded within the original explicit hard district budget, and this is reported. No terrain asset changes.
- Radius is a sampled horizontal geometry screen, not full final spline turning performance, vertical curvature or intersection swept-path validation. Road class targets only affect metadata.
- A lot matches measured prefab dimensions and a sampled approach; it is not an instantiated building, delivery spawn point, certified foundation or final driveway. Catalog unavailable means no verified prefab-fit count. Park access is a sampled screening proxy, not a built accessible sidewalk.
- Bridge approach search is bounded; 65% separation may remain infeasible under protected land and terrain constraints. No unauthorized protected-zone crossing is enabled. Grade and clearance are sampled.
- V6 fallback does not receive V7 topology or prefab/park screening. Quality score on fallback is diagnostic. Neither score nor target values override hard checks.
- Preview cache is per-session, up to three road plans. Snapshot is a read-only preview format, not a trusted bake or game save. Scene/settings input hash mismatch prevents regional refinement. Manual anchor choices are session state; snapshots preserve result locations, not settings application.
- Final Simple Road Placement geometry, actual road splitting/kavşak generation, terrain collider/mesh bake, building placement, water and driving/traffic tests are deferred until the preview is approved.
