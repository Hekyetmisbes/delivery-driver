using UnityEngine;

namespace DeliveryDriver.UIKit
{
    /// <summary>
    /// Visual grouping utility for tabs and smartphone app panels.
    /// Hook existing buttons to ShowByIndex from the Unity Inspector.
    /// </summary>
    public sealed class DDUIScreenSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject[] panels;
        [SerializeField] private int initialIndex;
        private void Awake() { ShowByIndex(initialIndex); }
        public void ShowByIndex(int index)
        {
            if (panels == null) return;
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null) panels[i].SetActive(i == index);
        }
        public void HideAll()
        {
            if (panels == null) return;
            foreach (var panel in panels)
                if (panel != null) panel.SetActive(false);
        }
    }
}
