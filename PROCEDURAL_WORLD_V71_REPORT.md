# Delivery Driver — V7.1 staged optimization

## Why
Actual V6: Ankara 12/12, Istanbul 12/12; 2871 retained, 10 detached, four existing-road proposals, two bridges, 1242 m separation, sampled cut/fill 2.84/1.49 m, peak 21.9 m, extra grading 31.8%.
Raw V7: Ankara 4/12, 1011 detached, 152 retained Ankara segments; cut/fill 0/5.82 m, peak 19.1 m, extra grading 43.4%. Raw V7 remains a diagnostic mode, not the default.
Sea Surface Y -2E-05 means -0.00002 m (effectively zero); it is not a parsing error. Keep sea Y unchanged when comparing layouts.

## Workflow
Default staged mode generates a same-seed V6 base (raw V6 as in V7 comparator, guarded V6 if raw fails or lacks coverage). It does NOT rebuild an independent strict V7 street network. Stage 0 is preserved; each stage works on a separate clone. Acceptance requires preservation of retained edge topology, coverage, bridge connectivity, existing link paths, and no material regression of sampled cut/fill, peak or extra grading. Rejected candidates remain available.

1. Shared junction/profile solver: each junction is a shared height variable; interior samples have their own variables. Center/shoulder and midpoint terrain budgets plus incident grade limits constrain updates. Bridge endpoints, authored link endpoints and detached shared endpoints stay fixed. Alternating coordinate sweeps minimize sampled absolute deviation. This is a bounded local iterative optimizer on the shared graph, not a certified global optimum.
2. Balanced earthworks: stronger penalties above normal and preferred grading limits, symmetric cut/fill objective, acceptance also considers imbalance. No physical terrain modifications or increased hard allowance.
3. Detached component repair: whole component validation against retained graph, bounded direct or terrain-routed connectors, promote only when connected. Roll back if global terrain or network checks regress. Purple diagnostics remain if infeasible.
4. Geometry refinement: reroute radius-flagged roads with fixed endpoints and cross/angle/grade checks. Existing violations remain if no safe improvement is found. No pruning of original edges, including short stubs.
5. Bridge refinement: test alternative curved land approaches at fixed bridge gates; retain only greater envelope separation without conflict. Existing coastal spline endpoints are audited. This does NOT relocate bridge gates or authorize crossing protected Kizilay: an actual new corridor along authored roads requires separate graph redesign. 65% separation is not guaranteed.
6. Urban screening: regenerate prefab-sized lots and park access on accepted roads using V7's model size and sampled access logic. Not a built driveway or pedestrian network.

## Stage history
Report: before/candidate metrics, elapsed seconds, acceptance/rejection reason and stage diagnostic notes. Stage comparison displays baseline and accepted/rejected snapshots read-only. Return to final optimized plan restores final preview. By default JSON snapshots and final.json are written under Library/ProceduralWorldCheckpoints; disable if desired. Intermediate snapshots are view-only; final snapshot may be refined only on matching fingerprint. Older V7 JSON can be opened read-only, not automatically used as optimization input. Baseline is regenerated, not loaded from the user's local JSON. Intermediate parcel/park metrics may be historical until stage 6.

Mandatory preview checks: requested city coverage, requested bridge graph, configured Ankara authored-link count when protected, resolved manual anchors, no detached roads, zero radius flags, sampled grade validation and successful urban screening. This is not final bake certification. Generate remains disabled. Short stubs and 65% bridge spacing remain targets, not mandatory checks.

## First test
- Pull feature/procedural-world-generator-mvp. Verify Unity compiles and reopen Procedural World Generator.
- Detect terrain; retain seed 4281 and prior V6 values. Set Sea surface Y to -0.00002 or use Detect's original reference. Grade 10%, normal cut/fill 6 m, district limit 30 m, preferred cap 15 m. Enable V7 quality design and V7.1 staged optimization. Profile sweeps 16. Legacy V7 guard setting is ignored.
- Preview Layout once. Copy preview report. Inspect baseline vs final and rejected stages. Check retained edges >= baseline, 12/12 each, four Kizilay proposals, two bridges; compare sampled mean/peak/extra grading and detached count.
- Save final plan. Inspect checkpoints in Library. Repeat unchanged settings for cache test. Do not change Sea Y during comparison.
- Run Procedural Planner Smoke Test in disposable editor session. Added staged flat-city baseline preservation and unchanged terrain checks; legacy V7 test remains with staged mode disabled.

## Validation limits
Source integration checks only; Unity compilation and smoke tests cannot be executed here. No measured V7.1 results, performance claims or guarantee of solving all detached roads. Repeated cloning, solver sweeps and checkpoint I/O may take time and memory. Final road mesh/terrain bake, true bridge relocation using existing road corridor, intersection/traffic driving tests and building placement remain pending. No EasyRoads dependency added.
