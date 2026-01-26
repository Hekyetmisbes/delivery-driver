using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Represents a location in the game world where quest events occur (pickup or delivery)
    /// </summary>
    [System.Serializable]
    public class QuestLocation
    {
        /// <summary>
        /// World position of the location
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// Display name for the location (e.g., "Downtown Warehouse", "Central Station")
        /// </summary>
        public string LocationName;

        /// <summary>
        /// Reference to the road segment this location is on
        /// </summary>
        public int RoadSegmentIndex;

        /// <summary>
        /// Reference to the specific waypoint on the road segment
        /// </summary>
        public int WaypointIndex;

        /// <summary>
        /// Detection radius in meters for player proximity
        /// </summary>
        public float TriggerRadius;

        /// <summary>
        /// Optional 3D marker prefab reference for visualizing this location
        /// </summary>
        public GameObject VisualMarker;

        /// <summary>
        /// Reference to the instantiated marker GameObject in the scene
        /// </summary>
        private GameObject instantiatedMarker;

        /// <summary>
        /// Default constructor
        /// </summary>
        public QuestLocation()
        {
            TriggerRadius = 10f;
        }

        /// <summary>
        /// Creates a new quest location with specified parameters
        /// </summary>
        /// <param name="pos">World position of the location</param>
        /// <param name="name">Display name for the location</param>
        /// <param name="radius">Detection radius (default: 10m)</param>
        public QuestLocation(Vector3 pos, string name, float radius = 10f)
        {
            Position = pos;
            LocationName = name;
            TriggerRadius = radius;
            RoadSegmentIndex = -1;
            WaypointIndex = -1;
        }

        /// <summary>
        /// Checks if the player is within range of this location
        /// </summary>
        /// <param name="playerTransform">The player's transform component</param>
        /// <returns>True if player is within the trigger radius, false otherwise</returns>
        public bool IsPlayerInRange(Transform playerTransform)
        {
            if (playerTransform == null)
            {
                Debug.LogWarning("QuestLocation.IsPlayerInRange: playerTransform is null");
                return false;
            }

            float distance = Vector3.Distance(Position, playerTransform.position);
            return distance <= TriggerRadius;
        }

        /// <summary>
        /// Shows the visual marker at this location
        /// </summary>
        public void ShowMarker()
        {
            if (VisualMarker == null)
            {
                Debug.LogWarning($"QuestLocation.ShowMarker: No visual marker prefab assigned for location '{LocationName}'");
                return;
            }

            // If marker already instantiated, just enable it
            if (instantiatedMarker != null)
            {
                instantiatedMarker.SetActive(true);
            }
            else
            {
                // Instantiate the marker at this location
                instantiatedMarker = Object.Instantiate(VisualMarker, Position, Quaternion.identity);
                instantiatedMarker.name = $"QuestMarker_{LocationName}";
            }
        }

        /// <summary>
        /// Hides the visual marker at this location
        /// </summary>
        public void HideMarker()
        {
            if (instantiatedMarker != null)
            {
                instantiatedMarker.SetActive(false);
            }
        }

        /// <summary>
        /// Destroys the instantiated marker GameObject
        /// </summary>
        public void DestroyMarker()
        {
            if (instantiatedMarker != null)
            {
                Object.Destroy(instantiatedMarker);
                instantiatedMarker = null;
            }
        }
    }
}
