using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Manager;

namespace PopLife.NarrativeSystem
{
    /// <summary>
    /// 叙事管理器，控制所有对话流程
    /// Narrative Manager controlling all conversation flows
    /// </summary>
    public class NarrativeManager : MonoBehaviour
    {
        public static NarrativeManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private bool enableNarratives = true;
        [SerializeField] private float defaultSegmentDelay = 0.5f;  // 片段切换默认延迟

        [Header("References")]
        [SerializeField] private UI.FanConversationPanel conversationPanel;  // 扇形对话面板

        [Header("Narrative Database")]
        [SerializeField] private List<NarrativeSO> availableNarratives;  // 可用的叙事列表

        // Runtime state
        private NarrativeSequence activeSequence;
        private NarrativeSO activeNarrative;
        private bool isNarrativeActive;
        private Queue<NarrativeSO> narrativeQueue;

        // Properties
        public NarrativeSequence ActiveSequence => activeSequence;
        public bool IsNarrativeActive => isNarrativeActive;

        // Events
        public event Action<NarrativeSO> OnNarrativeStarted;
        public event Action<NarrativeSO> OnNarrativeCompleted;
        public event Action<NarrativeSegment> OnSegmentDisplayed;
        public event Action<List<NarrativeSegment>> OnChoicesAvailable;

        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            narrativeQueue = new Queue<NarrativeSO>();
            LoadAvailableNarratives();
        }

        private void OnEnable()
        {
            // Subscribe to tutorial markers if needed
            TutorialEventBus.OnMarkerTriggered += OnTutorialMarkerTriggered;
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= OnTutorialMarkerTriggered;
        }

        /// <summary>
        /// 加载所有可用的叙事资源
        /// </summary>
        private void LoadAvailableNarratives()
        {
            // Load from Resources folder
            var narratives = Resources.LoadAll<NarrativeSO>("NarrativeData");
            availableNarratives = new List<NarrativeSO>(narratives);

            Debug.Log($"[NarrativeManager] Loaded {availableNarratives.Count} narratives");
        }

        /// <summary>
        /// 开始叙事
        /// </summary>
        public bool StartNarrative(string narrativeID)
        {
            if (!enableNarratives)
            {
                Debug.Log("[NarrativeManager] Narratives are disabled");
                return false;
            }

            if (isNarrativeActive)
            {
                Debug.Log("[NarrativeManager] Another narrative is already active");
                QueueNarrative(narrativeID);
                return false;
            }

            // Find narrative by ID
            var narrative = availableNarratives.Find(n => n.NarrativeID == narrativeID);
            if (narrative == null)
            {
                Debug.LogError($"[NarrativeManager] Narrative not found: {narrativeID}");
                return false;
            }

            return StartNarrative(narrative);
        }

        /// <summary>
        /// 开始叙事（使用ScriptableObject）
        /// </summary>
        public bool StartNarrative(NarrativeSO narrative)
        {
            if (narrative == null)
            {
                Debug.LogError("[NarrativeManager] Cannot start null narrative");
                return false;
            }

            if (!enableNarratives)
            {
                Debug.Log("[NarrativeManager] Narratives are disabled");
                return false;
            }

            if (isNarrativeActive)
            {
                narrativeQueue.Enqueue(narrative);
                Debug.Log($"[NarrativeManager] Queued narrative: {narrative.NarrativeName}");
                return false;
            }

            // Check trigger conditions
            int currentDay = DayLoopManager.Instance?.currentDay ?? 0;
            int currentFame = ResourceManager.Instance?.GetFame() ?? 0;
            bool isVIP = false; // TODO: Implement VIP check

            if (!narrative.CheckTriggerConditions(currentDay, currentFame, isVIP))
            {
                Debug.Log($"[NarrativeManager] Narrative {narrative.NarrativeName} conditions not met");
                return false;
            }

            // Create sequence instance
            activeSequence = narrative.CreateSequenceInstance();
            if (activeSequence == null)
            {
                Debug.LogError($"[NarrativeManager] Failed to create sequence for {narrative.NarrativeName}");
                return false;
            }

            // Setup
            activeNarrative = narrative;
            isNarrativeActive = true;

            // Subscribe to sequence events
            activeSequence.OnSegmentChanged += HandleSegmentChanged;
            activeSequence.OnSequenceCompleted += HandleSequenceCompleted;
            activeSequence.OnChoicesPresented += HandleChoicesPresented;

            // Show UI
            if (conversationPanel != null)
            {
                conversationPanel.ShowPanel(narrative);
                var segments = activeSequence.GetVisibleSegments();
                conversationPanel.DisplaySegments(segments.previous, segments.current, segments.next);
            }

            // Notify
            OnNarrativeStarted?.Invoke(narrative);

            Debug.Log($"[NarrativeManager] Started narrative: {narrative.NarrativeName}");
            return true;
        }

