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
            EnsureComponentExists<PlayerProgressionManager>("PlayerProgressionManager");
            EnsureComponentExists<DriverProgressionSystem>("DriverProgressionSystem");
            EnsureComponentExists<ProgressionSkillTreeUI>("ProgressionSkillTreeUI");
        }

        private static void EnsureComponentExists<T>(string objectName) where T : Component
        {
            if (FindAnyObjectByType<T>() != null)
            {
                return;
            }

            GameObject host = new GameObject(objectName);
            host.AddComponent<T>();
        }
    }
}
