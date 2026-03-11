using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Data;
using PopLife.Manager;
using PopLife.UI.Quest;

namespace PopLife.Quest
{
    /// <summary>
    /// 任务逻辑管理器 - 单例
    /// 协调进度追踪、DDL检查、奖励发放、自动完成、持久化
    /// 挂载在场景中的管理器 GameObject 上
    /// </summary>
    [DefaultExecutionOrder(10)] // 确保在 QuestDataService 之后初始化
    public class QuestLogicManager : MonoBehaviour
    {
        public static QuestLogicManager Instance { get; private set; }

        [Header("配置")]
        [SerializeField] private bool debugMode = false;

        // 子系统
        private QuestProgressTracker tracker;

        // 已处理过奖励的任务集合（防止重复发放）
        private HashSet<string> rewardedQuests = new();

        // marker → questName 查找表（用于 marker 自动激活）
        private Dictionary<TutorialMarker, string> markerToQuest;

        // ES3 持久化
        private const string ES3_FILE = "QuestProgress.es3";
        private const string ES3_KEY_COUNTERS = "questCounters";
        private const string ES3_KEY_ACTIVATION = "questActivationDays";
        private const string ES3_KEY_REWARDED = "questRewarded";

        /// <summary>
        /// 任务被激活时触发
        /// </summary>
        public static event Action<string> OnQuestActivated;

        /// <summary>
        /// 任务完成时触发
        /// </summary>
        public static event Action<string> OnQuestCompleted;

        /// <summary>
        /// 任务失败时触发
        /// </summary>
        public static event Action<string> OnQuestFailed;

        #region Unity 生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // 初始化追踪器
            tracker = new QuestProgressTracker();
            tracker.SetDebugMode(debugMode);

            // 加载持久化数据
            LoadProgress();

            // 订阅事件
            tracker.OnEntryCompleted += HandleEntryCompleted;
            tracker.SubscribeEvents();

            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnQuestStateChanged += HandleQuestStateChanged;

            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnDayChanged += HandleDayChanged;

            // 构建 marker → quest 查找表并订阅事件
            BuildMarkerLookup();
            TutorialEventBus.OnMarkerTriggered += OnMarkerTriggered;

            // 初始扫描已激活的任务
            ScanActiveQuests();

            if (debugMode)
                Debug.Log("[QuestLogicManager] 初始化完成");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            SaveProgress();

            tracker?.UnsubscribeEvents();

            if (tracker != null)
                tracker.OnEntryCompleted -= HandleEntryCompleted;

            if (QuestDataService.Instance != null)
                QuestDataService.Instance.OnQuestStateChanged -= HandleQuestStateChanged;

            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnDayChanged -= HandleDayChanged;

            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;

            Instance = null;
        }

        #endregion

        #region 公共 API

        /// <summary>
        /// 手动激活任务（代码调用入口）
        /// </summary>
        public void ActivateQuest(string questName)
        {
            if (string.IsNullOrEmpty(questName)) return;

            var state = QuestLog.GetQuestState(questName);
            if (state == QuestState.Active)
            {
                if (debugMode) Debug.Log($"[QuestLogicManager] 任务已激活: {questName}");
                return;
            }

            QuestLog.SetQuestState(questName, QuestState.Active);
            QuestLog.SetQuestTracking(questName, true);

            if (debugMode)
                Debug.Log($"[QuestLogicManager] 激活任务: {questName}");
        }

        #endregion

        #region Marker 自动激活

        /// <summary>
        /// 构建 marker → questName 查找表
        /// </summary>
        private void BuildMarkerLookup()
        {
            markerToQuest = new Dictionary<TutorialMarker, string>();
            var allDefs = Resources.LoadAll<QuestDefinition>("ScriptableObjects/Quests");

            foreach (var def in allDefs)
            {
                if (def.ActivationMarker != TutorialMarker.None)
                {
                    if (!markerToQuest.TryAdd(def.ActivationMarker, def.QuestName))
                    {
                        Debug.LogWarning($"[QuestLogicManager] Duplicate marker {def.ActivationMarker} on quest: {def.QuestName}");
                    }
                }
            }
        }

