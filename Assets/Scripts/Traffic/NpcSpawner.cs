using System.Collections.Generic;
using UnityEngine;
using DeliveryDriver.Company;
using DeliveryDriver.Optimization;
using DeliveryDriver.Vehicle;
using TrafficSystem.Spline;

namespace TrafficSystem
{
    /// <summary>
    /// Spawns NPC vehicles on the road network with proper spacing
    /// Supports object pooling for performance
    /// </summary>
    public class NpcSpawner : MonoBehaviour
    {
        [Header("References")]
        private RoadNetworkManager networkManager;

        [Header("Spawn Settings")]
        [Tooltip("NPC vehicle prefabs to spawn (should have NpcCarAgent + NpcRecovery)")]
        [SerializeField] private GameObject[] npcVehiclePrefabs;
        [Tooltip("Number of NPCs to spawn")]
        [SerializeField] private int spawnCount = 30;
        [Tooltip("Minimum distance between spawned vehicles (meters)")]
        [SerializeField] private float minimumSpawnSpacing = 20f;
        [Tooltip("Check radius for existing vehicles/obstacles at spawn point (meters)")]
        [SerializeField] private float spawnCheckRadius = 8f;
        [Tooltip("Small radius used to reject static obstacles directly overlapping the spawn pose")]
        [SerializeField] private float staticSpawnObstacleRadius = 2.4f;
        [Tooltip("Layer mask for checking obstacles at spawn point")]
        [SerializeField] private LayerMask spawnObstacleCheckMask = ~0;
        [Tooltip("Spawn vehicles on Start()")]
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Initial delay before starting spawn")]
        [SerializeField] private float initialSpawnDelay = 0.5f;
        [Tooltip("Delay between each NPC spawn (seconds)")]
        [SerializeField] private float spawnDelay = 0.8f;
        [Tooltip("Extra clearance multiplier around each spawned vehicle")]
        [SerializeField] private float spawnClearanceMultiplier = 1.15f;
        [Tooltip("Raycast mask for grounding spawn position")]
        [SerializeField] private LayerMask spawnGroundMask = ~0;
        [Tooltip("Raycast height above waypoint for grounding")]
        [SerializeField] private float spawnRaycastHeight = 5f;
        [Tooltip("Raycast distance downward for grounding")]
        [SerializeField] private float spawnRaycastDistance = 10f;
        [Tooltip("Reject spawn points that are currently visible in the player's camera")]
        [SerializeField] private bool avoidSpawningInCameraView = true;
        [Tooltip("Viewport padding used when rejecting visible spawn points")]
        [SerializeField, Range(0f, 0.35f)] private float cameraSpawnViewportMargin = 0.08f;
        [Tooltip("Only reject visible spawn points within this camera distance")]
        [SerializeField] private float cameraVisibleSpawnRejectDistance = 160f;

        [Header("Pooling")]
        [Tooltip("Enable object pooling (disable/enable instead of destroy/instantiate)")]
        [SerializeField] private bool enablePooling = true;
        [Tooltip("Warm the NPC pool gradually after scene start. Disabled by default to avoid startup freezes.")]
        [SerializeField] private bool warmPoolOnStart = false;
        [Tooltip("Delay before optional gradual pool warmup starts")]
        [SerializeField] private float poolWarmupDelay = 1.5f;
        [Tooltip("How many NPC prefabs may be instantiated per warmup frame")]
        [SerializeField] private int poolWarmupPerFrame = 1;
        [Tooltip("Pre-pool extra vehicles")]
        [SerializeField] private int poolExtraCount = 5;

        public enum TrafficDensityProfile
        {
            MobileBalanced60Fps, // 16-18 vehicles, 45m-120m ring, 150m despawn (Optimized for Mobile 60 FPS)
            MobileDense,         // 22 vehicles, 40m-130m ring, 160m despawn
            PcStandard,          // 32 vehicles, 45m-160m ring, 200m despawn
            PcUltraDense         // 48 vehicles, 40m-200m ring, 240m despawn
        }

