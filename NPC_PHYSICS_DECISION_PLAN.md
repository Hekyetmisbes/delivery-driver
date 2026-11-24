# NPC lightweight physics and decision cadence — discussion draft

Owner direction: arcade driving realism is sufficient for NPCs; use the player CarController as reference, avoid unnecessary physics detail. Existing native WheelCollider remains the baseline. No GPS/A*/RTAA* changes.

## Implemented physics boundary
- Dynamic bodies: apply steering smoothing, live-speed motor/brake and bounded stability each physics tick (project fixed timestep 0.02 s, 50 Hz). WheelCollider handles native suspension; do not manually simulate spring/damper every tick. Anti-roll every 3 actual ticks; artificial tilt correction off by default, optional in Inspector.
- Decision ticks: route advancement, lookahead, nearby/obstacle checks, reverse and lane state, effective target speed; reuse last steering/speed targets between decisions. Kinematic bodies do not receive continuous motor/stability forces.
- Fresh decision after invalidation (spawn/disable/reposition/kinematic/invalid path) or current urgent-state bypass. Stop flags override cached acceleration; live steering-dependent speed cap limits cached speed.
- Tradeoff: physics actuation is more frequent for throttled dynamic bodies than before, so script CPU and effective forces can increase. WheelCollider native solver is still a potentially significant cost. Measure rather than assume FPS gains. No heavy custom suspension/tire simulation is planned.

## Current decision timing (source defaults, not verified Inspector)
- Distance <50 m: decision each tick; 50–150 m: each 2; 150–300 m: each 4; 300–500 m: each 8; >=500 m: each 16. Urgent dynamic state bypasses outer gate.
- Base inner periods measured in executed decision ticks: obstacles/turn/prediction 3, nearby overlap 2, offroad 10, weather 30. Adaptive multiplier 1/2/3/4 based on distance/risk, so nested throttling can cause long gaps. At 150–300 m the obstacle period can be 4*3*2*0.02=0.48 s. At 300–500 m it can be 8*3*3*0.02=1.44 s. >=500 m default optimizer sets kinematic beyond threshold; actual behavior depends on inspector and state transitions.
- Current predictive limiter and imminent risk are reset on executed decision ticks, even when obstacle scan is not due. This is a pre-existing lifetime issue: design an explicit freshness contract before changing behavior.

## Proposal for owner approval — not implemented
1. Separate safety sensing, path/steering decisions and slow environment updates; schedule on real physics ticks, not multiplied nested decision counters.
2. Near player and urgent traffic (closing gap, low TTC, collision, intersection, reverse, lane change): safety sensing every 1–2 ticks (20–40 ms); normal nearby safety 2–3 ticks (40–60 ms); path steering targets 1–3 ticks. Preserve safety when player is fast or approaching from behind.
3. Mid-distance free-flow: safety/path updates around 4–5 ticks (80–100 ms); far dynamic low-risk: around 8–10 ticks (160–200 ms), only when reaction distance is sufficient. These are starting values to validate, not hard commitments. Speed alone is not danger; use relative closing speed, braking distance, visibility/interaction, and distance to junction/obstacle. Never make a known hazard wait on distance LOD.
4. Compute a conservative maximum sensing delay from available free distance and closing speed: delay <= max(0, (freeDistance - stoppingDistance - safetyBuffer) / max(closingSpeed, epsilon)). Clamp to the above timing limits. Unknown or stale sensor state uses conservative speed/braking and immediate refresh, not assumed clear road. Preserve hazard until new valid evidence clears it.
5. Stagger low-priority work across NPCs; reserve budget for urgent sensing. No deferred collision braking or stale commands solely to meet a frame budget.
6. For distant traffic, discuss whether to keep frozen/despawned bodies or use lightweight waypoint-only kinematic travel. Do not automatically replace near-player WheelCollider driving or make active dynamic cars teleport. Restore valid pose, speed and decision state before re-entering dynamic simulation.
7. Keep existing road connectivity, right-hand traffic, following, reverse recovery and lane priorities until separately approved. No new planner/state machine yet.

## Validation gates
Compare identical scenes and NPC seeds at 30/60/120 FPS and fixed 50 Hz physics, 20/50/100 active NPCs as feasible. Record main-thread decision and physics time, Physics.Simulate/WheelCollider cost, casts, GC allocations, missed stops, collision rate, rollovers, lane oscillation, recovery, high-speed player approach and distance-tier transitions. Verify slope/curb, sharp turns, reversing, parked obstacle, pooled spawn and kinematic-to-dynamic activation. Test with optional self-leveling enabled if necessary. Unity build, actual scene Inspector values and profiling are not yet verified.
