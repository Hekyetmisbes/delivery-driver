# Procedural World Generator MVP

Branch: feature/procedural-world-generator-mvp

## Quick start (Unity Editor)

1. Pull the branch and open **a copy of GameScene.unity**. Ensure Git LFS assets are present.
2. Open **Tools > Delivery Driver > City > Procedural World Generator**.
3. Click **Detect Terrain Layout & City Roots**. The scene must contain 24 land Terrain objects in two axis-aligned, equally sized 4x3 blocks with a positive sea gap, or 28 Terrain objects including the four middle sea tiles. Both X and Z layouts are supported.
4. Check the Ankara and Istanbul root fields, side toggle, and **Sea surface Y**. The actual gap between city shores (empty space or four sea tiles) is the sea corridor; no normal city roads or terrain deformation are allowed there.
5. To test just Ankara, uncheck Generate Istanbul. Keep Protect existing Kizilay district enabled. Click **Preview Layout**. Cyan/orange lines are roads; yellow lines are the two bridge routes when both cities are selected.
6. Review warnings. If a city cannot connect its two gates, try a different seed or spacing. If the terrain is too steep, change the cut/fill budget cautiously or test one city first.
7. Click **Generate Roads & Bridges**. The tool clones affected land TerrainData and uses the existing SimpleRoadPlacementTool mesh, collider, sidewalk, lane marking, intersection, and bridge code.
8. Inspect the resulting scene and drive it before saving. The original TerrainData references can be restored with **Clear Generated Roads / Restore Terrain**. The generated mesh/terrain assets under Assets/Art/Terrains/ProceduralGenerated are deliberately retained on Clear for Undo. Delete old unused build folders only after confirming no scene references and no need for Undo.

## What this MVP does

- 12+12 land tile detection with optional four sea tiles, real corridor width, seeded scattered road nodes, district-sensitive node spacing, connected road component and optional cycles, varied curved paths rather than a square road grid.
- Bounded elevation profiles and corridor-local cut/fill on cloned land tiles; no writes to sea tiles, protected Kizilay or the immediate area of existing authored roads.
- Shared explicit intersection references and the same sampled road polyline for the spline and baked asphalt geometry; one network refresh after baking.
- Two actual bridge paths spanning the middle row (if both cities selected and a feasible plan exists). Bridge supports use the existing road generator.
- No deletion or rewriting of existing manual roads, optional continuation from one suitable existing dead end per city, no automatic splitting of authored roads.
- Manifest-based undo/clear, persisted generated meshes and terrain assets. No automatic scene save.

## Known limitations / local testing required

- No Unity editor execution was available when creating the branch. This is a source-level MVP, not a tested release.
- Uniform road/sidewalk width in each build; mixed-width junctions and preservation of baked elevation when manually rebuilding a generated road with Shift are not implemented yet.
- The road planner uses sampled curved candidate paths and rejects unsafe slopes/crossings; it is not a full terrain A* solver. It may skip isolated nodes, or reject the whole preview if both bridges cannot be linked. Very steep terrain can need a different seed, spacing or larger cut/fill budget.
- Road grade validation and sampled terrain clearance are not a replacement for Play Mode vehicle driving, physics and visual junction seam tests.
- A generated graph remains available for pathfinding during city streaming, while random NPC selection stays restricted to active roads. Verify transition corridors, NPC routing and cross-city GPS locally. No changes to quest data or transition triggers.
- No procedural buildings, nature, landmark, water, or optimization bake in this first version. Use the existing roadside tool only on selected generated roads with its global clearing disabled.
- The old Ankara/Istanbul Master Builder windows still exist; don't run them on the same scene. They are not yet migrated to the shared planner.
- Scene/terrain content was not read from LFS remotely. Actual tile count, orientation, roots, heightmap resolution and sea surface must be verified by the tool in Unity.
- Do not merge into main until the scene copy compiles and the generated roads, two bridges, Clear/Undo and save/reload are verified.

## Test checklist

- [ ] No compile errors after Unity imports the new files.
- [ ] Run Tools > Delivery Driver > City > Run Procedural Planner Smoke Test and confirm PASS in Console (temporary synthetic flat 28/24-tile scene, no scene assets written).
- [ ] Detect confirms 24 or 28 terrain objects, correct city sides and real sea gap.
- [ ] Preview does not change the scene or terrain assets.
- [ ] Ankara-only preview/generation preserves Kizilay.
- [ ] Roads have usable spline, asphalt and MeshCollider on Road layer.
- [ ] Road shoulders, slopes and intersection joins are visually sound; test with minivan.
- [ ] Two-city preview creates exactly two bridge links.
- [ ] Drive across both bridges and verify NPC/GPS behavior in both directions.
- [ ] Save/reopen scene copy; generated meshes and TerrainData still exist.
- [ ] Clear restores original TerrainData and preserves manual roads/buildings; Undo works.

### Bridge preview troubleshooting

The bridge planner searches independent approach lengths and perpendicular/diagonal corridors, with a grade-bounded elevation profile rather than a fixed flat deck. It preserves existing roads and protected districts, and reports rejection counts for protected areas, existing roads, missing terrain, width/bounds and grade/height. Check Sea surface Y against the water plane. If two bridges cannot be planned, Preview can still display city roads without bridges, explicitly marked as a fallback. Generate remains strict unless Connect with two bridges is unchecked. No bridge is silently omitted during generation.
