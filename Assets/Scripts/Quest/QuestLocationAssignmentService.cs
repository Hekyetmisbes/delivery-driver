using System;
using System.Collections.Generic;
using TrafficSystem;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal sealed class QuestLocationAssignmentService
    {
        private readonly Func<RoadGraphBuilder> roadGraphBuilderProvider;
        private readonly Func<GameObject> pickupMarkerPrefabProvider;
        private readonly Func<GameObject> deliveryMarkerPrefabProvider;
        private readonly List<Vector3> usedLocations;
        private readonly float locationCooldownDistance;

        public QuestLocationAssignmentService(
            Func<RoadGraphBuilder> roadGraphBuilderProvider,
            Func<GameObject> pickupMarkerPrefabProvider,
            Func<GameObject> deliveryMarkerPrefabProvider,
            List<Vector3> usedLocations,
            float locationCooldownDistance)
        {
            this.roadGraphBuilderProvider = roadGraphBuilderProvider;
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

            QuestLocation pickup = null;
            QuestLocation delivery = null;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                pickup = GenerateRandomLocation("Pickup");
                delivery = GenerateRandomLocation("Delivery");

                if (AreLocationsValid(pickup, delivery, quest.Difficulty))
                {
                    break;
                }
            }

            if (pickup == null || delivery == null)
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
                QuestLocation extraStop = GenerateRandomLocation("Delivery");
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
            RoadGraphBuilder roadGraphBuilder = roadGraphBuilderProvider?.Invoke();
            if (roadGraphBuilder == null || roadGraphBuilder.RoadGraph == null)
            {
                Debug.Log("[QuestManager] RoadGraphBuilder is not ready yet. Skipping quest location generation.");
                return null;
            }

            int attempts = 0;
            RoadSegment segment = null;
            int waypointIndex = -1;
            Vector3 candidatePosition = Vector3.zero;

            while (attempts < 20)
            {
                attempts++;

                var result = roadGraphBuilder.RoadGraph.GetRandomWaypoint();
                segment = result.Item1;
                waypointIndex = result.Item2;

                if (segment == null || segment.waypoints.Count == 0)
                {
                    continue;
                }

                candidatePosition = segment.waypoints[waypointIndex].position;

                bool tooClose = false;
                float cooldownDistanceSqr = locationCooldownDistance * locationCooldownDistance;
                foreach (Vector3 used in usedLocations)
                {
                    Vector3 delta = candidatePosition - used;
                    if (delta.sqrMagnitude < cooldownDistanceSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                if (!Physics.Raycast(candidatePosition + Vector3.up * 50f, Vector3.down, out _, 100f))
                {
                    continue;
                }

                break;
            }

            if (segment == null)
            {
                return null;
            }

            usedLocations.Add(candidatePosition);
            if (usedLocations.Count > 20)
            {
                usedLocations.RemoveAt(0);
            }

            string[] locationTypes;
            if (segment.id % 4 == 0)
            {
                locationTypes = new[] { "Industrial Park", "Factory", "Plant", "Refinery" };
            }
            else if (segment.id % 4 == 1)
            {
                locationTypes = new[] { "Mall", "Plaza", "Store", "Market", "Shop" };
            }
            else if (segment.id % 4 == 2)
            {
                locationTypes = new[] { "Residence", "Apartments", "Estate", "Manor" };
            }
            else
            {
                locationTypes = new[] { "Warehouse", "Depot", "Station", "Hub", "Terminal" };
            }

            string locationType = locationTypes[UnityEngine.Random.Range(0, locationTypes.Length)];
            string[] directions = { "North", "South", "East", "West", "Central", "Upper", "Lower" };
            string direction = directions[UnityEngine.Random.Range(0, directions.Length)];
            string locationName = $"{direction} {locationType}";
            float triggerRadius = UnityEngine.Random.Range(10f, 15f);

            QuestLocation location = new QuestLocation(candidatePosition, locationName, triggerRadius)
            {
                RoadSegmentIndex = segment.id,
                WaypointIndex = waypointIndex
            };

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                location.LocationName = locationName;
            }

            return location;
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

            if (pickup.RoadSegmentIndex < 0 || delivery.RoadSegmentIndex < 0)
            {
                return false;
            }

            Vector3 delta = pickup.Position - delivery.Position;
            float distanceSqr = delta.sqrMagnitude;
            float minDistance = difficulty switch
            {
                QuestDifficulty.Easy => 500f,
                QuestDifficulty.Medium => 1000f,
                QuestDifficulty.Hard => 1500f,
                QuestDifficulty.Expert => 2000f,
                _ => 500f
            };

            return distanceSqr >= minDistance * minDistance;
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
