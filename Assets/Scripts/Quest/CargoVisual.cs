using UnityEngine;

namespace DeliveryDriver.Quest
{
    public class CargoVisual : MonoBehaviour
    {
        [SerializeField] private Transform attachPoint;
        [SerializeField] private GameObject[] cargoModels;
        [SerializeField] private ParticleSystem damageEffect;
        [SerializeField] private AudioSource damageSound;

        private GameObject activeModel;

        public void AttachCargo(CargoData cargo)
        {
            if (attachPoint == null || cargoModels == null || cargoModels.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(GetModelIndex(cargo), 0, cargoModels.Length - 1);
            activeModel = cargoModels[index];

            if (activeModel == null)
            {
                return;
            }

            activeModel.transform.SetParent(attachPoint, false);
            activeModel.transform.localPosition = Vector3.zero;
            activeModel.transform.localRotation = Quaternion.identity;
            activeModel.SetActive(true);
        }

        public void DetachCargo()
        {
            if (activeModel == null)
            {
                return;
            }

            activeModel.transform.SetParent(null, true);
            activeModel.SetActive(false);
            activeModel = null;
        }

        public void PlayDamageEffect()
        {
            if (damageEffect != null)
            {
                damageEffect.Play();
            }

            if (damageSound != null)
            {
                damageSound.Play();
            }
        }

        private int GetModelIndex(CargoData cargo)
        {
            if (cargo == null || cargoModels == null || cargoModels.Length == 0)
            {
                return 0;
            }

            int hash = Mathf.Abs(cargo.CargoName.GetHashCode());
            return hash % cargoModels.Length;
        }
    }
}
