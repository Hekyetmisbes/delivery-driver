using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.Optimization
{
    /// <summary>
    /// Represents a single chunk of the world grid
    /// Manages chunk state transitions: Unloaded -> Loaded-Proxy -> Loaded-Full
    /// </summary>
    public class WorldChunk : MonoBehaviour
    {
        public enum ChunkState
        {
            Unloaded,       // Chunk is not loaded
            LoadedProxy,    // Chunk is loaded with low-detail proxy
            LoadedFull      // Chunk is fully loaded with all details
        }

        [Header("Chunk Configuration")]
        public Vector2Int gridPosition;
        public float chunkSize = 64f;
        public ChunkState currentState = ChunkState.Unloaded;

        [Header("Chunk Content")]
        public GameObject fullDetailContent;      // Full detail buildings, props, etc.
        public GameObject proxyContent;           // Low-poly proxy meshes
        public List<GameObject> npcVehicles = new List<GameObject>();

        [Header("Performance Settings")]
        public bool disablePhysicsInProxy = true;
        public bool disableRenderersWhenUnloaded = true;

        private Collider[] chunkColliders;
        private Renderer[] fullDetailRenderers;
        private Renderer[] allChunkRenderers;

        private void Awake()
        {
            CacheComponents();
            // Enforce initial state immediately so unloaded chunks do not keep rendering.
            ApplyStateConfiguration();
        }

        private void CacheComponents()
        {
            if (fullDetailContent != null)
            {
                fullDetailRenderers = fullDetailContent.GetComponentsInChildren<Renderer>(true);
            }

            allChunkRenderers = GetComponentsInChildren<Renderer>(true);
            chunkColliders = GetComponentsInChildren<Collider>(true);
        }

        /// <summary>
        /// Transition chunk to a new state
        /// </summary>
        public void SetState(ChunkState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            ApplyStateConfiguration();
        }

        private void ApplyStateConfiguration()
        {
            switch (currentState)
            {
                case ChunkState.Unloaded:
                    SetUnloadedState();
                    break;

                case ChunkState.LoadedProxy:
                    SetProxyState();
                    break;

                case ChunkState.LoadedFull:
                    SetFullState();
                    break;
            }
        }

        private void SetUnloadedState()
        {
            // Disable everything
            if (fullDetailContent != null)
                fullDetailContent.SetActive(false);

            if (proxyContent != null)
                proxyContent.SetActive(false);

            // Disable NPCs
            foreach (var npc in npcVehicles)
            {
                if (npc != null)
                    npc.SetActive(false);
            }

            // Force-disable renderers so outside chunks are never drawn.
            if (disableRenderersWhenUnloaded)
            {
                SetRenderersEnabled(allChunkRenderers, false);
            }

            // Disable colliders
            foreach (var collider in chunkColliders)
            {
                if (collider != null)
                    collider.enabled = false;
            }
        }

        private void SetProxyState()
        {
            if (disableRenderersWhenUnloaded)
            {
                SetRenderersEnabled(allChunkRenderers, true);
            }

            // Enable proxy, disable full detail
            if (fullDetailContent != null)
                fullDetailContent.SetActive(false);

            if (proxyContent != null)
                proxyContent.SetActive(true);
            else
                SetRenderersEnabled(fullDetailRenderers, false);

            // Disable NPCs in proxy mode
            foreach (var npc in npcVehicles)
            {
                if (npc != null)
                    npc.SetActive(false);
            }

            // Disable physics colliders in proxy mode
            if (disablePhysicsInProxy)
            {
                foreach (var collider in chunkColliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = collider is MeshCollider;
                    }
                }
            }
            else
            {
                foreach (var collider in chunkColliders)
                {
                    if (collider != null)
                        collider.enabled = true;
                }
            }
        }

        private void SetFullState()
        {
            if (disableRenderersWhenUnloaded)
            {
                SetRenderersEnabled(allChunkRenderers, true);
            }

            // Enable full detail, disable proxy
            if (fullDetailContent != null)
                fullDetailContent.SetActive(true);
            else
                SetRenderersEnabled(fullDetailRenderers, true);

            if (proxyContent != null)
                proxyContent.SetActive(false);

            // Enable NPCs
            foreach (var npc in npcVehicles)
            {
                if (npc != null)
                    npc.SetActive(true);
            }

            // Enable all colliders
            foreach (var collider in chunkColliders)
            {
                if (collider != null)
                    collider.enabled = true;
            }
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
        {
            if (renderers == null) return;

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = isEnabled;
                }
            }
        }

        /// <summary>
        /// Get world position of chunk center
        /// </summary>
        public Vector3 GetWorldCenter()
        {
            return new Vector3(
                gridPosition.x * chunkSize + chunkSize * 0.5f,
                0,
                gridPosition.y * chunkSize + chunkSize * 0.5f
            );
        }

        /// <summary>
        /// Check if a world position is within this chunk
        /// </summary>
        public bool ContainsPosition(Vector3 worldPosition)
        {
            Vector3 chunkMin = new Vector3(gridPosition.x * chunkSize, 0, gridPosition.y * chunkSize);
            Vector3 chunkMax = chunkMin + new Vector3(chunkSize, 1000, chunkSize);

            return worldPosition.x >= chunkMin.x && worldPosition.x < chunkMax.x &&
                   worldPosition.z >= chunkMin.z && worldPosition.z < chunkMax.z;
        }

        /// <summary>
        /// Register an NPC vehicle to this chunk
        /// </summary>
        public void RegisterNPC(GameObject npc)
        {
            if (!npcVehicles.Contains(npc))
            {
                npcVehicles.Add(npc);
            }
        }

        /// <summary>
        /// Unregister an NPC vehicle from this chunk
        /// </summary>
        public void UnregisterNPC(GameObject npc)
        {
            npcVehicles.Remove(npc);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw chunk boundaries
            Vector3 center = GetWorldCenter();
            Vector3 size = new Vector3(chunkSize, 10f, chunkSize);

            Gizmos.color = currentState switch
            {
                ChunkState.Unloaded => Color.red,
                ChunkState.LoadedProxy => Color.yellow,
                ChunkState.LoadedFull => Color.green,
                _ => Color.white
            };

            Gizmos.DrawWireCube(center, size);
        }
    }
}