        [Header("Dynamic Traffic Ring & Profiles")]
        [Tooltip("Active profile tuning vehicle count, spawn ring, and despawn range")]
        [SerializeField] private TrafficDensityProfile densityProfile = TrafficDensityProfile.MobileBalanced60Fps;
        [Tooltip("Keep traffic centered around the player instead of filling the whole map at once")]
        [SerializeField] private bool keepTrafficAroundPlayer = true;
        [Tooltip("Desired active NPC count around the player")]
        [SerializeField] private int targetActiveNpcCount = 18;
        [Tooltip("Do not spawn traffic too close to the player")]
        [SerializeField] private float playerSpawnMinDistance = 45f;
        [Tooltip("Maximum distance from the player where new traffic can spawn")]
        [SerializeField] private float playerSpawnMaxDistance = 120f;
        [Tooltip("NPCs farther than this are recycled back into the pool")]
        [SerializeField] private float playerDespawnDistance = 160f;
        [Tooltip("How often to try filling missing nearby traffic")]
        [SerializeField] private float dynamicSpawnInterval = 0.35f;
        [Tooltip("How often to recycle distant traffic")]
        [SerializeField] private float dynamicDespawnInterval = 1.0f;
        [Tooltip("Max spawn attempts made in one maintenance tick")]
        [SerializeField] private int maxSpawnAttemptsPerTick = 8;
        [Tooltip("Max successful spawns performed in one maintenance tick")]
        [SerializeField] private int maxSpawnsPerTick = 2;
        [Tooltip("Resolve the player transform automatically when missing")]
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private Transform playerTransform;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        public TrafficDensityProfile CurrentProfile => densityProfile;

        public void ApplyDensityProfile(TrafficDensityProfile profile)
        {
            densityProfile = profile;
            switch (profile)
            {
                case TrafficDensityProfile.MobileBalanced60Fps:
                    targetActiveNpcCount = 18;
                    playerSpawnMinDistance = 45f;
                    playerSpawnMaxDistance = 120f;
                    playerDespawnDistance = 160f;
                    minimumSpawnSpacing = 20f;
                    break;
                case TrafficDensityProfile.MobileDense:
                    targetActiveNpcCount = 22;
                    playerSpawnMinDistance = 40f;
                    playerSpawnMaxDistance = 130f;
                    playerDespawnDistance = 170f;
                    minimumSpawnSpacing = 18f;
                    break;
                case TrafficDensityProfile.PcStandard:
                    targetActiveNpcCount = 32;
                    playerSpawnMinDistance = 45f;
                    playerSpawnMaxDistance = 160f;
                    playerDespawnDistance = 210f;
                    minimumSpawnSpacing = 16f;
                    break;
                case TrafficDensityProfile.PcUltraDense:
                    targetActiveNpcCount = 48;
                    playerSpawnMinDistance = 40f;
                    playerSpawnMaxDistance = 200f;
                    playerDespawnDistance = 250f;
                    minimumSpawnSpacing = 14f;
                    break;
            }
        }

        // Runtime data
        private List<GameObject> activeNpcs = new List<GameObject>();
        private List<GameObject> pooledNpcs = new List<GameObject>();
        private HashSet<GameObject> pooledNpcSet = new HashSet<GameObject>();
        private Transform npcContainer;
        private Coroutine spawnCoroutine;
        private Coroutine poolWarmupCoroutine;

        public bool HasPendingOrActiveSpawn => spawnCoroutine != null || activeNpcs.Count > 0;
        public int ActiveNpcCount => activeNpcs.Count;
        public int PooledNpcCount => pooledNpcs.Count;

        private float nextDynamicSpawnTime;
        private float nextDynamicDespawnTime;
        private float nextPlayerResolveTime;

        // Cached allocations to avoid GC pressure
        private const float HiddenPoolDepth = -1000f;
        private static readonly Collider[] spawnOverlapBuffer = new Collider[32];
        private static readonly RaycastHit[] spawnGroundHitBuffer = new RaycastHit[16];
        private WaitForSeconds cachedSpawnDelay;

        private void Start()
        {
            networkManager = RoadNetworkManager.Instance;

            // Create container for organization
            npcContainer = new GameObject("NPC Vehicles").transform;
            npcContainer.SetParent(transform);

            EnsureTrafficCommunicationSystem();

            if (enablePooling && warmPoolOnStart)
            {
                poolWarmupCoroutine = StartCoroutine(WarmPoolGradually(GetDesiredPoolSize()));
            }

            if (spawnOnStart)
            {
                if (keepTrafficAroundPlayer)
                {
                    float startDelay = Mathf.Max(0f, initialSpawnDelay);
                    nextDynamicSpawnTime = Time.time + startDelay;
                    nextDynamicDespawnTime = Time.time + startDelay;
                }
                else
                {
                    SpawnNpcsDeferred(0f);
                }
            }
        }