        /// <summary>
        /// 当 marker 触发时，自动激活对应的任务
        /// </summary>
        private void OnMarkerTriggered(TutorialMarker marker)
        {
            if (markerToQuest != null && markerToQuest.TryGetValue(marker, out var questName))
            {
                ActivateQuest(questName);
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 条目完成回调（来自 QuestProgressTracker）
        /// </summary>
        private void HandleEntryCompleted(string questName, int entryNum)
        {
            // 检查该任务是否所有 Entry 都完成
            if (AreAllEntriesComplete(questName))
            {
                CompleteQuest(questName);
            }

            SaveProgress();
        }

        /// <summary>
        /// 任务状态变化回调（来自 QuestDataService）
        /// 用于检测新激活的任务（可能由 TutorialMarkerBridge/对话/Lua 触发）
        /// </summary>
        private void HandleQuestStateChanged()
        {
            string[] activeQuests = QuestLog.GetAllQuests(QuestState.Active);
            if (activeQuests == null) return;

            foreach (string questName in activeQuests)
            {
                if (!tracker.IsTracking(questName))
                {
                    tracker.StartTracking(questName);
                    OnQuestActivated?.Invoke(questName);

                    if (debugMode)
                        Debug.Log($"[QuestLogicManager] 检测到新激活任务: {questName}");
                }
            }

            // 检查外部完成的任务（可能由对话/Lua直接设置为success）
            CheckExternalCompletions();
        }

        /// <summary>
        /// 天数变化回调 → SurviveDays增量 → Daily计数器重置 → DDL检查
        /// </summary>
        private void HandleDayChanged(int newDay)
        {
            // 1. 先处理 SurviveDays 增量（在重置 daily 之前）
            tracker.ProcessDayChanged(newDay);

            // 2. 重置 Daily 计数器
            tracker.ResetDailyCounters();

            // 3. 检查所有 active 任务的 DDL
            CheckDeadlines();

            SaveProgress();
        }

        #endregion

        #region 任务完成/失败

        /// <summary>
        /// 完成任务：设置状态、发放奖励、触发事件
        /// </summary>
        private void CompleteQuest(string questName)
        {
            // 先标记已奖励，防止 SetQuestState 同步回调中 CheckExternalCompletions 重复触发
            bool shouldReward = !rewardedQuests.Contains(questName);
            if (shouldReward)
                rewardedQuests.Add(questName);

            // 设置任务状态为成功（会同步触发 OnQuestStateChange → CheckExternalCompletions）
            QuestLog.SetQuestState(questName, QuestState.Success);

            // 发放奖励
            if (shouldReward)
                QuestRewardDistributor.DistributeRewards(questName);

            // 停止追踪
            tracker.StopTracking(questName);

            // 触发事件
            OnQuestCompleted?.Invoke(questName);

            // 完成时立即触发 Marker（非 AfterToast 模式）
            var def = QuestDataService.Instance?.GetDefinition(questName);
            if (def != null && def.CompletionMarker != TutorialMarker.None && !def.TriggerMarkerAfterToast)
            {
                TutorialEventBus.RaiseMarker(def.CompletionMarker);
                if (debugMode)
                    Debug.Log($"[QuestLogicManager] 任务完成触发 Marker: {def.CompletionMarker}");
            }

            // 音效
            AudioManager.Instance?.PlaySound(AudioKeys.QUEST_COMPLETE);

            if (debugMode)
                Debug.Log($"[QuestLogicManager] 任务完成: {questName}");

            SaveProgress();
        }

        /// <summary>
        /// 任务失败（DDL 过期）
        /// </summary>
        private void FailQuest(string questName)
        {
            QuestLog.SetQuestState(questName, QuestState.Failure);

            // 停止追踪
            tracker.StopTracking(questName);

            // 触发事件
            OnQuestFailed?.Invoke(questName);

            // 音效
            AudioManager.Instance?.PlaySound(AudioKeys.QUEST_FAILED);

            if (debugMode)
                Debug.Log($"[QuestLogicManager] 任务失败（过期）: {questName}");

            SaveProgress();
        }

        #endregion

        #region 检查逻辑

        /// <summary>
        /// 扫描当前所有 active 的任务，开始追踪
        /// </summary>
        private void ScanActiveQuests()
        {
            string[] activeQuests = QuestLog.GetAllQuests(QuestState.Active);
            if (activeQuests == null) return;

            foreach (string questName in activeQuests)
            {
                if (!tracker.IsTracking(questName))
                {
                    tracker.StartTracking(questName);

                    if (debugMode)
                        Debug.Log($"[QuestLogicManager] 扫描到已激活任务: {questName}");
                }
            }
        }

        /// <summary>
        /// 检查所有 active 任务的 DDL
        /// </summary>
        private void CheckDeadlines()
        {
            string[] activeQuests = QuestLog.GetAllQuests(QuestState.Active);
            if (activeQuests == null) return;

            foreach (string questName in activeQuests)
            {
                int remaining = QuestDataService.Instance?.GetRemainingDays(questName) ?? -1;
                if (remaining == -1) continue; // 永不过期
                if (remaining <= 0)
                {
                    FailQuest(questName);
                }
            }
        }

        /// <summary>
        /// 检查是否有任务被外部代码直接完成（未经 Tracker）
        /// 如果完成但未发放奖励，则补发
        /// </summary>
        private void CheckExternalCompletions()
        {
            string[] successQuests = QuestLog.GetAllQuests(QuestState.Success);
            if (successQuests == null) return;

            foreach (string questName in successQuests)
            {
                if (!rewardedQuests.Contains(questName))
                {
                    var def = QuestDataService.Instance?.GetDefinition(questName);
                    if (def?.Rewards != null && def.Rewards.Length > 0)
                    {
                        QuestRewardDistributor.DistributeRewards(questName);
                        rewardedQuests.Add(questName);

                        OnQuestCompleted?.Invoke(questName);

                        if (debugMode)
                            Debug.Log($"[QuestLogicManager] 外部完成的任务，补发奖励: {questName}");
                    }
                }

                // 确保不再追踪已完成的任务
                tracker.StopTracking(questName);
            }
        }

        /// <summary>
        /// 检查任务是否所有 Entry 都已完成
        /// </summary>
        private bool AreAllEntriesComplete(string questName)
        {
            var (completed, total) = QuestDataService.Instance?.GetProgress(questName) ?? (0, 0);
            return total > 0 && completed >= total;
        }

        #endregion

        #region 持久化

        private void SaveProgress()
        {
            try
            {
                // 保存计数器
                ES3.Save(ES3_KEY_COUNTERS, tracker.GetCountersForSave(), ES3_FILE);

                // 保存激活日映射
                var activationMap = QuestDataService.Instance?.GetActivationDayMap();
                if (activationMap != null)
                    ES3.Save(ES3_KEY_ACTIVATION, activationMap, ES3_FILE);

                // 保存已奖励的任务集合
                ES3.Save(ES3_KEY_REWARDED, new List<string>(rewardedQuests), ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestLogicManager] 保存失败: {e.Message}");
            }
        }

        private void LoadProgress()
        {
            try
            {
                // 加载计数器
                if (ES3.KeyExists(ES3_KEY_COUNTERS, ES3_FILE))
                {
                    var saved = ES3.Load<Dictionary<string, int[]>>(ES3_KEY_COUNTERS, ES3_FILE);
                    tracker.LoadCounters(saved);
                }

                // 加载激活日映射
                if (ES3.KeyExists(ES3_KEY_ACTIVATION, ES3_FILE))
                {
                    var map = ES3.Load<Dictionary<string, int>>(ES3_KEY_ACTIVATION, ES3_FILE);
                    QuestDataService.Instance?.SetActivationDayMap(map);
                }

                // 加载已奖励的任务集合
                if (ES3.KeyExists(ES3_KEY_REWARDED, ES3_FILE))
                {
                    var list = ES3.Load<List<string>>(ES3_KEY_REWARDED, ES3_FILE);
                    rewardedQuests = new HashSet<string>(list);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuestLogicManager] 加载失败（首次运行正常）: {e.Message}");
            }
        }

        #endregion
    }
}
