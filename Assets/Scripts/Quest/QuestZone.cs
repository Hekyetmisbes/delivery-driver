using UnityEngine;
using UnityEngine.Events;

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
    }
}
