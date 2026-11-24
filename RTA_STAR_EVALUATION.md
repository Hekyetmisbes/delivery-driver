# RTA* navigation feasibility — Delivery Driver 2.0

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
