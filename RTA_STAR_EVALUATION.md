# RTAA* navigation feasibility and deferred implementation plan — Delivery Driver 2.0

**Status: feasibility only; implementation deferred by project owner.** The current cleanup phase must not replace A*, enable RTAA*, or change GPS/minimap behavior. The existing filename remains stable for links.

## Recommendation

Retain the complete-route incremental A* planner as the production GPS/minimap backend. Investigate Real-Time A* (RTA*), Real-Time Adaptive A* (RTAA*) and Time-Bounded A* (TBA*) as experimental planners, not interchangeable names or a guaranteed upgrade.

Korf's RTA* bounds planning effort before each action using local lookahead. The second-best backed-up value is used in classic RTA*; LRTA* differs in its learning rule. Local optimal decisions do not guarantee globally shortest routes, and directed dead ends can invalidate reachability assumptions. RTAA* and LSS-LRTA* use bounded local A* searches with heuristic updates. TBA* retains a global A* search while allowing execution before a complete plan. Compare with the actual baseline, not an artificially blocking implementation.

## Repository evidence

- NavigationService already uses a coroutine and IncrementalPathSearchSession; the old private synchronous route path had no call sites and has been removed.
- Existing pathfinder works with a directed waypoint graph, lane/alignment/turn penalties and guarded spatial transfer edges. Its quantized grid Manhattan heuristic and first-success candidate pair do not provide a general proof of globally optimal cost. Keep the cost model consistent across experiments; use a zero-heuristic reference on a fixed pair if optimality is being evaluated.
- NPC agents already implement local obstacle handling, path following, lane changes and recovery, not a full A* search every physics tick. RTA* is relevant only where an agent actually has a destination/replanning problem; it will not automatically accelerate those existing physics/AI operations.
- Player-facing route, delivery distance/ETA and minimap require a complete, stable route. A local tentative route must not be published as a verified complete route or change quest reward/time calculations.

## Implemented baseline

Remove unreachable duplicate search implementations (309 source lines before budget/diagnostics additions). Keep public FindPath and incremental session. Add a configurable 1.5 ms cooperative per-Step time budget alongside the existing node limit. Time is checked between node expansions. Projection, index creation, candidate setup, single expansion and path reconstruction can exceed the budget. Multiple slices in a frame can also exceed it; this is not a hard real-time guarantee.

NavigationService exposes last completed request/attempt diagnostics: expanded nodes, queue pops, maximum individual setup or search slice, summed setup/search compute time and wall time (including yields). Aborted sessions may have incomplete diagnostics; retries are separate requests. A route test compares tiny slices and unlimited time on the same directed graph.

## Future Adaptive GPS & Navigation Overhaul (not authorized for implementation yet)

The reported gameplay problem is stale or nonsensical minimap guidance when the vehicle moves faster than route refresh/replanning. RTAA* is the intended research candidate, not classic RTA*. Faster search alone cannot fix stale route publication or map matching.

Source-code findings (defaults, not verified scene Inspector values):
- NavigationService refreshes every 0.2 s, publishes after 4 m movement, has a 1.5 s reroute cooldown, 0.75 s minimum async interval and 14/26 m off-route thresholds. At 120 km/h the vehicle travels about 6.7 m per refresh and 50 m during the cooldown.
- RoadGraph.ProjectPointOnRoad scans every road edge at search setup for both start and goal. The active A* search itself uses a spatial index and does not necessarily visit the entire graph each frame.
- ProjectOnRoute chooses the globally closest segment of the current polyline without a persistent progress cursor or direction score. Parallel roads, crossings and overlapping routes can cause incorrect projection.
- BuildRouteAsync captures start/end before search and can publish the completed route using that old start. PublishProjected prepends the vehicle-to-projection connector; it can show a backward or off-road line.
- MinimapUI simplifies the polyline in screen space and may remove important turns. CompassUI computes a direct bearing to the objective rather than the road route. Distinguish these from search performance when reproducing the reported issue.

Future implementation order (explicit approval required):
1. Index road edges spatially, with a complete-query fallback and direction/elevation/continuity checks. Keep a route progress cursor and handle true reversal or teleport.
2. Validate request identity, graph revision and current vehicle position before publishing; discard or rebase obsolete results. Show no false connector or unverified complete route.
3. Separate per-frame vehicle/marker tracking, speed-aware local route tracking, event-driven rerouting and rendering. Use a bounded search budget; select lookahead distance based on vehicle speed and upcoming junctions.
4. Prototype RTAA* in a development-only harness on the same directed road graph and edge costs. Use bounded local A* lookahead, learn h(s) = f(frontier) - g(s) for expanded states, and reset/invalidate learned values when goal or relevant graph/cost assumptions change. Use a consistent admissible initial heuristic; the existing quantized Manhattan heuristic must not be assumed consistent. Do not confuse local guidance with a complete validated route.
5. Compare current incremental A* against RTAA*, retaining A* for complete route/ETA and a safe fallback. Only adopt RTAA* if measured improvements justify the tradeoffs; no algorithm switch during cleanup.
6. Test high-speed traversal, missed junctions, U-turns, parallel/elevated roads, teleport, stale-result rejection, no-path, and route visualization. Record route freshness, wrong-direction guidance, render geometry, search latency, CPU/GC and route cost separately.

Primary RTAA* reference: Sven Koenig and Maxim Likhachev, Real-Time Adaptive A* (AAMAS 2006), https://www.cs.cmu.edu/~maxim/files/rtaa_aamas06_abs.html

## Experiment design (not executed)

1. Capture identical Game graph snapshots, start/goal pairs and transfer settings. Start with 30-50 fixed reachable and unreachable pairs, including one-way roads, intersections, disconnected islands, opposite lanes, detours and repeat queries.
2. Use the same graph neighbor/cost interface and direction rules for all planners. Add optional experimental RTA*/RTAA* or TBA* behind a development-only setting. Preserve the global A* fallback; do not let experimental local moves enter production until validated.
3. Compare first safe action latency, full route latency, p50/p95/p99 compute slices, nodes expanded/revisited, total CPU across journey, managed allocations and peak memory. Compare actual traveled distance and weighted path cost against the same global reference, with route failures, oscillations, dead ends and illegal transfers counted.
4. Replay at 30/60/120 render FPS and a fixed physics timestep. Evaluate NPCs with a real destination and controlled dynamic graph edge changes separately from player GPS.
5. Proposed gates (not measured): zero illegal directed transitions; no regression in delivery success or validated GPS route availability; an experimental planner should materially reduce p95 frame work without unacceptable route detour or oscillation. Measure before choosing a numerical detour threshold; disable the experiment if safety or correctness regresses.

## Sources

- Richard E. Korf, Real-time heuristic search (1990), https://doi.org/10.1016/0004-3702(90)90054-4
- Korf, Real-Time Heuristic Search: New Results (1988), https://www.aiinternational.org/Library/AAAI/1988/aaai88-025.php
- Björnsson, Bulitko, Sturtevant, TBA*: Time-Bounded A* (2009), https://ijcai.org/proceedings/2009/79
- Does Fast Beat Thorough? Comparing RTAA* and LSS-LRTA*, https://cdn.aaai.org/ojs/18465/18465-77-21981-1-2-20210717.pdf

The real Game scene is Git LFS material; Unity Editor and Profiler runs are not available in the remote authoring environment. No claim is made that RTA* or the new time budget delivers a measured FPS improvement.
