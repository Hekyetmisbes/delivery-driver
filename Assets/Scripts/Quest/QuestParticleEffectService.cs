using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    internal sealed class QuestParticleEffectService
    {
        private readonly MonoBehaviour coroutineRunner;
        private readonly GameObject questMarkerParticlePrefab;
        private readonly GameObject pickupEffectPrefab;
        private readonly GameObject deliveryEffectPrefab;
        private readonly GameObject damageEffectPrefab;
        private readonly GameObject levelUpEffectPrefab;
        private readonly int particlePoolSize;

        private readonly Queue<GameObject> particlePool = new Queue<GameObject>();

        public QuestParticleEffectService(
            MonoBehaviour coroutineRunner,
            GameObject questMarkerParticlePrefab,
            GameObject pickupEffectPrefab,
            GameObject deliveryEffectPrefab,
            GameObject damageEffectPrefab,
            GameObject levelUpEffectPrefab,
            int particlePoolSize)
        {
            this.coroutineRunner = coroutineRunner;
            this.questMarkerParticlePrefab = questMarkerParticlePrefab;
            this.pickupEffectPrefab = pickupEffectPrefab;
            this.deliveryEffectPrefab = deliveryEffectPrefab;
            this.damageEffectPrefab = damageEffectPrefab;
            this.levelUpEffectPrefab = levelUpEffectPrefab;
            this.particlePoolSize = particlePoolSize;
        }

        public GameObject PickupEffectPrefab => pickupEffectPrefab;
        public GameObject DeliveryEffectPrefab => deliveryEffectPrefab;
        public GameObject DamageEffectPrefab => damageEffectPrefab;

        public void InitializeParticlePool()
        {
            particlePool.Clear();

            GameObject[] prefabs = { pickupEffectPrefab, deliveryEffectPrefab, damageEffectPrefab, levelUpEffectPrefab };
            int divisor = Mathf.Max(1, prefabs.Length);

            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                for (int i = 0; i < particlePoolSize / divisor; i++)
                {
                    GameObject particle = Object.Instantiate(prefab);
                    particle.SetActive(false);
                    particlePool.Enqueue(particle);
                }
            }

            Debug.Log($"[QuestManager] Particle pool initialized with {particlePool.Count} objects.");
        }

        public void PlayParticleEffect(GameObject effectPrefab, Vector3 position)
        {
            if (effectPrefab == null)
            {
                return;
            }

            GameObject effect = particlePool.Count > 0 ? particlePool.Dequeue() : null;
            if (effect == null)
            {
                effect = Object.Instantiate(effectPrefab, position, Quaternion.identity);
            }
            else
            {
                effect.transform.SetPositionAndRotation(position, Quaternion.identity);
                effect.SetActive(true);
            }

            ParticleSystem particleSystem = effect.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Play();
                float duration = particleSystem.main.duration + particleSystem.main.startLifetime.constantMax;
                coroutineRunner.StartCoroutine(ReturnParticleToPool(effect, duration));
                return;
            }

            coroutineRunner.StartCoroutine(ReturnParticleToPool(effect, 5f));
        }

        public void PlayLevelUpEffect(Vector3 position)
        {
            PlayParticleEffect(levelUpEffectPrefab, position);
        }

        public void SpawnMarkerParticles(Vector3 position)
        {
            if (questMarkerParticlePrefab == null)
            {
                return;
            }

            GameObject markerParticle = Object.Instantiate(questMarkerParticlePrefab, position, Quaternion.identity);
            ParticleSystem particleSystem = markerParticle.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
        }

        private IEnumerator ReturnParticleToPool(GameObject particle, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (particle != null)
            {
                particle.SetActive(false);
                particlePool.Enqueue(particle);
            }
        }
    }
}
