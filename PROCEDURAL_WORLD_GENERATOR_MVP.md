# Procedural World Generator V6 — urban design preview

Branch: feature/procedural-world-generator-mvp. **Generate is locked.** This is an editor-only planning update. It does not modify terrain heights, scenes, existing spline roads, buildings or mesh assets. The existing Simple Road Placement Tool remains the eventual geometry backend. No EasyRoads dependency added.

## Quick start

1. Pull the branch. Open GameScene, then Tools > Delivery Driver > City > Procedural World Generator.
2. Detect Terrain Layout & City Roots. Verify 24 land terrains (or 28 including water tiles), city roots and Sea surface Y.
3. Click Analyze Terrain (read only). Enable Show terrain analysis layer and select Slope, Elevation, Contours, Seams or Buildability. Earthworks is populated by the V6 road preview.
4. Enable both cities and two bridges, protect existing Kizilay, enable V6 urban design and Preserve V5 coverage. Click Preview Layout.
5. Read Selected layout: V6 or V5 fallback. If fallback appears, disable Preserve V5 coverage to inspect the raw candidate (it may fail validation). Existing V5 comparison may itself use the earlier V4 coverage guard; this is reported, not a claim of an identical saved V5 result.
6. Inspect roads with analysis layers individually. Enable Building lot preview and Park / plaza reservations. For overview performance leave Fast overview rendering on. Zoom in or disable it for thicker individual road lines.
7. For manual Kizilay access, close/disable other road editing tools first. In Manual existing-road anchors select an existing SplineRoad and normalized spline position, or arm Pick anchor in Scene View, then Shift+left click near a spline (Escape cancels). Repeat for desired anchors. They are editor-window session data only; no existing road is split. Preview again. Green marker = feasible proposal attached to retained graph; red = unresolved. Automatic proposals are tried as well. Manual anchors are high-priority requests, not a bypass of constraints. A failed anchor is reported rather than fabricated.
8. Do not generate or run the legacy master builders on this preview. Send the results and warnings for review.

## Suggested settings

| Setting | Value |
|---|---:|
| Seed | 4281 |
| Planning mode | Terrain Adaptive |
| Contour road preference | 0.85 |
| Earthwork optimization | 0.90 |
| Open space target | 12% |
| Preserve V5 coverage | On |
| Terrain-adaptive city design | On |
| Terrain direction influence | 0.65 |
| District orientation variation | 0.65 |
| Existing road connection target | 4 |
| Bridge separation target | 65% |
| Arterial / collector / local spacing | 600 / 250 / 95 m |
| Maximum grade | 10% |
| Normal cut/fill | 6 m |
| District grading limit | 30 m |
| Bridge clearance | 8 m |

## V6 implementation scope

P0: read-only sampled slope/elevation, 10 m marching-squares contours, tile seam discontinuities, buildability screening and signed earthwork overlays; contour-sensitive street directions and candidate/search costs; wider curve alternatives; elevation profile preference closer to raw cross section within hard grade and grading bounds; manual and automatic existing-road proposals before backbone planning; coverage guard.

P1: independent oriented neighborhood subregions inside the retained major-road framework, with actual district boundaries clipping the subregions; reserved park/plaza rectangles before local road creation; sampled roadside lots screened for raw-ground relief, road clearance, protected areas, open spaces and other lots; feasible bridge corridor ranking by separation and approach earthworks/endpoint terrain. Roads do not cross reserved open spaces, including the declared road margin. Park target is area-based and may be missed. Parcel preview capped at 3000 total, split across both cities when selected.

P2: cached terrain candidate costs, cached polylines/analysis quads, batched distant roads, seams, contours, earthwork and parcel outlines; cached panel counts and measured planning time. Guard comparisons and routing are still CPU-heavy and can take longer than V5. Cancellation supported in the main planning loops. Actual editor frame-time measurements require Unity.

## What to verify

- Run Tools > Delivery Driver > City > Run Procedural Planner Smoke Test. Existing layout, bridge, stepped terrain and seed tests use baseline mode; new V6 flat-city test disables fallback and checks parks, parcels, seam data, graph, grade, reservation clipping and unchanged terrain.
- Real Ankara: reach 12/12 tiles, no detached roads, two bridge paths connected across both cities, and at least four feasible authored proposals if available.
- Compare V5/V6 sampled mean cut/fill, peak adjustment, and percentage of road length requiring more than ordinary grading. Aim below 15–20% extra grading and 10–15 m peak; these are targets, not promised results.
- Verify park/plaza and parcel overlays, manual anchor results and neighborhood street orientation. In case of fallback, the selected layout does NOT contain V6 parks, parcels or access proposals; inspect raw V6 separately.
- Verify Scene View rendering and planning duration on actual hardware.

## Limitations / safety

Unity Editor/Play Mode was not available remotely; compilation, smoke tests and actual GameScene performance/quality are unverified. Analysis is sampled, not a civil engineering survey, certified foundation map or volumetric cut/fill model. Contour-first is a preference-driven local street/search algorithm, not a global continuous contour tracing solver. District subregions remain constrained by major blocks; they are not a fully freeform polygonal subdivision. Building lots are conservative candidate footprints, not building assets or cadastral polygons. Manual proposals can fail for existing-road clearance, slope, protected terrain or network degree. Bridge spacing and grading targets are not guaranteed. Physical road widths, intersection meshes, road splitting, final earthwork/collider bake, buildings and water are deferred until preview approval; Generate remains disabled. No scene/terrain assets are changed.
