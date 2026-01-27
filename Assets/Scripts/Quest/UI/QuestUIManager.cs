using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
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
        [SerializeField] private float distanceUpdateThresholdMeters = 1f;

        private QuestManager questManager;
        private bool isSubscribed;
        private float lastDistanceSqr = -1f;
        private QuestLocation lastObjective;

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
            questManager = QuestManager.Instance;
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
                HandleQuestStarted(questManager.CurrentQuest);
                HandleQuestUpdated(questManager.CurrentQuest);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromQuestEvents();
        }

        private void Update()
        {
            HandleQuestListToggleInput();

            if (questManager == null || questManager.CurrentQuest == null)
            {
                return;
            }

            UpdateActiveQuestDistance(questManager.CurrentQuest);
            UpdateCargoHealth(questManager.CurrentQuest);
        }

        private void HandleQuestListToggleInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                questListUI?.TogglePanel();
            }
#else
            if (Input.GetKeyDown(KeyCode.Q))
            {
                questListUI?.TogglePanel();
            }
#endif
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

            if (activeQuestUI != null)
            {
                activeQuestUI.Show();
                activeQuestUI.UpdateQuestDisplay(quest);
                lastDistanceSqr = -1f;
                lastObjective = null;
                UpdateActiveQuestDistance(quest);
                UpdateCargoHealth(quest);
            }
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
                questCompleteUI.ShowCompleteScreen(quest, reward);
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
                questCompleteUI.ShowFailedScreen(quest, reason);
            }

            RefreshQuestList();
        }

        private void HandleQuestUpdated(QuestData quest)
        {
            if (activeQuestUI == null)
            {
                return;
            }

            activeQuestUI.UpdateQuestDisplay(quest);
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

            if (questManager != null && questManager.PlayerTransform != null)
            {
                playerTransform = questManager.PlayerTransform;
                return;
            }

            CarController controller = FindAnyObjectByType<CarController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
        }

        private void UpdateActiveQuestDistance(QuestData quest)
        {
            if (activeQuestUI == null || playerTransform == null || quest == null)
            {
                return;
            }

            QuestLocation target = GetCurrentObjective(quest);
            if (target == null)
            {
                activeQuestUI.UpdateDistance(0f);
                lastDistanceSqr = -1f;
                lastObjective = null;
                return;
            }

            Vector3 delta = playerTransform.position - target.Position;
            float distanceSqr = delta.sqrMagnitude;
            float thresholdSqr = distanceUpdateThresholdMeters * distanceUpdateThresholdMeters;

            if (target != lastObjective || lastDistanceSqr < 0f || Mathf.Abs(distanceSqr - lastDistanceSqr) >= thresholdSqr)
            {
                float distance = Mathf.Sqrt(distanceSqr);
                activeQuestUI.UpdateDistance(distance);
                lastDistanceSqr = distanceSqr;
                lastObjective = target;
            }
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
    }
}
