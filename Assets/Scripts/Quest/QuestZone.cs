using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeliveryDriver.Quest
{
    [RequireComponent(typeof(Collider))]
    public class QuestZone : MonoBehaviour
    {
        [SerializeField] private QuestLocation location;
        [SerializeField] private QuestZoneType zoneType;
        [SerializeField] private bool isActive = true;

        public UnityEvent<Transform> OnPlayerEntered = new UnityEvent<Transform>();
        public UnityEvent<Transform> OnPlayerExited = new UnityEvent<Transform>();

        private Collider cachedCollider;

        public QuestLocation Location => location;
        public QuestZoneType ZoneType => zoneType;
        public bool IsActive => isActive;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            if (cachedCollider != null)
            {
                cachedCollider.isTrigger = true;
            }
        }

        public void Configure(QuestLocation questLocation, QuestZoneType type)
        {
            location = questLocation;
            zoneType = type;
        }

        public void SetLocation(QuestLocation questLocation)
        {
            location = questLocation;
        }

        public void SetActive(bool active)
        {
            isActive = active;

            if (cachedCollider != null)
            {
                cachedCollider.enabled = active;
            }

            if (location != null)
            {
                if (active)
                {
                    location.ShowMarker();
                }
                else
                {
                    location.HideMarker();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActive)
            {
                return;
            }

            if (!IsPlayer(other))
            {
                return;
            }

            OnPlayerEntered?.Invoke(other.transform);
            QuestManager.Instance?.OnPlayerEnteredZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!isActive)
            {
                return;
            }

            if (!IsPlayer(other))
            {
                return;
            }

            OnPlayerExited?.Invoke(other.transform);
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
            Color color = zoneType == QuestZoneType.Pickup ? Color.cyan : Color.green;
            Gizmos.color = color;

            float radius = 0.5f;
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider is SphereCollider sphere)
            {
                radius = sphere.radius;
            }
            else if (location != null)
            {
                radius = location.TriggerRadius;
            }

            Gizmos.DrawWireSphere(transform.position, radius);

            if (location != null && !string.IsNullOrWhiteSpace(location.LocationName))
            {
                Handles.Label(transform.position + Vector3.up * (radius + 0.5f), location.LocationName);
            }
        }
#endif
    }
}
