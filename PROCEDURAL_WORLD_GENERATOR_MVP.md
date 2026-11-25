# Procedural World Generator V5 — terrain design preview

**Branch:** feature/procedural-world-generator-mvp

**Generation is intentionally locked.** No Unity scene, terrain asset, road mesh, or authored road is modified by Preview. The previous bake code is retained for later integration, but V2 road hierarchy and authored-road proposals are not yet baked. Do not use the old Master Builders to test this layout.

## Test in Unity

1. Pull the branch and open GameScene (Git LFS assets present).
2. Tools > Delivery Driver > City > Procedural World Generator.
3. Detect Terrain Layout & City Roots. Supports 24 land tiles (two 4 x 3 blocks with an empty water gap), or 28 including four sea tiles, along X or Z.
4. Confirm city roots, city side and actual Sea surface Y; keep Protect existing Kizilay district enabled.
5. Select both cities and two bridges, then Preview Layout. For troubleshooting, deselect bridges or one city. No scene assets are written.
6. Inspect the whole city and individual districts in top-down Scene View. Use the road class visibility toggles to inspect the primary network separately.
7. Send the preview screenshot, counts, and warnings before enabling generation in a later revision.

## Recommended first settings

| Setting | Value |
|---|---:|
| Seed | 4281 |
| Arterial spacing | 600 m |
| Collector spacing | 250 m |
| Local street spacing | 95 m |
| Candidate budget per land tile | 180 |
| Straight road preference | 0.70 |
| Organic curvature | 0.35 |
| Local alternate routes | 0.75 |
| Existing road connection target | 4 |
| Bridge separation target | 65% |
| Maximum grade | 10% |
| Max cut/fill | 6 m |
| Bridge clearance | 8 m |

## Preview legend

- Blue: arterials (primary corridor segments).
- Orange: collectors (secondary corridor segments).
- Gray/white: local streets.
- Yellow: bridges.
- Green: proposed links from existing authored roads to the new graph. These are not actual connections yet; no existing road has been split.

## What changed

The V1 scattered point graph has been replaced by a nested, variably spaced street scaffold. Major streets are prioritized; local streets use seeded omissions, small coherent offsets and district-specific density, bend and jitter preferences. A limited diagonal gap-repair pass reconnects otherwise isolated areas. The slope solver, road clearance, protected areas and explicit graph endpoints remain in use. Budget scales with the number of land tiles (12 per city); it is a city-wide candidate cap derived from a per-tile setting, not a strict per-tile quota. Any necessary spacing adjustment is reported. Bridge selection evaluates available corridor pairs and prefers a much larger lateral gap; infeasible target separation is reported. If bridge approaches cannot be joined to the city graph, a city-only preview can still be shown.

## Limits

- Street scaffold is still based on a warped, hierarchical grid, not a full terrain-aware A* or independently rotated district layout. Visual quality needs iteration.
- Road classes are planning/preview metadata; mixed-width geometry and junctions are not implemented.
- Existing-road proposals are sampled from endpoints and suitable interior road locations; count is a target, not guaranteed. They are not included in the actual road graph and no road is split. Explicit connection/bake implementation is deferred.
- The wider bridge separation is a preference, not a guarantee. Protected districts, existing roads, grade limits and minimum physical separation are not bypassed.
- No Unity Editor runtime was available for this update. Compile, synthetic smoke test and real scene preview must be verified locally.
- No buildings, water, mesh, terrain deformation or optimization bake is performed in this iteration.

## Local checks

- [ ] Unity compiles.
- [ ] Tools > Delivery Driver > City > Run Procedural Planner Smoke Test prints PASS. This temporary synthetic test checks 24/28 tile detection, orientation, seed repeatability, hierarchy classes, grade, connected graph and two bridges; it does not test the actual GameScene.
- [ ] Preview shows all three road classes with coherent main corridors.
- [ ] Kizilay is preserved and proposed links are visible if feasible.
- [ ] Compare bridge spacing and report any fallback or missing connection warnings.
- [ ] Generate button remains disabled.

## V3 hillside coverage and diagnostics

