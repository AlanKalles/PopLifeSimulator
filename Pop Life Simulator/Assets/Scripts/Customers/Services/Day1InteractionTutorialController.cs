using PixelCrushers.DialogueSystem;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Data;
using PopLife.Manager;
using PopLife.Runtime;
using UnityEngine;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// Day 1 专用顾客问答教程：只在第三个自动生成顾客身上强制触发一次。
    /// </summary>
    public class Day1InteractionTutorialController : MonoBehaviour
    {
        public static Day1InteractionTutorialController Instance { get; private set; }

        [Header("Interaction")]
        [SerializeField] private InteractionEventData tutorialInteractionEvent;
        [Tooltip("Optional. If set, Day 1's 3rd auto-spawned customer will use this customer ID even if it is not unlocked in SpawnerProfile.")]
        [SerializeField] private string forcedDay1ThirdSpawnCustomerId;

        [Header("Dialogue")]
        [SerializeField] private string midoriIntroConversation = "Midori/5";
        [SerializeField] private string midoriResultConversation = "Midori/6";

        [Header("Timing")]
        [SerializeField] private float spotlightDelaySeconds = 0.4f;
        [SerializeField] private float clickWindowSeconds = 10f;

        [Header("Debug")]
        [SerializeField] private string forcedCustomerId;
        [SerializeField] private bool day1OverrideActive;

        private readonly Day1InteractionTutorialState state = new();

        public InteractionEventData TutorialInteractionEvent => tutorialInteractionEvent;
        public string ForcedDay1ThirdSpawnCustomerId => forcedDay1ThirdSpawnCustomerId;
        public string MidoriIntroConversation => midoriIntroConversation;
        public string MidoriResultConversation => midoriResultConversation;
        public float SpotlightDelaySeconds => spotlightDelaySeconds;
        public float ClickWindowSeconds => clickWindowSeconds;

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
            CustomerEventBus.OnCustomerEnteredStore += HandleCustomerEnteredStore;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += HandleBuildPhaseStart;
                DayLoopManager.Instance.OnDayChanged += HandleDayChanged;
            }

            RefreshDebugState();
        }

        private void OnDisable()
        {
            CustomerEventBus.OnCustomerEnteredStore -= HandleCustomerEnteredStore;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= HandleBuildPhaseStart;
                DayLoopManager.Instance.OnDayChanged -= HandleDayChanged;
            }

            if (Instance == this) Instance = null;
        }

        private void HandleCustomerEnteredStore(CustomerAgent agent)
        {
            if (agent == null) return;
            if (agent.bb != null && agent.bb.visitPurpose != CustomerVisitPurpose.PlayerStore) return;

            if (state.IsForcedCustomer(agent.customerID))
            {
                Debug.Log($"[Day1InteractionTutorial] 教程顾客已进入 PlayerStore: {agent.customerID}");
            }
            else if (string.IsNullOrWhiteSpace(forcedDay1ThirdSpawnCustomerId))
            {
                int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 0;
                bool forced = state.RecordCustomerEntered(agent.customerID, day, HasCompletedFirstRequest());
                if (forced)
                {
                    Debug.Log($"[Day1InteractionTutorial] 第三个进店顾客已锁定: {agent.customerID}");
                }
            }

            RefreshDebugState();
        }

        private void HandleBuildPhaseStart()
        {
            int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 0;
            if (day == 1 && !HasCompletedFirstRequest())
            {
                state.ResetForNewDay();
            }

            RefreshDebugState();
        }

        private void HandleDayChanged(int day)
        {
            if (day != 1)
            {
                state.MarkCompleted();
            }

            RefreshDebugState();
        }

        public bool TryResolveInteraction(CustomerAgent agent, ShelfInstance shelf,
            out InteractionEventData interactionEvent, out bool handled)
        {
            interactionEvent = null;
            handled = false;

            if (DayLoopManager.Instance == null || DayLoopManager.Instance.currentDay != 1)
            {
                return false;
            }

            // Day 1 override：教程完成前后都屏蔽 Day 1 普通 interaction。
            handled = true;
            if (HasCompletedFirstRequest())
            {
                return true;
            }

            if (agent != null && state.IsForcedCustomer(agent.customerID))
            {
                interactionEvent = tutorialInteractionEvent;
            }

            return true;
        }

        public bool IsTutorialInteraction(CustomerAgent agent, InteractionEventData interactionEvent)
        {
            return agent != null
                && interactionEvent != null
                && interactionEvent == tutorialInteractionEvent
                && state.IsForcedCustomer(agent.customerID);
        }

        public bool ShouldForceDay1ThirdSpawn(int upcomingSpawnNumber)
        {
            return upcomingSpawnNumber == Day1InteractionTutorialState.RequiredEnteredCustomerIndex
                && !string.IsNullOrWhiteSpace(forcedDay1ThirdSpawnCustomerId)
                && DayLoopManager.Instance != null
                && DayLoopManager.Instance.currentDay == 1
                && !HasCompletedFirstRequest();
        }

        public bool MarkForcedDay1CustomerSpawned(string customerId)
        {
            int day = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 0;
            bool marked = state.RecordForcedCustomerSpawned(customerId, day, HasCompletedFirstRequest());
            if (marked)
            {
                Debug.Log($"[Day1InteractionTutorial] 第三个自动生成顾客已锁定: {customerId}");
            }

            RefreshDebugState();
            return marked;
        }

        public bool IsDay1ForcedSpawnCustomerIdReserved(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId)
                || string.IsNullOrWhiteSpace(forcedDay1ThirdSpawnCustomerId)
                || DayLoopManager.Instance == null
                || DayLoopManager.Instance.currentDay != 1)
            {
                return false;
            }

            if (customerId != forcedDay1ThirdSpawnCustomerId.Trim())
            {
                return false;
            }

            // Day 1 before the tutorial completes: reserve this ID for spawn #3 only.
            // After completion: keep it reserved for the rest of Day 1 so it cannot
            // appear again through the normal pool or manual spawn.
            return !HasCompletedFirstRequest() || state.HasForcedCustomer;
        }

        public void CompleteTutorial(Day1InteractionTutorialResult result)
        {
            state.MarkCompleted();
            GameStateManager.Instance?.NotifyFirstRequestCompleted();

            DialogueLua.SetVariable("Day1InteractionResult", result.ToString());
            DialogueLua.SetVariable("Day1InteractionCorrect", result == Day1InteractionTutorialResult.Correct);
            DialogueLua.SetVariable("Day1InteractionIncorrect", result == Day1InteractionTutorialResult.Incorrect);
            DialogueLua.SetVariable("Day1InteractionMissed", result == Day1InteractionTutorialResult.Missed);
            DialogueLua.SetVariable("InteractionCorrect", result == Day1InteractionTutorialResult.Correct);
            DialogueLua.SetVariable("InteractionIncorrect", result == Day1InteractionTutorialResult.Incorrect);
            DialogueLua.SetVariable("InteractionMissed", result == Day1InteractionTutorialResult.Missed);

            RefreshDebugState();
        }

        private static bool HasCompletedFirstRequest()
        {
            return GameStateManager.Instance != null && GameStateManager.Instance.hasCompletedFirstRequest;
        }

        private void RefreshDebugState()
        {
            forcedCustomerId = state.ForcedCustomerId;
            day1OverrideActive = DayLoopManager.Instance != null
                && DayLoopManager.Instance.currentDay == 1
                && !HasCompletedFirstRequest();
        }
    }

    public enum Day1InteractionTutorialResult
    {
        Correct,
        Incorrect,
        Missed
    }
}
