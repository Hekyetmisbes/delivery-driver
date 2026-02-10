using UnityEngine;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Trigger zone for neighborhoods. Notifies NeighborhoodManager when player enters.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class NeighborhoodZone : MonoBehaviour
    {
        [SerializeField] private string neighborhoodName;
        [SerializeField] private Color debugColor = Color.cyan;

        private BoxCollider cachedCollider;
        private bool playerInside = false;

        public string NeighborhoodName
        {
            get => neighborhoodName;
            set => neighborhoodName = value;
        }

        public Color DebugColor
        {
            get => debugColor;
            set => debugColor = value;
        }

        private void Awake()
        {
            cachedCollider = GetComponent<BoxCollider>();
            if (cachedCollider != null)
            {
                cachedCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other) && !playerInside)
            {
                playerInside = true;
                NeighborhoodManager.Instance?.OnPlayerEnteredNeighborhood(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                playerInside = false;
                NeighborhoodManager.Instance?.OnPlayerExitedNeighborhood(this);
            }
        }

        private bool IsPlayer(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            return other.CompareTag("Player");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(debugColor.r, debugColor.g, debugColor.b, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = debugColor;
            Gizmos.DrawWireCube(box.center, box.size);

            if (!string.IsNullOrWhiteSpace(neighborhoodName))
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * (box.size.y * 0.5f + 2f),
                    neighborhoodName,
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = debugColor },
                        fontSize = 14,
                        fontStyle = FontStyle.Bold
                    }
                );
            }
        }
#endif
    }
}
