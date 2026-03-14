using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal sealed class QuestZoneMarkerService
    {
        private readonly Func<GameObject> questZonePrefabProvider;
        private readonly Func<GameObject> pickupMarkerPrefabProvider;
        private readonly Func<GameObject> deliveryMarkerPrefabProvider;
        private readonly List<QuestZone> activeZones;
        private readonly Action<Vector3> spawnMarkerParticles;

        public QuestZoneMarkerService(
            Func<GameObject> questZonePrefabProvider,
            Func<GameObject> pickupMarkerPrefabProvider,
            Func<GameObject> deliveryMarkerPrefabProvider,
            List<QuestZone> activeZones,
            Action<Vector3> spawnMarkerParticles)
        {
            this.questZonePrefabProvider = questZonePrefabProvider;
            this.pickupMarkerPrefabProvider = pickupMarkerPrefabProvider;
            this.deliveryMarkerPrefabProvider = deliveryMarkerPrefabProvider;
            this.activeZones = activeZones ?? throw new ArgumentNullException(nameof(activeZones));
            this.spawnMarkerParticles = spawnMarkerParticles;
        }

        public void CleanupQuestMarkers(QuestData quest)
        {
            quest?.PickupLocation?.DestroyMarker();

            if (quest?.DeliveryLocations == null)
            {
                return;
            }

            foreach (QuestLocation location in quest.DeliveryLocations)
            {
                location?.DestroyMarker();
            }
        }

        public QuestZone SpawnQuestZone(QuestLocation location, QuestZoneType type)
        {
            if (location == null)
            {
                return null;
            }

            GameObject zoneObject = null;
            GameObject questZonePrefab = questZonePrefabProvider?.Invoke();
            if (questZonePrefab != null)
            {
                zoneObject = UnityEngine.Object.Instantiate(questZonePrefab, location.Position, Quaternion.identity);
            }

            if (zoneObject == null)
            {
                zoneObject = new GameObject("QuestZone");
            }

            zoneObject.transform.position = location.Position;

            QuestZone zone = zoneObject.GetComponent<QuestZone>();
            if (zone == null)
            {
                try
                {
                    zone = zoneObject.AddComponent<QuestZone>();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[QuestManager] Failed adding QuestZone component: {e.Message}");
                }
            }

            if (zone == null)
            {
                Debug.LogError("[QuestManager] Failed to create QuestZone component.");
                UnityEngine.Object.Destroy(zoneObject);
                return null;
            }

            zone.Configure(location, type);
            EnsureMarkerPrefab(location, type);

            Collider zoneCollider = zoneObject.GetComponent<Collider>();
            if (zoneCollider == null)
            {
                zoneCollider = zoneObject.AddComponent<SphereCollider>();
            }

            if (zoneCollider is SphereCollider sphere)
            {
                sphere.isTrigger = true;
                sphere.radius = Mathf.Max(0.1f, location.TriggerRadius);
            }
            else if (zoneCollider is BoxCollider box)
            {
                box.isTrigger = true;
                float size = Mathf.Max(0.1f, location.TriggerRadius * 2f);
                box.size = new Vector3(size, size, size);
            }

            zone.SetActive(true);
            activeZones.Add(zone);
            spawnMarkerParticles?.Invoke(location.Position);

            return zone;
        }

        public void ClearAllZones()
        {
            if (activeZones.Count == 0)
            {
                return;
            }

            for (int i = activeZones.Count - 1; i >= 0; i--)
            {
                QuestZone zone = activeZones[i];
                if (zone != null)
                {
                    UnityEngine.Object.Destroy(zone.gameObject);
                }
            }

            activeZones.Clear();
        }

        public void RestoreQuestMarkers(IEnumerable<QuestData> activeQuests, Func<QuestData, QuestLocation> getCurrentDeliveryLocation)
        {
            ClearAllZones();
            if (activeQuests == null)
            {
                return;
            }

            foreach (QuestData quest in activeQuests)
            {
                if (quest == null)
                {
                    continue;
                }

                if (!quest.HasPickedUpCargo)
                {
                    SpawnQuestZone(quest.PickupLocation, QuestZoneType.Pickup);
                }
                else
                {
                    QuestLocation delivery = getCurrentDeliveryLocation?.Invoke(quest);
                    SpawnQuestZone(delivery, QuestZoneType.Delivery);
                }
            }
        }

        private void EnsureMarkerPrefab(QuestLocation location, QuestZoneType type)
        {
            if (location == null || location.VisualMarker != null)
            {
                return;
            }

            location.VisualMarker = type == QuestZoneType.Pickup
                ? pickupMarkerPrefabProvider?.Invoke()
                : deliveryMarkerPrefabProvider?.Invoke();
        }
    }
}
