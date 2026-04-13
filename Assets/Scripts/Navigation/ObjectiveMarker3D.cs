using UnityEngine;

namespace DeliveryDriver.Navigation
{
    public class ObjectiveMarker3D : MonoBehaviour
    {
        [SerializeField] private bool enableLegacyWorldMarker = false;

        [Header("Marker Settings")]
        [SerializeField] private float markerHeight = 24f;
        [SerializeField] private Vector3 markerScale = new Vector3(4f, 10f, 4f);
        [SerializeField] private float spinSpeed = 120f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmount = 0.2f;
        [SerializeField] private Color pickupColor = new Color(0.1f, 1f, 1f, 1f);
        [SerializeField] private Color deliveryColor = new Color(1f, 0.9f, 0.05f, 1f);
        [SerializeField] private string markerLayerName = "NavigationMarker";
        [SerializeField] private float followSmoothTime = 0.08f;

        private GameObject markerObject;
        private Material markerMaterial;
        private int cachedMarkerLayer = int.MinValue;
        private Vector3 markerVelocity;
        private bool hasMarkerPosition;
        private NavigationObjective currentObjective;

        private void OnEnable()
        {
            if (!enableLegacyWorldMarker)
            {
                RemoveMarker();
                return;
            }

            if (NavigationService.Instance != null)
            {
                NavigationService.Instance.OnObjectiveChanged += HandleObjectiveChanged;
                NavigationService.Instance.OnNavigationCleared += HandleNavigationCleared;
            }
        }

        private void OnDisable()
        {
            if (NavigationService.Instance != null)
            {
                NavigationService.Instance.OnObjectiveChanged -= HandleObjectiveChanged;
                NavigationService.Instance.OnNavigationCleared -= HandleNavigationCleared;
            }
        }

        private void OnDestroy()
        {
            RemoveMarker();
        }

        private void Update()
        {
            if (!enableLegacyWorldMarker || !currentObjective.IsValid)
            {
                return;
            }

            if (markerObject == null)
            {
                return;
            }

            Vector3 desiredPosition = currentObjective.WorldPosition + Vector3.up * markerHeight;
            if (!hasMarkerPosition)
            {
                markerObject.transform.position = desiredPosition;
                markerVelocity = Vector3.zero;
                hasMarkerPosition = true;
            }
            else
            {
                float smoothTime = Mathf.Max(0.01f, followSmoothTime);
                markerObject.transform.position = Vector3.SmoothDamp(
                    markerObject.transform.position,
                    desiredPosition,
                    ref markerVelocity,
                    smoothTime);
            }

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            markerObject.transform.localScale = markerScale * pulse;
            markerObject.transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        private void HandleObjectiveChanged(NavigationObjective objective)
        {
            currentObjective = objective;

            if (!objective.IsValid)
            {
                RemoveMarker();
                return;
            }

            EnsureMarker();
            if (markerObject != null)
            {
                markerObject.SetActive(true);
                hasMarkerPosition = false;
            }

            Color color = objective.Type == ObjectiveType.Delivery ? deliveryColor : pickupColor;
            if (markerMaterial != null)
            {
                markerMaterial.color = color;
            }
        }

        private void HandleNavigationCleared()
        {
            currentObjective = NavigationObjective.Empty;
            RemoveMarker();
        }

        private void EnsureMarker()
        {
            if (markerObject != null)
            {
                return;
            }

            markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = "NavigationObjectiveMarker";
            markerObject.transform.localScale = markerScale;

            int layer = ResolveMarkerLayer();
            if (layer >= 0)
            {
                markerObject.layer = layer;
            }

            Collider markerCollider = markerObject.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Destroy(markerCollider);
            }

            MeshRenderer renderer = markerObject.GetComponent<MeshRenderer>();
            markerMaterial = RuntimeColorMaterialHelper.CreateColorMaterial(pickupColor, renderer);
            if (markerMaterial != null && renderer != null)
            {
                renderer.material = markerMaterial;
            }
        }

        private int ResolveMarkerLayer()
        {
            if (cachedMarkerLayer == int.MinValue)
            {
                cachedMarkerLayer = LayerMask.NameToLayer(markerLayerName);
            }
            return cachedMarkerLayer;
        }

        private void RemoveMarker()
        {
            if (markerObject != null)
            {
                Destroy(markerObject);
                markerObject = null;
                hasMarkerPosition = false;
            }

            if (markerMaterial != null)
            {
                Destroy(markerMaterial);
                markerMaterial = null;
            }
        }
    }
}