        /// <summary>
        /// 结束当前叙事
        /// </summary>
        public void EndCurrentNarrative()
        {
            if (!isNarrativeActive || activeNarrative == null)
            {
                Debug.Log("[NarrativeManager] No active narrative to end");
                return;
            }

            // Grant rewards
            GrantRewards(activeNarrative.CompletionRewards);

            // Cleanup
            if (activeSequence != null)
            {
                activeSequence.OnSegmentChanged -= HandleSegmentChanged;
                activeSequence.OnSequenceCompleted -= HandleSequenceCompleted;
                activeSequence.OnChoicesPresented -= HandleChoicesPresented;
            }

            // Hide UI
            if (conversationPanel != null)
            {
                conversationPanel.HidePanel();
            }

            // Notify
            var completedNarrative = activeNarrative;
            OnNarrativeCompleted?.Invoke(completedNarrative);

            // 检查是否需要触发后续教程标记
            // Check if we need to trigger follow-up tutorial markers
            TriggerFollowUpContent(completedNarrative.NarrativeID);

            // Reset state
            activeNarrative = null;
            activeSequence = null;
            isNarrativeActive = false;

            Debug.Log($"[NarrativeManager] Ended narrative: {completedNarrative.NarrativeName}");

            // Process queue
            ProcessNarrativeQueue();
        }

