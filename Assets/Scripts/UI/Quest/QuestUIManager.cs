using UnityEngine;
using TrafficSystem;

namespace DeliveryDriver.Quest.UI
{
    public class QuestUIManager : MonoBehaviour
    {
        public static QuestUIManager Instance { get; private set; }

        [Header("UI Panels")]
        [SerializeField] private QuestListUI questListUI;
        [SerializeField] private ActiveQuestUI activeQuestUI;
        [SerializeField] private QuestCompleteUI questCompleteUI;

        [Header("Player")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private QuestManager questManager;
        [SerializeField] private DeliveryManager deliveryManager;
        private bool isSubscribed;
        private QuestLocation lastObjective;
        private float nextHudRefreshTime;
        private Vector3 lastDistanceRefreshPosition = Vector3.positiveInfinity;
        private const float HudRefreshInterval = 0.2f;
        private const float DistanceRefreshThreshold = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (questManager == null)
            {
                questManager = QuestManager.Instance;
            }

            SubscribeToQuestEvents();
        }

        private void Start()
        {
            if (questManager == null)
            {
                questManager = QuestManager.Instance;
                SubscribeToQuestEvents();
            }

            ResolvePlayerTransform();
            ResolveDeliveryManager();

            if (questCompleteUI != null)
            {
                questCompleteUI.SetContinueAction(HandleContinue);
                questCompleteUI.Hide();
            }

            if (activeQuestUI != null)
            {
                activeQuestUI.Hide();
            }

            RefreshQuestList();

            if (questManager != null && questManager.CurrentQuest != null)
            {
                SyncActiveQuestVisibility(questManager.CurrentQuest);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromQuestEvents();
        }

        [ContextMenu("Auto Assign Startup References")]
        private void AutoAssignStartupReferencesFromContextMenu()
        {
            AutoAssignStartupReferences();
        }

        public bool AutoAssignStartupReferences()
        {
            bool changed = false;

            if (questManager == null)
            {
                questManager = QuestManager.Instance ?? FindSceneComponent<QuestManager>();
                changed |= questManager != null;
            }

            if (deliveryManager == null)
            {
                deliveryManager = FindSceneComponent<DeliveryManager>();
                changed |= deliveryManager != null;
            }

            if (questListUI == null)
            {
                questListUI = FindSceneComponent<QuestListUI>();
                changed |= questListUI != null;
            }

            if (activeQuestUI == null)
            {
                activeQuestUI = FindSceneComponent<ActiveQuestUI>();
                changed |= activeQuestUI != null;
            }

            if (questCompleteUI == null)
            {
                questCompleteUI = FindSceneComponent<QuestCompleteUI>();
                changed |= questCompleteUI != null;
            }

            if (playerTransform == null)
            {
                if (questManager != null && questManager.PlayerTransform != null)
                {
                    playerTransform = questManager.PlayerTransform;
                }
                else
                {
                    CarController controller = FindSceneComponent<CarController>();
                    if (controller != null)
                    {
                        playerTransform = controller.transform;
                    }
                }

                changed |= playerTransform != null;
            }

            return changed;
        }

        private void Update()
        {
            if (questManager == null || questManager.CurrentQuest == null)
            {
                return;
            }

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
            }

            QuestData currentQuest = questManager.CurrentQuest;
            SyncActiveQuestVisibility(currentQuest);

            if (activeQuestUI != null)
            {
                if (!activeQuestUI.gameObject.activeSelf)
                {
                    return;
                }

                activeQuestUI.UpdateTimer(currentQuest.TimeRemaining, currentQuest.TimeLimit);
            }

            if (Time.unscaledTime < nextHudRefreshTime && !ShouldForceDistanceRefresh())
            {
                return;
            }

            nextHudRefreshTime = Time.unscaledTime + HudRefreshInterval;
            UpdateActiveQuestDistance(currentQuest);
            UpdateCargoHealth(currentQuest);
        }

        private void SubscribeToQuestEvents()
        {
            if (questManager == null)
            {
                return;
            }

            if (isSubscribed)
            {
                return;
            }

            questManager.OnQuestStarted.AddListener(HandleQuestStarted);
            questManager.OnQuestCompleted.AddListener(HandleQuestCompleted);
            questManager.OnQuestFailed.AddListener(HandleQuestFailed);
            questManager.OnQuestUpdated.AddListener(HandleQuestUpdated);
            questManager.OnDrivingFeedback.AddListener(HandleDrivingFeedback);
            isSubscribed = true;
        }

        private void UnsubscribeFromQuestEvents()
        {
            if (questManager == null)
            {
                return;
            }

            questManager.OnQuestStarted.RemoveListener(HandleQuestStarted);
            questManager.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
            questManager.OnQuestFailed.RemoveListener(HandleQuestFailed);
            questManager.OnQuestUpdated.RemoveListener(HandleQuestUpdated);
            questManager.OnDrivingFeedback.RemoveListener(HandleDrivingFeedback);
            isSubscribed = false;
        }

        public void RefreshQuestList()
        {
            if (questListUI == null || questManager == null)
            {
                return;
            }

            questListUI.PopulateQuestList(new System.Collections.Generic.List<QuestData>(questManager.AvailableQuests));
        }