The previous version discarded all street components except the one containing the bridge gates (or the largest component without bridges). In V3 isolated hillside nodes try contour-oriented neighbors first, then a component repair pass tries longer direct links and bounded terrain-aware A* connectors. Each accepted connector is checked against the existing elevation, clearance and junction-angle constraints. No grade limit is silently relaxed. The search is cancellable and bounded; it is not a guarantee of full terrain coverage.

Purple lines represent individually feasible but detached street components, not a connected road network. These are kept separately from the retained graph. Preview displays connected/detached counts, covered terrain tiles, empty districts, elevation/geometry rejection counts, and protected/blocked/missing node counts. Bridge gate connectivity warnings remain visible instead of silently removing the inland layout. Generation remains locked.

Start with Ankara only, Terrain-aware network repair enabled, grade 10%, cut/fill 6 m, then compare both-city preview. If necessary, explicitly try grade 12% and cut/fill 10 m as a separate preview experiment; do not treat higher values as a verified vehicle-safe setting.

## V4 Ankara-first backbone

Ankara now builds a connected primary route toward each terrain tile center (candidate target group) and the other bridge gate before expanding the regular street scaffold. Failed backbone links are retried with alternate candidates and, when enabled, bounded terrain routing. The Istanbul planner remains the V3 hierarchy. Purple detached segments remain visible for diagnosis.

New controls: Ankara backbone first (on), Ankara virtual district grading (on), District grading limit (30 m), X-ray road preview (on), Extra grading markers (red). The virtual model uses a coarse smoothed, approximately grade-limited height raster to choose shared junction heights. Road profiles are checked against the original sampled center and both shoulders using the explicit total grading allowance (at least the normal road budget); no automatic increase to the chosen grade limit. Protected areas and authored roads do not receive the expanded allowance. The original terrain assets are not edited. Red marks indicate sampled road positions requiring more than ordinary road cut/fill, NOT terrain modifications already performed. This is not a complete terrace or earthworks simulation. Actual grading, drainage, retaining structures, full collision and vehicle tests are deferred until layout approval.

The panel reports backbone targets reached, reserved arterials, direct growth count, connected terrain coverage and sampled tile seam discontinuity (maximum and tile pair). Test Ankara only with Seed 4281, local spacing 95 m, max grade 10%, normal cut/fill 6 m, district grading limit 30 m. Compare with virtual district grading off; if necessary test 45 m as an explicit preview experiment. Generation remains locked. Synthetic smoke test includes three stepped terrain height bands and verifies retained network depth-band coverage and unchanged height data. It has not been executed in Unity remotely.

## V5: terrain intelligence (preview only)

Detect the layout, then click **Analyze Terrain (read only)** to inspect actual land elevation and sampled slope/roughness. Green means raw ground slope <= half the selected road grade, amber <= selected grade, red > selected grade, gray protected/near existing roads. The map is coarse, not a certified road feasibility map. The sea row is excluded. Preview Layout integrates these samples into district-dependent bounded street orientation offsets, slope/roughness-based candidate priorities, and selection among feasible road curves by sampled cut/fill cost. Existing hard grade, width, protected-area and graph checks remain. Early authored-road proposals reserve room and become Ankara backbone targets; proposals are still not actual road splits. Unconnected proposals are removed, with a later retry.

Default controls: Terrain-adaptive city design on, Terrain direction influence 0.65, District orientation variation 0.65, Prefer lower earthworks 0.8, Preserve V4 coverage on. Coverage guard runs the same seed with adaptive design off and can select the V4 plan if tile coverage, bridge connectivity, retained road count or detached-road count regresses. This doubles planning work and does not guarantee the same output as a previous run on a modified scene. Turn guard off to inspect the raw V5 candidate; the status explicitly reports the selected version. Compare sampled mean cut/fill, peak adjustment and fraction of road length exceeding the normal cut/fill budget; these are not earthwork volumes. Real terrain grading, independent rotated district meshes, park reservations, mixed road widths and baking authored links remain deferred. The scaffold is still hierarchical and only moderately warped. No scene or terrain data changes; Generate remains locked. Unity compile and smoke test not run remotely.
