#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using DeliveryDriver.Quest.UI;

namespace DeliveryDriver.Quest
{
    /// <summary>
    /// Debug menu for quest testing and tuning. Available in editor or development builds only.
    /// </summary>
    public class DebugQuestMenu : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private bool showMenu = false;
        [SerializeField] private int addMoneyAmount = 1000;
        [SerializeField] private int addXpAmount = 250;
        [SerializeField] private int unlockAllLevel = 50;
        [SerializeField] private Rect windowRect = new Rect(20, 20, 320, 420);

        private const string WindowTitle = "Quest Debug Menu";
        private const string DebugFailReason = "Debug fail";

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showMenu = !showMenu;
            }
        }

        private void OnGUI()
        {
            if (!showMenu)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, WindowTitle);
        }

        private void DrawWindow(int id)
        {
            QuestManager questManager = QuestManager.Instance;
            PlayerProgressionManager progressionManager = PlayerProgressionManager.Instance;
            QuestUIManager uiManager = QuestUIManager.Instance;

            GUILayout.Label("Quests", GUI.skin.label);

            if (GUILayout.Button("Complete Current Quest"))
            {
                questManager?.CompleteQuest(questManager.CurrentQuest);
            }

            if (GUILayout.Button("Fail Current Quest"))
            {
                questManager?.FailQuest(questManager.CurrentQuest, DebugFailReason);
            }

            if (GUILayout.Button("Teleport to Pickup/Delivery"))
            {
                questManager?.TeleportToActiveObjective();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Progression", GUI.skin.label);

            if (GUILayout.Button($"Add ${addMoneyAmount}"))
            {
                progressionManager?.AddMoney(addMoneyAmount);
            }

            if (GUILayout.Button($"Add {addXpAmount} XP"))
            {
                progressionManager?.AddXP(addXpAmount);
            }

            if (GUILayout.Button("Unlock All Quests"))
            {
                progressionManager?.SetLevelForDebug(unlockAllLevel);
                questManager?.RefreshAvailableQuests();
                uiManager?.RefreshQuestList();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Toggles", GUI.skin.label);

            if (questManager != null)
            {
                bool infiniteTime = GUILayout.Toggle(questManager.DebugInfiniteTimeEnabled, "Infinite Time");
                if (infiniteTime != questManager.DebugInfiniteTimeEnabled)
                {
                    questManager.SetDebugInfiniteTime(infiniteTime);
                }

                bool invincibleCargo = GUILayout.Toggle(questManager.DebugInvincibleCargoEnabled, "Invincible Cargo");
                if (invincibleCargo != questManager.DebugInvincibleCargoEnabled)
                {
                    questManager.SetDebugInvincibleCargo(invincibleCargo);
                }

                bool drawGizmos = GUILayout.Toggle(questManager.DebugDrawGizmosEnabled, "Show Quest Gizmos");
                if (drawGizmos != questManager.DebugDrawGizmosEnabled)
                {
                    questManager.SetDebugDrawGizmos(drawGizmos);
                }

                bool drawRoute = GUILayout.Toggle(questManager.DebugDrawRouteEnabled, "Show Route Lines");
                if (drawRoute != questManager.DebugDrawRouteEnabled)
                {
                    questManager.SetDebugDrawRoute(drawRoute);
                }

                bool drawLabels = GUILayout.Toggle(questManager.DebugDrawLabelsEnabled, "Show Quest Labels");
                if (drawLabels != questManager.DebugDrawLabelsEnabled)
                {
                    questManager.SetDebugDrawLabels(drawLabels);
                }
            }
            else
            {
                GUILayout.Label("QuestManager not found in scene.");
            }

            GUI.DragWindow();
        }
    }
}
#endif