        private void HandleQuestStarted(QuestData quest)
        {
            if (questListUI != null)
            {
                questListUI.SetOpen(false);
            }

            SyncActiveQuestVisibility(quest);
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (activeQuestUI != null)
            {
                activeQuestUI.Hide();
            }

            if (questCompleteUI != null)
            {
                int reward = questManager != null ? questManager.LastCompletionReward : quest.CalculateFinalReward();
                QuestManager.RewardPenaltyBreakdown? breakdown = questManager != null
                    ? questManager.LastQuestBreakdown
                    : null;
                questCompleteUI.ShowCompleteScreen(quest, reward, breakdown);
            }

            RefreshQuestList();
        }

        private void HandleQuestFailed(QuestData quest)
        {
            if (activeQuestUI != null)
            {
                activeQuestUI.Hide();
            }

            if (questCompleteUI != null)
            {
                string reason = questManager != null ? questManager.LastFailureReason : string.Empty;
                int penalty = questManager != null ? questManager.LastFailurePenalty : 0;
                QuestManager.RewardPenaltyBreakdown? breakdown = questManager != null
                    ? questManager.LastQuestBreakdown
                    : null;
                questCompleteUI.ShowFailedScreen(quest, reason, penalty, breakdown);
            }

            RefreshQuestList();
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (activeQuestUI == null)
            {
                return;
            }

            SyncActiveQuestVisibility(quest);
        }

        private void HandleDrivingFeedback(string message, int scoreDelta)
        {
            if (activeQuestUI == null)
            {
                return;
            }

            activeQuestUI.ShowDrivingFeedback(message, scoreDelta);
        }

        private void HandleContinue()
        {
            RefreshQuestList();
        }

        private void ResolvePlayerTransform()
        {
            if (playerTransform != null)
            {
                return;
            }

            CarController controller = FindAnyObjectByType<CarController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
                return;
            }

            if (questManager != null && questManager.PlayerTransform != null)
            {
                playerTransform = questManager.PlayerTransform;
                return;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerTransform = taggedPlayer.transform;
                return;
            }

            if (Camera.main != null)
            {
                playerTransform = Camera.main.transform.root;
            }
        }

        private void ResolveDeliveryManager()
        {
            if (deliveryManager != null)
            {
                return;
            }

            deliveryManager = FindAnyObjectByType<DeliveryManager>();
        }

        private bool ShouldShowActiveQuestUI(QuestData quest)
        {
            if (quest == null)
            {
                return false;
            }

            ResolveDeliveryManager();
            if (deliveryManager == null)
            {
                return true;
            }

            if (deliveryManager.HasObjectivePoint)
            {
                return true;
            }

            // Keep panel visible for active quests even if delivery flow flags
            // momentarily desync during mission-offer transitions.
            return quest.Status == QuestStatus.Active;
        }

        private void SyncActiveQuestVisibility(QuestData quest)
        {
            if (activeQuestUI == null)
            {
                return;
            }

            bool shouldShow = ShouldShowActiveQuestUI(quest);
            if (!shouldShow)
            {
                activeQuestUI.Hide();
                return;
            }

            activeQuestUI.Show();
            activeQuestUI.UpdateQuestDisplay(quest);
            lastObjective = null;
            lastDistanceRefreshPosition = Vector3.positiveInfinity;
            nextHudRefreshTime = 0f;
            UpdateActiveQuestDistance(quest);
            UpdateCargoHealth(quest);
        }

        private void UpdateActiveQuestDistance(QuestData quest)
        {
            if (quest == null || activeQuestUI == null)
            {
                return;
            }

            if (playerTransform == null)
            {
                ResolvePlayerTransform();
            }

            if (playerTransform == null)
            {
                return;
            }

            QuestLocation target = GetCurrentObjective(quest);
            if (target == null)
            {
                activeQuestUI.UpdateDistance(0f);
                lastObjective = null;
                return;
            }

            Vector3 delta = playerTransform.position - target.Position;
            float distanceSqr = delta.sqrMagnitude;
            float distance = Mathf.Sqrt(distanceSqr);
            activeQuestUI.UpdateDistance(distance);
            lastObjective = target;
            lastDistanceRefreshPosition = playerTransform.position;
        }

        private void UpdateCargoHealth(QuestData quest)
        {
            if (activeQuestUI == null || quest == null)
            {
                return;
            }

            if (quest.Cargo != null && quest.Cargo.IsFragile)
            {
                activeQuestUI.UpdateCargoHealth(quest.Cargo.CargoHealth);
            }
        }

        private QuestLocation GetCurrentObjective(QuestData quest)
        {
            if (quest == null)
            {
                return null;
            }

            if (!quest.HasPickedUpCargo)
            {
                return quest.PickupLocation;
            }

            if (quest.DeliveryLocations == null || quest.DeliveryLocations.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(quest.CurrentDeliveryIndex, 0, quest.DeliveryLocations.Count - 1);
            return quest.DeliveryLocations[index];
        }

        private bool ShouldForceDistanceRefresh()
        {
            if (playerTransform == null)
            {
                return true;
            }

            if (lastDistanceRefreshPosition.x == float.PositiveInfinity)
            {
                return true;
            }

            return (playerTransform.position - lastDistanceRefreshPosition).sqrMagnitude >= DistanceRefreshThreshold * DistanceRefreshThreshold;
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return components != null && components.Length > 0 ? components[0] : null;
        }
    }
}