        private void Update()
        {
            if (!TryResolveReadyNetwork()) return;

            if (!spawnOnStart || !keepTrafficAroundPlayer) return;

            if (!TryResolvePlayerTransform(out Transform player)) return;

            if (Time.time >= nextDynamicDespawnTime)
            {
                nextDynamicDespawnTime = Time.time + Mathf.Max(0.25f, dynamicDespawnInterval);
                RecycleFarTraffic(player.position);
            }

            if (Time.time >= nextDynamicSpawnTime)
            {
                nextDynamicSpawnTime = Time.time + Mathf.Max(0.1f, dynamicSpawnInterval);
                MaintainTrafficRing(player.position);
            }
        }

        private void EnsureTrafficCommunicationSystem()
        {
            if (TrafficCommunicationSystem.Instance == null)
            {
                GameObject commsObj = new GameObject("TrafficCommunicationSystem");
                commsObj.AddComponent<TrafficCommunicationSystem>();
            }

            if (WeatherManager.Instance == null)
            {
                GameObject weatherObj = new GameObject("WeatherManager");
                weatherObj.AddComponent<WeatherManager>();
            }
        }

        private int GetDesiredPoolSize()
        {
            return keepTrafficAroundPlayer ? targetActiveNpcCount + poolExtraCount : spawnCount + poolExtraCount;
        }

        private bool TryResolvePlayerTransform(out Transform player)
        {
            if (playerTransform != null)
            {
                player = playerTransform;
                return true;
            }

            if (!autoFindPlayer || Time.time < nextPlayerResolveTime)
            {
                player = null;
                return false;
            }

            nextPlayerResolveTime = Time.time + 1.0f;

            var car = FindFirstObjectByType<CarController>();
            if (car != null)
            {
                playerTransform = car.transform;
                player = playerTransform;
                return true;
            }

            var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerTransform = taggedPlayer.transform;
                player = playerTransform;
                return true;
            }

            player = null;
            return false;
        }

        private void RecycleFarTraffic(Vector3 playerPosition)
        {
            if (activeNpcs.Count == 0) return;

            float despawnDistSqr = playerDespawnDistance * playerDespawnDistance;

            for (int i = activeNpcs.Count - 1; i >= 0; i--)
            {
                GameObject npc = activeNpcs[i];
                if (npc == null)
                {
                    activeNpcs.RemoveAt(i);
                    continue;
                }

                float distSqr = (npc.transform.position - playerPosition).sqrMagnitude;
                if (distSqr > despawnDistSqr)
                {
                    activeNpcs.RemoveAt(i);
                    ReturnNpcToPool(npc);
                }
            }
        }

