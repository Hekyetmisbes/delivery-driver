using System.Collections.Generic;
using UnityEngine;

namespace TrafficSystem.Spline
{
    public class RoadIntersection : MonoBehaviour
    {
        [Tooltip("The roads connected to this intersection.")]
        public List<SplineRoad> connectedRoads = new List<SplineRoad>();

        [Tooltip("City or Highway sector this intersection belongs to (e.g., 'Ankara', 'İstanbul', 'Highways')")]
        public string sectorTag = "Ankara";

        [Tooltip("Is this a 3-way or 4-way intersection?")]
        public bool isStopSignControlled = true;

        // Maximum time a single vehicle can block the intersection before being auto-evicted (deadlock prevention)
        private const float MaxOccupancyDuration = 7.0f;
        private float lastEntryTime;

        // Runtime data for traffic yield logic
        private readonly HashSet<NpcCarAgent> vehiclesInIntersection = new HashSet<NpcCarAgent>();
        private readonly Queue<NpcCarAgent> waitingVehicles = new Queue<NpcCarAgent>();

        private void Awake()
        {
            if (connectedRoads == null)
            {
                connectedRoads = new List<SplineRoad>();
            }
        }

        private void OnValidate()
        {
            if (connectedRoads == null)
            {
                connectedRoads = new List<SplineRoad>();
            }
        }

        public void RegisterConnectedRoad(SplineRoad road)
        {
            if (road == null) return;

            if (connectedRoads == null)
            {
                connectedRoads = new List<SplineRoad>();
            }

            if (!connectedRoads.Contains(road))
            {
                connectedRoads.Add(road);
            }
        }

        public void UnregisterConnectedRoad(SplineRoad road)
        {
            if (road == null || connectedRoads == null) return;

            if (connectedRoads.Contains(road))
            {
                connectedRoads.Remove(road);
            }
        }

        public bool RequestEntry(NpcCarAgent vehicle)
        {
            if (vehicle == null) return true;

            // 1. Purge stale/destroyed/inactive vehicles
            CleanStaleVehicles();

            // 2. Deadlock timeout check: If a car has been inside for too long, evict it
            if (vehiclesInIntersection.Count > 0 && Time.time - lastEntryTime > MaxOccupancyDuration)
            {
                vehiclesInIntersection.Clear();
            }

            // 3. If this vehicle is already inside/approved, allow it to continue
            if (vehiclesInIntersection.Contains(vehicle))
            {
                return true;
            }

            // 4. If empty and either queue is empty or this vehicle is first in queue
            if (vehiclesInIntersection.Count == 0)
            {
                if (waitingVehicles.Count == 0 || waitingVehicles.Peek() == vehicle)
                {
                    if (waitingVehicles.Count > 0 && waitingVehicles.Peek() == vehicle)
                    {
                        waitingVehicles.Dequeue();
                    }
                    vehiclesInIntersection.Add(vehicle);
                    lastEntryTime = Time.time;
                    return true;
                }
            }

            // 5. Must wait
            if (!waitingVehicles.Contains(vehicle))
            {
                waitingVehicles.Enqueue(vehicle);
            }

            return false;
        }

        public void ExitIntersection(NpcCarAgent vehicle)
        {
            if (vehicle == null) return;

            vehiclesInIntersection.Remove(vehicle);
            RemoveFromQueue(vehicle);

            if (vehiclesInIntersection.Count == 0)
            {
                lastEntryTime = 0f;
            }
        }

        private void CleanStaleVehicles()
        {
            vehiclesInIntersection.RemoveWhere(v => v == null || !v.gameObject.activeInHierarchy || Vector3.Distance(transform.position, v.transform.position) > 25f);

            if (waitingVehicles.Count > 0)
            {
                Queue<NpcCarAgent> validQueue = new Queue<NpcCarAgent>(waitingVehicles.Count);
                while (waitingVehicles.Count > 0)
                {
                    var v = waitingVehicles.Dequeue();
                    if (v != null && v.gameObject.activeInHierarchy && Vector3.Distance(transform.position, v.transform.position) < 35f)
                    {
                        validQueue.Enqueue(v);
                    }
                }
                while (validQueue.Count > 0)
                {
                    waitingVehicles.Enqueue(validQueue.Dequeue());
                }
            }
        }

        private void RemoveFromQueue(NpcCarAgent vehicle)
        {
            if (!waitingVehicles.Contains(vehicle)) return;

            Queue<NpcCarAgent> newQueue = new Queue<NpcCarAgent>(waitingVehicles.Count);
            while (waitingVehicles.Count > 0)
            {
                var v = waitingVehicles.Dequeue();
                if (v != vehicle) newQueue.Enqueue(v);
            }
            while (newQueue.Count > 0)
            {
                waitingVehicles.Enqueue(newQueue.Dequeue());
            }
        }

        private void OnDrawGizmos()
        {
            if (connectedRoads == null) return;
            connectedRoads.RemoveAll(r => r == null);
            if (connectedRoads.Count < 2) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawSphere(transform.position, 1.2f);
        }
    }
}
