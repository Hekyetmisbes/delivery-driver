using System;
using System.Collections.Generic;
using TrafficSystem;
using TrafficSystem.Spline;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal sealed class QuestLocationAssignmentService
    {
        private readonly Func<RoadNetworkManager> roadNetworkManagerProvider;
        private readonly Func<GameObject> pickupMarkerPrefabProvider;
        private readonly Func<GameObject> deliveryMarkerPrefabProvider;
        private readonly List<Vector3> usedLocations;
        private readonly float locationCooldownDistance;
        private const int MaxLocationCandidateCount = 1024;

        public QuestLocationAssignmentService(
            Func<RoadNetworkManager> roadNetworkManagerProvider,
            Func<GameObject> pickupMarkerPrefabProvider,
            Func<GameObject> deliveryMarkerPrefabProvider,
            List<Vector3> usedLocations,
            float locationCooldownDistance)
        {
            this.roadNetworkManagerProvider = roadNetworkManagerProvider;
            this.pickupMarkerPrefabProvider = pickupMarkerPrefabProvider;
            this.deliveryMarkerPrefabProvider = deliveryMarkerPrefabProvider;
            this.usedLocations = usedLocations ?? throw new ArgumentNullException(nameof(usedLocations));
            this.locationCooldownDistance = locationCooldownDistance;
        }

        public bool AssignQuestLocations(QuestData quest)
        {
            if (quest == null)
            {
                return false;
            }

            bool foundPrimaryLocations = TryGeneratePrimaryLocations(
                quest.Difficulty,
                out QuestLocation pickup,
                out QuestLocation delivery);

            if (!foundPrimaryLocations)
            {
                Debug.LogWarning("[QuestManager] Failed to generate valid quest locations.");
                return false;
            }

            quest.PickupLocation = pickup;
            EnsurePickupMarker(quest.PickupLocation);

            quest.DeliveryLocations ??= new List<QuestLocation>();
            quest.DeliveryLocations.Clear();

            if (delivery != null)
            {
                EnsureDeliveryMarker(delivery);
                quest.DeliveryLocations.Add(delivery);
            }

            if (quest.QuestType == QuestType.MultiStopDelivery)
            {
                QuestLocation extraStop = GenerateRandomLocation("Delivery", reserveLocation: true);
                if (extraStop != null)
                {
                    EnsureDeliveryMarker(extraStop);
                    quest.DeliveryLocations.Add(extraStop);
                }
            }

            return true;
        }

        public QuestLocation GenerateRandomLocation(string prefix)
        {
            return GenerateRandomLocation(prefix, reserveLocation: true);
        }

        private QuestLocation GenerateRandomLocation(string prefix, bool reserveLocation)
        {
            var networkManager = roadNetworkManagerProvider?.Invoke();
            if (networkManager == null || networkManager.roads.Count == 0)
            {
                Debug.Log("[QuestManager] RoadNetworkManager is not ready yet. Skipping quest location generation.");
                return null;
            }

            int attempts = 0;
            Vector3 candidatePosition = Vector3.zero;

            while (attempts < 20)
            {
                attempts++;

                var road = networkManager.GetRandomRoad();
                if (road == null) continue;
                float t = UnityEngine.Random.value;
                bool isForward = UnityEngine.Random.value > 0.5f;
                candidatePosition = road.GetLanePosition(t, isForward);
                candidatePosition.y += 0.2f; // Offset from ground

                bool isTooClose = false;
                foreach (Vector3 used in usedLocations)
                {
                    if (Vector3.Distance(used, candidatePosition) < locationCooldownDistance)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                if (!isTooClose)
                {
                    if (reserveLocation)
                    {
                        ReserveLocation(candidatePosition);
                    }

                    string[] locationTypes = { "Industrial Park", "Factory", "Plant", "Mall", "Plaza", "Store", "Residence", "Apartments", "Warehouse", "Depot" };
                    string locationType = locationTypes[UnityEngine.Random.Range(0, locationTypes.Length)];
                    string[] directions = { "North", "South", "East", "West", "Central", "Upper", "Lower" };
                    string direction = directions[UnityEngine.Random.Range(0, directions.Length)];
                    string locationName = $"{direction} {locationType}";
                    float triggerRadius = UnityEngine.Random.Range(10f, 15f);

                    return new QuestLocation(candidatePosition, locationName, triggerRadius);
                }
            }

            return null;
        }

        private bool TryGeneratePrimaryLocations(QuestDifficulty difficulty, out QuestLocation pickup, out QuestLocation delivery)
        {
            pickup = null;
            delivery = null;

            var networkManager = roadNetworkManagerProvider?.Invoke();
            if (networkManager == null || networkManager.roads.Count == 0)
            {
                Debug.Log("[QuestManager] RoadNetworkManager is not ready yet. Skipping quest location generation.");
                return false;
            }

            List<Vector3> candidates = BuildLocationCandidates(networkManager);
            if (candidates.Count < 2)
            {
                return false;
            }

            float minDistance = GetMinimumDistanceForDifficulty(difficulty);
            float minDistanceSqr = minDistance * minDistance;
            float bestDistanceSqr = -1f;
            Vector3 bestPickup = Vector3.zero;
            Vector3 bestDelivery = Vector3.zero;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 pickupCandidate = candidates[i];
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    Vector3 deliveryCandidate = candidates[j];
                    float distanceSqr = (pickupCandidate - deliveryCandidate).sqrMagnitude;
                    if (distanceSqr < minDistanceSqr || distanceSqr <= bestDistanceSqr)
                    {
                        continue;
                    }

                    bestDistanceSqr = distanceSqr;
                    bestPickup = pickupCandidate;
                    bestDelivery = deliveryCandidate;
                }
            }

            if (bestDistanceSqr < 0f)
            {
                return false;
            }

            pickup = CreateQuestLocation(bestPickup);
            delivery = CreateQuestLocation(bestDelivery);
            ReserveLocation(bestPickup);
            ReserveLocation(bestDelivery);
            return true;
        }

        private List<Vector3> BuildLocationCandidates(RoadNetworkManager networkManager)
        {
            List<Vector3> candidates = new List<Vector3>(MaxLocationCandidateCount);
            int roadCount = networkManager.roads.Count;
            int step = Mathf.Max(1, Mathf.CeilToInt(roadCount * 3f / MaxLocationCandidateCount));

            for (int roadIndex = 0; roadIndex < roadCount && candidates.Count < MaxLocationCandidateCount; roadIndex += step)
            {
                var road = networkManager.roads[roadIndex];
                if (road == null)
                {
                    continue;
                }

                AddLocationCandidate(candidates, road.GetLanePosition(0.1f, true) + Vector3.up * 0.2f);
                AddLocationCandidate(candidates, road.GetLanePosition(0.5f, true) + Vector3.up * 0.2f);
                AddLocationCandidate(candidates, road.GetLanePosition(0.9f, true) + Vector3.up * 0.2f);
            }

            return candidates;
        }

        private void AddLocationCandidate(List<Vector3> candidates, Vector3 candidatePosition)
        {
            if (candidates.Count >= MaxLocationCandidateCount || IsLocationOnCooldown(candidatePosition))
            {
                return;
            }

            candidates.Add(candidatePosition);
        }

        private bool IsLocationOnCooldown(Vector3 candidatePosition)
        {
            for (int i = 0; i < usedLocations.Count; i++)
            {
                if (Vector3.Distance(usedLocations[i], candidatePosition) < locationCooldownDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private QuestLocation CreateQuestLocation(Vector3 position)
        {
            string[] locationTypes = { "Industrial Park", "Factory", "Plant", "Mall", "Plaza", "Store", "Residence", "Apartments", "Warehouse", "Depot" };
            string locationType = locationTypes[UnityEngine.Random.Range(0, locationTypes.Length)];
            string[] directions = { "North", "South", "East", "West", "Central", "Upper", "Lower" };
            string direction = directions[UnityEngine.Random.Range(0, directions.Length)];
            string locationName = $"{direction} {locationType}";
            float triggerRadius = UnityEngine.Random.Range(10f, 15f);

            return new QuestLocation(position, locationName, triggerRadius);
        }

        private void ReserveLocation(Vector3 position)
        {
            usedLocations.Add(position);

            if (usedLocations.Count > 20)
            {
                usedLocations.RemoveAt(0);
            }
        }

        public void EnsureQuestMarkersAssigned(QuestData quest)
        {
            if (quest == null)
            {
                return;
            }

            EnsurePickupMarker(quest.PickupLocation);

            if (quest.DeliveryLocations == null)
            {
                return;
            }

            foreach (QuestLocation delivery in quest.DeliveryLocations)
            {
                EnsureDeliveryMarker(delivery);
            }
        }

        public void EnsurePickupMarker(QuestLocation location)
        {
            EnsureMarker(location, pickupMarkerPrefabProvider?.Invoke());
        }

        public void EnsureDeliveryMarker(QuestLocation location)
        {
            EnsureMarker(location, deliveryMarkerPrefabProvider?.Invoke());
        }

        private bool AreLocationsValid(QuestLocation pickup, QuestLocation delivery, QuestDifficulty difficulty)
        {
            if (pickup == null || delivery == null)
            {
                return false;
            }

            float minDistance = GetMinimumDistanceForDifficulty(difficulty);
            Vector3 delta = pickup.Position - delivery.Position;
            float distanceSqr = delta.sqrMagnitude;

            return distanceSqr >= minDistance * minDistance;
        }

        private static float GetMinimumDistanceForDifficulty(QuestDifficulty difficulty)
        {
            return difficulty switch
            {
                QuestDifficulty.Easy => 150f,
                QuestDifficulty.Medium => 300f,
                QuestDifficulty.Hard => 600f,
                QuestDifficulty.Expert => 1000f,
                _ => 150f
            };
        }

        private static void EnsureMarker(QuestLocation location, GameObject prefab)
        {
            if (location == null || location.VisualMarker != null || prefab == null)
            {
                return;
            }

            location.VisualMarker = prefab;
        }
    }
}
