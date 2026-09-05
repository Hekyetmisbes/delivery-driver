using UnityEngine;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Trigger zone placed on bridges or highways between cities.
    /// Informs CityStreamingManager that the player is in the transition corridor.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TransitionCorridorZone : MonoBehaviour
    {

        private Collider triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;

            if (CityStreamingManager.Instance != null)
            {
                CityStreamingManager.Instance.SetTransitionCorridorState(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;

            if (CityStreamingManager.Instance != null)
            {
                CityStreamingManager.Instance.SetTransitionCorridorState(false);
            }
        }

        private bool IsPlayer(Collider col)
        {
            return col.CompareTag("Player") || col.GetComponentInParent<CarController>() != null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(col.center, col.size);
            }
        }
    }
}
