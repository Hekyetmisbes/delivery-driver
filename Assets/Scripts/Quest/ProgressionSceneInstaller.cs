using DeliveryDriver.Quest.UI;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Ensures progression systems and progression/skill-tree UI are present in scene.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public class ProgressionSceneInstaller : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerProgressionManager playerProgressionManager;
        [SerializeField] private DriverProgressionSystem driverProgressionSystem;
        [SerializeField] private ProgressionSkillTreeUI progressionSkillTreeUI;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstaller()
        {
            if (FindAnyObjectByType<ProgressionSceneInstaller>() != null)
            {
                return;
            }

            GameObject installerObject = new GameObject("ProgressionSceneInstaller");
            installerObject.AddComponent<ProgressionSceneInstaller>();
        }

        private void Awake()
        {
            EnsureComponentExists(ref playerProgressionManager, "PlayerProgressionManager");
            EnsureComponentExists(ref driverProgressionSystem, "DriverProgressionSystem");
            EnsureComponentExists(ref progressionSkillTreeUI, "ProgressionSkillTreeUI");
        }

        public bool AutoAssignStartupReferences()
        {
            bool changed = false;
            changed |= AutoAssignReference(ref playerProgressionManager);
            changed |= AutoAssignReference(ref driverProgressionSystem);
            changed |= AutoAssignReference(ref progressionSkillTreeUI);
            return changed;
        }

        private static bool AutoAssignReference<T>(ref T reference) where T : Component
        {
            T found = FindAnyObjectByType<T>();
            if (reference == found)
            {
                return false;
            }

            reference = found;
            return true;
        }

        private static void EnsureComponentExists<T>(ref T reference, string objectName) where T : Component
        {
            if (reference != null)
            {
                return;
            }

            reference = FindAnyObjectByType<T>();
            if (reference != null)
            {
                return;
            }

            GameObject host = new GameObject(objectName);
            reference = host.AddComponent<T>();
        }
    }
}
