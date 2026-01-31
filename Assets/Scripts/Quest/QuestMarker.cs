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

        [Header("Task 10.6: Marker LOD")]
        [SerializeField] private GameObject fullModelRoot;
        [SerializeField] private GameObject billboardRoot;
        [SerializeField] private float lodSwitchDistance = 150f;
        [SerializeField] private float lodHysteresis = 10f;

        private Vector3 basePosition;
        private Camera mainCamera;
        private bool isBillboardActive;

        private void Awake()
        {
            basePosition = transform.position;
            mainCamera = Camera.main;
            UpdateLodState(force: true);
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

            UpdateLodState(force: false);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                basePosition = target.position;
            }
        }

        private void UpdateLodState(bool force)
        {
            if (fullModelRoot == null && billboardRoot == null)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    return;
                }
            }

            Vector3 delta = mainCamera.transform.position - transform.position;
            float distanceSqr = delta.sqrMagnitude;

            float enterDistance = lodSwitchDistance + lodHysteresis;
            float exitDistance = Mathf.Max(0f, lodSwitchDistance - lodHysteresis);
            float enterSqr = enterDistance * enterDistance;
            float exitSqr = exitDistance * exitDistance;

            bool shouldUseBillboard = isBillboardActive
                ? distanceSqr > exitSqr
                : distanceSqr > enterSqr;

            if (!force && shouldUseBillboard == isBillboardActive)
            {
                return;
            }

            isBillboardActive = shouldUseBillboard;

            if (fullModelRoot != null)
            {
                fullModelRoot.SetActive(!isBillboardActive);
            }

            if (billboardRoot != null)
            {
                billboardRoot.SetActive(isBillboardActive);
            }
        }
    }
}
