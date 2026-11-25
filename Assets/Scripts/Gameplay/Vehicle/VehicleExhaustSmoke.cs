using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeliveryDriver.Vehicle
{
    public enum VehicleExhaustSmokePreset
    {
        Van,
        Truck,
        NpcLight,
        NpcTruckLight
    }

    public class VehicleExhaustSmoke : MonoBehaviour
    {
        private const string ExhaustRootName = "RuntimeExhaustSmoke";

        [Header("Preset")]
        [SerializeField] private VehicleExhaustSmokePreset preset = VehicleExhaustSmokePreset.Van;
        [SerializeField] private Vector3[] exhaustPorts = { new Vector3(-0.32f, 0.26f, -1.58f) };

        [Header("Smoke Shape")]
        [SerializeField] private Color smokeColor = new Color(0.68f, 0.69f, 0.67f, 0.58f);
        [SerializeField] private float startSize = 0.42f;
        [SerializeField] private float endSizeMultiplier = 2.35f;
        [SerializeField] private float lifetime = 1.6f;
        [SerializeField] private float startSpeed = 0.75f;
        [SerializeField] private float coneAngle = 16f;
        [SerializeField] private float coneRadius = 0.08f;

        [Header("Emission")]
        [SerializeField] private float idleEmissionRate = 4f;
        [SerializeField] private float movingEmissionRate = 10f;
        [SerializeField] private float maxRateSpeedKmh = 70f;

        private Rigidbody vehicleRigidbody;
        private Transform smokeRoot;
        private ParticleSystem[] emitters;

        private static Material sharedSmokeMaterial;
        private static Mesh sharedSmokeMesh;

        public void ConfigurePreset(VehicleExhaustSmokePreset targetPreset)
        {
            preset = targetPreset;

            if (preset == VehicleExhaustSmokePreset.Truck || preset == VehicleExhaustSmokePreset.NpcTruckLight)
            {
                bool isNpc = preset == VehicleExhaustSmokePreset.NpcTruckLight;
                exhaustPorts = new[]
                {
                    new Vector3(-0.68f, 0.48f, -1.95f),
                    new Vector3(0.96f, 0.48f, -1.95f)
                };

                smokeColor = new Color(0.62f, 0.63f, 0.6f, 0.6f);
                startSize = isNpc ? 0.36f : 0.56f;
                endSizeMultiplier = isNpc ? 2.2f : 2.55f;
                lifetime = isNpc ? 1.35f : 1.9f;
                startSpeed = 0.65f;
                coneAngle = 18f;
                coneRadius = 0.11f;
                idleEmissionRate = isNpc ? 1.2f : 5f;
                movingEmissionRate = isNpc ? 4.5f : 14f;
                maxRateSpeedKmh = 65f;
            }
            else
            {
                bool isNpc = preset == VehicleExhaustSmokePreset.NpcLight;
                exhaustPorts = new[] { new Vector3(-0.32f, 0.26f, -1.58f) };
                smokeColor = new Color(0.68f, 0.69f, 0.67f, 0.58f);
                startSize = isNpc ? 0.26f : 0.42f;
                endSizeMultiplier = isNpc ? 2.05f : 2.35f;
                lifetime = isNpc ? 1.15f : 1.6f;
                startSpeed = 0.75f;
                coneAngle = 16f;
                coneRadius = 0.08f;
                idleEmissionRate = isNpc ? 0.9f : 4f;
                movingEmissionRate = isNpc ? 3.2f : 10f;
                maxRateSpeedKmh = 70f;
            }

            RebuildEmitters();
        }

        private void Awake()
        {
            vehicleRigidbody = GetComponent<Rigidbody>();
            RebuildEmitters();
        }

        private void OnEnable()
        {
            SetEmissionEnabled(true);
        }

        private void OnDisable()
        {
            SetEmissionEnabled(false);
        }

        private void Update()
        {
            if (emitters == null)
            {
                return;
            }

            float speedKmh = vehicleRigidbody != null ? vehicleRigidbody.linearVelocity.magnitude * 3.6f : 0f;
            float speedFactor = Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxRateSpeedKmh));
            float rate = Mathf.Lerp(idleEmissionRate, movingEmissionRate, speedFactor);

            for (int i = 0; i < emitters.Length; i++)
            {
                ParticleSystem emitter = emitters[i];
                if (emitter == null) continue;

                ParticleSystem.EmissionModule emission = emitter.emission;
                emission.rateOverTime = rate;
            }
        }

        private void RebuildEmitters()
        {
            if (!isActiveAndEnabled && Application.isPlaying)
            {
                return;
            }

            EnsureRoot();

            if (exhaustPorts == null || exhaustPorts.Length == 0)
            {
                exhaustPorts = new[] { new Vector3(-0.32f, 0.26f, -1.58f) };
            }

            for (int i = smokeRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(smokeRoot.GetChild(i).gameObject);
            }

            emitters = new ParticleSystem[exhaustPorts.Length];
            for (int i = 0; i < exhaustPorts.Length; i++)
            {
                emitters[i] = CreateEmitter(i, exhaustPorts[i]);
            }
        }

        private void EnsureRoot()
        {
            if (smokeRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(ExhaustRootName);
            if (existing != null)
            {
                smokeRoot = existing;
                return;
            }

            GameObject rootObject = new GameObject(ExhaustRootName);
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            smokeRoot = rootObject.transform;
        }

        private ParticleSystem CreateEmitter(int index, Vector3 localPosition)
        {
            GameObject emitterObject = new GameObject($"ExhaustSmoke_{index + 1}");
            emitterObject.transform.SetParent(smokeRoot, false);
            emitterObject.transform.localPosition = localPosition;
            emitterObject.transform.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            emitterObject.transform.localScale = Vector3.one;

            ParticleSystem particleSystem = emitterObject.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particleSystem);
            return particleSystem;
        }

        private void ConfigureParticleSystem(ParticleSystem particleSystem)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed * 0.55f, startSpeed * 1.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.75f, startSize * 1.25f);
            main.startColor = smokeColor;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad);
            main.startRotationY = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);
            main.maxParticles = preset == VehicleExhaustSmokePreset.Truck ? 96 : 64;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = idleEmissionRate;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;
            shape.length = 0.18f;
            shape.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.34f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

            ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, endSizeMultiplier));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            Color start = smokeColor;
            Color end = new Color(smokeColor.r + 0.12f, smokeColor.g + 0.12f, smokeColor.b + 0.12f, 0f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(smokeColor.a, 0f),
                    new GradientAlphaKey(smokeColor.a * 0.55f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.25f;
            noise.damping = true;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetSmokeMesh();
            renderer.material = GetSmokeMaterial();
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.sortingFudge = -0.2f;

            particleSystem.Play();
        }

        private void SetEmissionEnabled(bool enabled)
        {
            if (emitters == null)
            {
                return;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                ParticleSystem emitter = emitters[i];
                if (emitter == null) continue;

                ParticleSystem.EmissionModule emission = emitter.emission;
                emission.enabled = enabled;
            }
        }

        private static Material GetSmokeMaterial()
        {
            if (sharedSmokeMaterial != null)
            {
                return sharedSmokeMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            sharedSmokeMaterial = new Material(shader)
            {
                name = "Runtime_LowPolyExhaustSmoke_URP",
                renderQueue = (int)RenderQueue.Transparent
            };

            sharedSmokeMaterial.SetColor("_BaseColor", Color.white);
            sharedSmokeMaterial.SetColor("_Color", Color.white);
            sharedSmokeMaterial.SetFloat("_Surface", 1f);
            sharedSmokeMaterial.SetFloat("_Blend", 0f);
            sharedSmokeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            sharedSmokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            sharedSmokeMaterial.SetFloat("_ZWrite", 0f);
            sharedSmokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            sharedSmokeMaterial.EnableKeyword("_ALPHABLEND_ON");

            return sharedSmokeMaterial;
        }

        private static Mesh GetSmokeMesh()
        {
            if (sharedSmokeMesh != null)
            {
                return sharedSmokeMesh;
            }

            sharedSmokeMesh = new Mesh
            {
                name = "Runtime_LowPolySmokeCloudPuff"
            };

            List<Vector3> vertices = new List<Vector3>(42);
            List<int> triangles = new List<int>(56);
            AddLowPolyLobe(vertices, triangles, new Vector3(-0.34f, 0f, 0.02f), new Vector3(0.44f, 0.38f, 0.34f));
            AddLowPolyLobe(vertices, triangles, new Vector3(0.02f, 0.12f, 0.02f), new Vector3(0.56f, 0.48f, 0.42f));
            AddLowPolyLobe(vertices, triangles, new Vector3(0.42f, 0.02f, 0.03f), new Vector3(0.38f, 0.34f, 0.3f));
            AddLowPolyLobe(vertices, triangles, new Vector3(-0.02f, -0.16f, -0.02f), new Vector3(0.42f, 0.3f, 0.36f));
            AddLowPolyLobe(vertices, triangles, new Vector3(-0.6f, -0.04f, -0.01f), new Vector3(0.26f, 0.23f, 0.22f));
            AddLowPolyLobe(vertices, triangles, new Vector3(0.68f, 0f, 0f), new Vector3(0.24f, 0.22f, 0.2f));

            sharedSmokeMesh.SetVertices(vertices);
            sharedSmokeMesh.SetTriangles(triangles, 0);
            sharedSmokeMesh.RecalculateNormals();
            sharedSmokeMesh.RecalculateBounds();
            return sharedSmokeMesh;
        }

        private static void AddLowPolyLobe(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 scale)
        {
            int start = vertices.Count;
            vertices.Add(center + new Vector3(0f, scale.y, 0f));
            vertices.Add(center + new Vector3(scale.x, 0f, 0f));
            vertices.Add(center + new Vector3(0f, 0f, scale.z));
            vertices.Add(center + new Vector3(-scale.x, 0f, 0f));
            vertices.Add(center + new Vector3(0f, 0f, -scale.z));
            vertices.Add(center + new Vector3(0f, -scale.y, 0f));

            triangles.AddRange(new[]
            {
                start + 0, start + 1, start + 2,
                start + 0, start + 2, start + 3,
                start + 0, start + 3, start + 4,
                start + 0, start + 4, start + 1,
                start + 5, start + 2, start + 1,
                start + 5, start + 3, start + 2,
                start + 5, start + 4, start + 3,
                start + 5, start + 1, start + 4
            });
        }
    }
}