        /// <summary>
        /// 导航到下一个片段
        /// </summary>
        public void NavigateForward(int choiceIndex = 0)
        {
            if (!isNarrativeActive || activeSequence == null)
                return;

            if (activeSequence.NavigateForward(choiceIndex))
            {
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 导航到前一个片段
        /// </summary>
        public void NavigateBackward()
        {
            if (!isNarrativeActive || activeSequence == null)
                return;

            if (activeSequence.NavigateBackward())
            {
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            if (conversationPanel != null && activeSequence != null)
            {
                var segments = activeSequence.GetVisibleSegments();
                conversationPanel.DisplaySegments(segments.previous, segments.current, segments.next);
            }
        }

        /// <summary>
        /// 处理片段变化
        /// </summary>
        private void HandleSegmentChanged(NarrativeSegment newSegment)
        {
            OnSegmentDisplayed?.Invoke(newSegment);
        }

        /// <summary>
        /// 处理序列完成
        /// </summary>
        private void HandleSequenceCompleted()
        {
            if (conversationPanel != null)
            {
                conversationPanel.ShowFinishButton();
            }
        }

        /// <summary>
        /// 处理选择分支
        /// </summary>
        private void HandleChoicesPresented(List<NarrativeSegment> choices)
        {
            OnChoicesAvailable?.Invoke(choices);

            if (conversationPanel != null)
            {
                // Panel will handle choice display
                conversationPanel.PresentChoices(choices);
            }
        }

        /// <summary>
        /// 将叙事加入队列
        /// </summary>
        private void QueueNarrative(string narrativeID)
        {
            var narrative = availableNarratives.Find(n => n.NarrativeID == narrativeID);
            if (narrative != null)
            {
                narrativeQueue.Enqueue(narrative);
                Debug.Log($"[NarrativeManager] Queued narrative: {narrative.NarrativeName}");
            }
        }

        /// <summary>
        /// 处理叙事队列
        /// </summary>
        private void ProcessNarrativeQueue()
        {
            if (narrativeQueue.Count > 0 && !isNarrativeActive)
            {
                var nextNarrative = narrativeQueue.Dequeue();
                StartNarrative(nextNarrative);
            }
        }

        /// <summary>
        /// 根据完成的叙事ID触发后续内容
        /// Trigger follow-up content based on completed narrative ID
        /// </summary>
        private void TriggerFollowUpContent(string completedNarrativeID)
        {
            // 延迟触发后续内容，确保当前叙事完全结束
            // Delay triggering follow-up content to ensure current narrative is fully ended
            StartCoroutine(TriggerFollowUpContentDelayed(completedNarrativeID));
        }

        /// <summary>
        /// 延迟触发后续内容的协程
        /// Coroutine to trigger follow-up content with delay
        /// </summary>
        private System.Collections.IEnumerator TriggerFollowUpContentDelayed(string completedNarrativeID)
        {
            // 等待足够时间，确保面板淡出动画完成且OnNarrativeCompleted事件先被处理
            // Wait enough time to ensure panel fade out animation completes and OnNarrativeCompleted event is processed first
            // FanConversationPanel的fadeOutDuration是0.3秒，我们等待0.5秒确保完全结束
            yield return new WaitForSeconds(0.5f);

            // 定义叙事序列链接
            // Define narrative sequence connections
            switch (completedNarrativeID)
            {
                case "D001":
                    // D001 完成后，不再自动触发D002，等待玩家点击Build按钮
                    // After D001 completes, don't auto-trigger D002, wait for player to click Build button
                    Debug.Log("[NarrativeManager] D001 completed, D002 will trigger when player enters Build Mode");
                    // PopLife.Manager.TutorialEventBus.RaiseMarker(PopLife.Manager.TutorialMarker.Custom01);
                    break;

                case "D002":
                    // D002 完成后，触发 Custom02 标记，该标记映射到指引
                    // After D002 completes, trigger Custom02 marker which maps to guidance
                    Debug.Log("[NarrativeManager] D002 completed, triggering D002toGBUILDFIRSTSHELF for guidance");
                    PopLife.Manager.TutorialEventBus.RaiseMarker(PopLife.Manager.TutorialMarker.D002toGBUILDFIRSTSHELF);
                    break;

                // 可以在这里添加更多的序列链接
                // Add more sequence connections here
                default:
                    // 没有后续内容
                    // No follow-up content
                    break;
            }
        }

        /// <summary>
        /// 发放奖励
        /// </summary>
        private void GrantRewards(RewardData[] rewards)
        {
            if (rewards == null || rewards.Length == 0) return;

            foreach (var reward in rewards)
            {
                switch (reward.Type)
                {
                    case RewardData.RewardType.Money:
                        ResourceManager.Instance?.AddMoney(reward.Amount);
                        Debug.Log($"[NarrativeManager] Granted {reward.Amount} money");
                        break;

                    case RewardData.RewardType.Fame:
                        ResourceManager.Instance?.AddFame(reward.Amount);
                        Debug.Log($"[NarrativeManager] Granted {reward.Amount} fame");
                        break;

                    case RewardData.RewardType.Blueprint:
                        // TODO: Implement blueprint unlock
                        Debug.Log($"[NarrativeManager] Blueprint reward: {reward.RewardID}");
                        break;

                    case RewardData.RewardType.Customer:
                        // TODO: Implement customer unlock
                        Debug.Log($"[NarrativeManager] Customer reward: {reward.RewardID}");
                        break;

                    case RewardData.RewardType.Item:
                        // TODO: Implement item grant
                        Debug.Log($"[NarrativeManager] Item reward: {reward.RewardID}");
                        break;
                }
            }
        }

        /// <summary>
        /// 处理教程标记触发
        /// </summary>
        private void OnTutorialMarkerTriggered(TutorialMarker marker)
        {
            // Map tutorial markers to narratives if needed
            // This will be handled by TutorialContentDispatcher
        }

        /// <summary>
        /// 获取当前活跃的叙事
        /// </summary>
        public NarrativeSO GetActiveNarrative()
        {
            return activeNarrative;
        }

        /// <summary>
        /// 暂停/恢复叙事系统
        /// </summary>
        public void SetNarrativesEnabled(bool enabled)
        {
            enableNarratives = enabled;
        }

#if UNITY_EDITOR
        [ContextMenu("Test Start First Narrative")]
        private void TestStartFirstNarrative()
        {
            if (availableNarratives.Count > 0)
            {
                StartNarrative(availableNarratives[0]);
            }
        }
#endif
    }
}