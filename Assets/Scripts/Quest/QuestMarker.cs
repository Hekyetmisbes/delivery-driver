using UnityEngine;

namespace DeliveryDriver.Quest
{
    public class QuestMarker : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.5f;
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private ParticleSystem particles;

        private Vector3 basePosition;

        private void Awake()
        {
            basePosition = transform.position;
        }

        private void Update()
        {
            if (target != null)
            {
                basePosition = target.position;
            }

            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = basePosition + Vector3.up * bobOffset;

            if (rotationSpeed != 0f)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                basePosition = target.position;
            }
        }
    }
}