        private void MaintainTrafficRing(Vector3 playerPosition)
        {
            if (npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0) return;

            int currentActive = activeNpcs.Count;
            if (currentActive >= targetActiveNpcCount) return;

            int neededSpawns = targetActiveNpcCount - currentActive;
            int spawnsThisTick = 0;
            int attempts = 0;

            while (spawnsThisTick < neededSpawns && spawnsThisTick < maxSpawnsPerTick && attempts < maxSpawnAttemptsPerTick)
            {
                attempts++;

                SplineRoad road = GetRoadForSpawn();
                if (road == null) continue;

                float t = Random.value;
                bool isForward = Random.value > 0.5f;

                if (!TryGetSpawnPoseOnRoad(road, t, isForward, out Vector3 spawnPos, out Vector3 forward))
                {
                    continue;
                }

                float distToPlayerSqr = (spawnPos - playerPosition).sqrMagnitude;
                float minSq = playerSpawnMinDistance * playerSpawnMinDistance;
                float maxSq = playerSpawnMaxDistance * playerSpawnMaxDistance;

                if (distToPlayerSqr < minSq || distToPlayerSqr > maxSq) continue;

                if (!TryGetGroundedSpawnPosition(spawnPos, out Vector3 groundSpawnPos))
                {
                    continue;
                }

                if (IsSpawnVisibleToPlayer(groundSpawnPos)) continue;

                if (!IsSpacingValid(groundSpawnPos)) continue;

                if (!IsSpawnPositionClear(groundSpawnPos, GetSpawnClearanceRadius())) continue;

                GameObject npc = GetOrCreateNpc();
                if (npc == null) continue;

                NpcCarAgent carAgent = npc.GetComponent<NpcCarAgent>();
                Vector3 finalSpawnPos = GetVehicleRootSpawnPosition(npc, groundSpawnPos, carAgent);
                Quaternion spawnRotation = Quaternion.LookRotation(forward);

                SetNpcPose(npc, finalSpawnPos, spawnRotation);

                if (carAgent != null)
                {
                    carAgent.InitializeOnRoad(road, t, isForward, false);
                }

                npc.SetActive(true);
                ApplySpawnVelocity(npc, forward);

                NpcRecovery recovery = npc.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.ConfigureForRuntime(false);
                    recovery.Initialize();
                }

                activeNpcs.Add(npc);
                spawnsThisTick++;
            }
        }

        private bool IsSpacingValid(Vector3 candidatePosition)
        {
            float minSq = minimumSpawnSpacing * minimumSpawnSpacing;
            for (int i = 0; i < activeNpcs.Count; i++)
            {
                if (activeNpcs[i] != null)
                {
                    float distSqr = (candidatePosition - activeNpcs[i].transform.position).sqrMagnitude;
                    if (distSqr < minSq) return false;
                }
            }
            return true;
        }

        private bool TryGetGroundedSpawnPosition(Vector3 basePosition, out Vector3 groundedPosition)
        {
            groundedPosition = basePosition;

            Vector3 origin = basePosition + Vector3.up * spawnRaycastHeight;
            float maxDistance = spawnRaycastHeight + spawnRaycastDistance;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, spawnGroundHitBuffer, maxDistance, spawnGroundMask, QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
            {
                return IsFinite(basePosition);
            }

            int bestRoadHitIndex = -1;
            int bestFallbackHitIndex = -1;
            float bestRoadHitDistance = float.MaxValue;
            float bestFallbackHitDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = spawnGroundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger || IsNpcVehicleCollider(hitCollider))
                {
                    continue;
                }

                if (IsRoadSurfaceCollider(hitCollider) || hitCollider is TerrainCollider)
                {
                    if (hit.distance < bestRoadHitDistance)
                    {
                        bestRoadHitDistance = hit.distance;
                        bestRoadHitIndex = i;
                    }
                    continue;
                }

                if (hit.distance < bestFallbackHitDistance)
                {
                    bestFallbackHitDistance = hit.distance;
                    bestFallbackHitIndex = i;
                }
            }

            int bestHitIndex = bestRoadHitIndex >= 0 ? bestRoadHitIndex : bestFallbackHitIndex;
            if (bestHitIndex < 0)
            {
                return IsFinite(basePosition);
            }

            groundedPosition = spawnGroundHitBuffer[bestHitIndex].point;
            return IsFinite(groundedPosition);
        }

        private float GetSpawnClearanceRadius()
        {
            return spawnCheckRadius * spawnClearanceMultiplier;
        }

        private bool IsSpawnPositionClear(Vector3 position, float radius)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(position, radius, spawnOverlapBuffer, spawnObstacleCheckMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = spawnOverlapBuffer[i];
                if (col == null) continue;
                if (col.isTrigger) continue;
                if (col is TerrainCollider || IsRoadSurfaceCollider(col)) continue;

                if (GetSafeColliderDistance(position, col) < staticSpawnObstacleRadius)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsRoadSurfaceCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.gameObject.layer == LayerMask.NameToLayer("Road"))
            {
                return true;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                string objectName = current.name;
                if (!string.IsNullOrEmpty(objectName) &&
                    objectName.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsNpcVehicleCollider(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<NpcCarAgent>() != null;
        }

        private static float GetSafeColliderDistance(Vector3 position, Collider collider)
        {
            if (collider == null)
            {
                return float.MaxValue;
            }

            if (collider is MeshCollider meshCollider && !meshCollider.convex)
            {
                return collider.bounds.SqrDistance(position) <= 0f
                    ? 0f
                    : Mathf.Sqrt(collider.bounds.SqrDistance(position));
            }

            return Vector3.Distance(position, collider.ClosestPoint(position));
        }

        private bool IsSpawnVisibleToPlayer(Vector3 spawnPosition)
        {
            if (!avoidSpawningInCameraView) return false;

            // 1. Forward View Check: Reject spawn if it's anywhere in front of the player within 90m
            Transform player = playerTransform;
            if (player == null && autoFindPlayer)
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null) player = taggedPlayer.transform;
            }

            if (player != null)
            {
                Vector3 toSpawn = spawnPosition - player.position;
                toSpawn.y = 0f;
                float dist = toSpawn.magnitude;

                if (dist < 90f && dist > 0.1f)
                {
                    Vector3 playerFwd = player.forward;
                    playerFwd.y = 0f;
                    if (playerFwd.sqrMagnitude > 0.01f)
                    {
                        float dot = Vector3.Dot(playerFwd.normalized, toSpawn.normalized);
                        // Forward arc ~150 degrees (dot > 0.15)
                        if (dot > 0.15f)
                        {
                            return true; // Directly in front of player - reject!
                        }
                    }
                }
            }

            // 2. Camera Viewport frustum check
            Camera cam = Camera.main;
            if (cam == null) return false;

            if (Vector3.Distance(cam.transform.position, spawnPosition) > cameraVisibleSpawnRejectDistance) return false;

            Vector3 viewportPos = cam.WorldToViewportPoint(spawnPosition);
            return viewportPos.z > 0 &&
                   viewportPos.x > -cameraSpawnViewportMargin && viewportPos.x < 1f + cameraSpawnViewportMargin &&
                   viewportPos.y > -cameraSpawnViewportMargin && viewportPos.y < 1f + cameraSpawnViewportMargin;
        }

        [ContextMenu("Spawn NPCs")]
        public void SpawnNpcs()
        {
            SpawnNpcsDeferred(0f);
        }

        public void SpawnNpcsDeferred(float additionalDelaySeconds)
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
            }

            spawnCoroutine = StartCoroutine(SpawnNpcsCoroutine(Mathf.Max(0f, additionalDelaySeconds)));
        }

        private System.Collections.IEnumerator SpawnNpcsCoroutine(float additionalDelaySeconds)
        {
            yield return new WaitForSeconds(initialSpawnDelay + additionalDelaySeconds);

            if (!TryResolveReadyNetwork())
            {
                spawnCoroutine = null;
                yield break;
            }

            if (npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0)
            {
                spawnCoroutine = null;
                yield break;
            }

            cachedSpawnDelay = new WaitForSeconds(spawnDelay);

            ClearAllNpcs(stopSpawnRoutine: false);

            if (enablePooling)
            {
                EnsurePoolCapacity(GetDesiredPoolSize());
            }

            int spawnedCount = 0;
            int attempts = 0;
            int maxAttempts = spawnCount * 30;

            while (spawnedCount < spawnCount && attempts < maxAttempts)
            {
                attempts++;

                SplineRoad road = GetRoadForSpawn();
                if (road == null) continue;

                float t = Random.value;
                bool isForward = Random.value > 0.5f;

                if (!TryGetSpawnPoseOnRoad(road, t, isForward, out Vector3 spawnPos, out Vector3 forward))
                {
                    continue;
                }

                if (!TryGetGroundedSpawnPosition(spawnPos, out Vector3 groundSpawnPos))
                {
                    continue;
                }

                if (IsSpawnVisibleToPlayer(groundSpawnPos))
                {
                    continue;
                }

                if (!IsSpacingValid(groundSpawnPos))
                {
                    continue;
                }

                if (!IsSpawnPositionClear(groundSpawnPos, GetSpawnClearanceRadius()))
                {
                    continue;
                }

                GameObject npcVehicle = GetOrCreateNpc();
                if (npcVehicle == null) continue;

                NpcCarAgent carAgent = npcVehicle.GetComponent<NpcCarAgent>();
                Vector3 finalSpawnPos = GetVehicleRootSpawnPosition(npcVehicle, groundSpawnPos, carAgent);
                Quaternion spawnRotation = Quaternion.LookRotation(forward);

                SetNpcPose(npcVehicle, finalSpawnPos, spawnRotation);

                if (carAgent != null)
                {
                    carAgent.InitializeOnRoad(road, t, isForward, false);
                }

                if (!npcVehicle.activeSelf) npcVehicle.SetActive(true);
                ApplySpawnVelocity(npcVehicle, forward);

                NpcRecovery recovery = npcVehicle.GetComponent<NpcRecovery>();
                if (recovery != null)
                {
                    recovery.ConfigureForRuntime(false);
                    recovery.Initialize();
                }

                activeNpcs.Add(npcVehicle);
                spawnedCount++;

                yield return cachedSpawnDelay;
            }

            spawnCoroutine = null;
        }

        public void ClearAllNpcs(bool stopSpawnRoutine = true)
        {
            if (stopSpawnRoutine && spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            if (activeNpcs != null)
            {
                for (int i = 0; i < activeNpcs.Count; i++)
                {
                    ReturnNpcToPool(activeNpcs[i]);
                }
                activeNpcs.Clear();
            }
        }

        public void SetNpcPrefabs(GameObject[] prefabs)
        {
            npcVehiclePrefabs = prefabs;
        }

        public GameObject[] GetNpcPrefabs() => npcVehiclePrefabs;

        private GameObject GetOrCreateNpc()
        {
            GameObject npc = null;

            if (enablePooling && pooledNpcs.Count > 0)
            {
                npc = pooledNpcs[pooledNpcs.Count - 1];
                pooledNpcs.RemoveAt(pooledNpcs.Count - 1);
                pooledNpcSet.Remove(npc);

                if (npc == null) return GetOrCreateNpc();
            }
            else
            {
                int prefabIndex = Random.Range(0, npcVehiclePrefabs.Length);
                GameObject prefab = npcVehiclePrefabs[prefabIndex];

                if (prefab != null)
                {
                    npc = Instantiate(prefab, GetHiddenPoolPosition(), Quaternion.identity, npcContainer);
                    npc.name = $"{prefab.name}_{System.Guid.NewGuid().ToString().Substring(0, 5)}";
                    npc.SetActive(false);
                }
            }

            return npc;
        }

        private void ReturnNpcToPool(GameObject npc)
        {
            if (npc == null) return;

            if (enablePooling)
            {
                ResetNpcPhysics(npc);
                npc.SetActive(false);
                npc.transform.position = GetHiddenPoolPosition();
                npc.transform.rotation = Quaternion.identity;
                if (!pooledNpcSet.Contains(npc))
                {
                    pooledNpcs.Add(npc);
                    pooledNpcSet.Add(npc);
                }
            }
            else
            {
                Destroy(npc);
            }
        }

        private void EnsurePoolCapacity(int capacity)
        {
            if (!enablePooling || npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0) return;

            int currentTotal = activeNpcs.Count + pooledNpcs.Count;
            int needed = capacity - currentTotal;

            for (int i = 0; i < needed; i++)
            {
                int prefabIndex = i % npcVehiclePrefabs.Length;
                GameObject prefab = npcVehiclePrefabs[prefabIndex];

                if (prefab != null)
                {
                    GameObject npc = Instantiate(prefab, GetHiddenPoolPosition(), Quaternion.identity, npcContainer);
                    npc.name = $"{prefab.name}_{System.Guid.NewGuid().ToString().Substring(0, 5)}";
                    npc.SetActive(false);
                    pooledNpcs.Add(npc);
                    pooledNpcSet.Add(npc);
                }
            }
        }

        private System.Collections.IEnumerator WarmPoolGradually(int capacity)
        {
            yield return new WaitForSeconds(poolWarmupDelay);

            if (!enablePooling || npcVehiclePrefabs == null || npcVehiclePrefabs.Length == 0) yield break;

            int currentTotal = activeNpcs.Count + pooledNpcs.Count;
            int needed = capacity - currentTotal;

            for (int i = 0; i < needed; i++)
            {
                int prefabIndex = i % npcVehiclePrefabs.Length;
                GameObject prefab = npcVehiclePrefabs[prefabIndex];

                if (prefab != null)
                {
                    GameObject npc = Instantiate(prefab, GetHiddenPoolPosition(), Quaternion.identity, npcContainer);
                    npc.name = $"{prefab.name}_{System.Guid.NewGuid().ToString().Substring(0, 5)}";
                    npc.SetActive(false);
                    pooledNpcs.Add(npc);
                    pooledNpcSet.Add(npc);
                }

                if ((i + 1) % Mathf.Max(1, poolWarmupPerFrame) == 0)
                {
                    yield return null;
                }
            }

            poolWarmupCoroutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!keepTrafficAroundPlayer || !showDebugInfo) return;

            Transform player = playerTransform;
            if (player == null)
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
                if (taggedPlayer != null) player = taggedPlayer.transform;
            }

            if (player != null)
            {
                Vector3 center = player.position;

                Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(center, playerSpawnMinDistance);

                Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
                Gizmos.DrawWireSphere(center, playerSpawnMaxDistance);

                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.DrawWireSphere(center, playerDespawnDistance);
            }
        }

        private bool TryResolveReadyNetwork()
        {
            if (networkManager == null)
            {
                networkManager = RoadNetworkManager.Instance;
            }

            if (networkManager == null)
            {
                networkManager = FindFirstObjectByType<RoadNetworkManager>();
            }

            if (networkManager == null)
            {
                return false;
            }

            if (networkManager.roads == null || networkManager.roads.Count == 0)
            {
                networkManager.RefreshNetwork();
            }

            return networkManager.roads != null && networkManager.roads.Count > 0;
        }

        private static Vector3 GetVehicleRootSpawnPosition(GameObject npc, Vector3 groundPosition, NpcCarAgent carAgent)
        {
            float clearanceOffset = (carAgent != null ? carAgent.GetGroundClearanceOffset() : 0.2f) + 0.35f;
            return groundPosition + Vector3.up * clearanceOffset;
        }

        private static void SetNpcPose(GameObject npc, Vector3 position, Quaternion rotation)
        {
            if (npc == null)
            {
                return;
            }

            Rigidbody rb = npc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
            }

            npc.transform.SetPositionAndRotation(position, rotation);
        }

        private static void ApplySpawnVelocity(GameObject npc, Vector3 forward)
        {
            Rigidbody rb = npc != null ? npc.GetComponent<Rigidbody>() : null;
            if (rb == null || rb.isKinematic)
            {
                return;
            }

            rb.linearVelocity = forward * Random.Range(8f, 12f);
            rb.angularVelocity = Vector3.zero;
        }

        private static void ResetNpcPhysics(GameObject npc)
        {
            Rigidbody rb = npc != null ? npc.GetComponent<Rigidbody>() : null;
            if (rb == null || rb.isKinematic)
            {
                return;
            }

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private static Vector3 GetHiddenPoolPosition()
        {
            return Vector3.down * Mathf.Abs(HiddenPoolDepth);
        }

        private static bool TryGetSpawnPoseOnRoad(SplineRoad road, float t, bool isForward, out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;

            if (road == null || !road.IsUsable)
            {
                return false;
            }

            position = road.GetLanePosition(t, isForward);
            forward = road.GetTangent(t);
            if (!isForward)
            {
                forward = -forward;
            }

            return IsFinite(position) &&
                   IsFinite(forward) &&
                   position.sqrMagnitude > 1f &&
                   forward.sqrMagnitude > 0.01f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private SplineRoad GetRoadForSpawn()
        {
            if (networkManager == null) return null;

            // Prioritize roads in the active sector and transition corridor
            if (DeliveryDriver.City.CityStreamingManager.Instance != null)
            {
                var sector = DeliveryDriver.City.CityStreamingManager.Instance.CurrentSector;
                string sectorName = sector.ToString();

                if (sector == DeliveryDriver.City.CitySectorType.HighwaysAndBridges)
                {
                    SplineRoad hwRoad = networkManager.GetRandomRoadInSector("Highways");
                    if (hwRoad != null && hwRoad.gameObject.activeInHierarchy) return hwRoad;
                }
                else
                {
                    SplineRoad sectorRoad = networkManager.GetRandomRoadInSector(sectorName);
                    if (sectorRoad != null && sectorRoad.gameObject.activeInHierarchy) return sectorRoad;
                }
            }

            // Fallback: pick any active usable road
            SplineRoad fallback = networkManager.GetRandomRoad();
            if (fallback != null && fallback.gameObject.activeInHierarchy) return fallback;

            return null;
        }
    }
}
