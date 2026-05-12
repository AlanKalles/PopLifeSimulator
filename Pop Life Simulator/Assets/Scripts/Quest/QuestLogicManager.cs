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
        private QuestStateStore stateStore;

        /// <summary>
        /// 任务状态 store，替代 QuestLog 作为状态后端
        /// </summary>
        public QuestStateStore StateStore => stateStore;

        // 已处理过奖励的任务集合（防止重复发放）
        private HashSet<string> rewardedQuests = new();

        // marker → questName 查找表（用于 marker 自动激活）
        // 支持一个 marker 激活多个任务：同一 marker 下的所有 QuestDefinition 都会被激活
        private Dictionary<TutorialMarker, List<string>> markerToQuests;

        // ES3 持久化
        private const string ES3_FILE = "QuestProgress.es3";
        private const string ES3_KEY_COUNTERS = "questCounters";
        private const string ES3_KEY_ACTIVATION = "questActivationDays";
        private const string ES3_KEY_REWARDED = "questRewarded";
        private const string ES3_KEY_STATE_DATA = "questStateData";

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
            // 初始化 QuestStateStore（必须在 LoadProgress 之前）
            InitializeStateStore();

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
            ActivateAlreadyTriggeredMarkerQuests();

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

            stateStore?.Dispose();

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
            if (state == QuestState.Success || state == QuestState.Failure)
            {
                if (debugMode) Debug.Log($"[QuestLogicManager] 任务已有终态，跳过激活: {questName} ({state})");
                return;
            }

            // 先开启 tracking，再广播 Active 状态，避免 QuestTrackerPanel
            // 在首次刷新时因为 tracking flag 尚未写入而把新任务过滤掉
            stateStore?.SetTracking(questName, true);
            QuestLog.SetQuestState(questName, QuestState.Active);

            if (debugMode)
                Debug.Log($"[QuestLogicManager] 激活任务: {questName}");

            var def = QuestDataService.Instance?.GetDefinition(questName);
            if (def != null && def.AutoCompleteOnActivation)
            {
                if (debugMode)
                    Debug.Log($"[QuestLogicManager] 自动完成任务: {questName}");
                CompleteQuest(questName);
            }
        }

        #endregion

        #region Marker 自动激活

        /// <summary>
        /// 构建 marker → questName 查找表（一对多）
        /// </summary>
        private void BuildMarkerLookup()
        {
            markerToQuests = new Dictionary<TutorialMarker, List<string>>();
            var allDefs = Resources.LoadAll<QuestDefinition>("ScriptableObjects/Quests");

            foreach (var def in allDefs)
            {
                if (def.ActivationMarker == TutorialMarker.None)
                    continue;

                if (!markerToQuests.TryGetValue(def.ActivationMarker, out var list))
                {
                    list = new List<string>();
                    markerToQuests[def.ActivationMarker] = list;
                }

                if (!list.Contains(def.QuestName))
                    list.Add(def.QuestName);
            }

            if (debugMode)
            {
                foreach (var pair in markerToQuests)
                {
                    if (pair.Value.Count > 1)
                        Debug.Log($"[QuestLogicManager] Marker {pair.Key} maps to {pair.Value.Count} quests: {string.Join(", ", pair.Value)}");
                }
            }
        }

        /// <summary>
        /// 当 marker 触发时，自动激活所有对应的任务
        /// </summary>
        private void OnMarkerTriggered(TutorialMarker marker)
        {
            if (markerToQuests == null) return;
            if (!markerToQuests.TryGetValue(marker, out var questNames) || questNames == null) return;

            foreach (var questName in questNames)
            {
                ActivateQuest(questName);
            }
        }

        /// <summary>
        /// Some startup markers can be raised before this manager subscribes.
        /// Catch those up so marker-activated quests are still assigned.
        /// </summary>
        private void ActivateAlreadyTriggeredMarkerQuests()
        {
            if (markerToQuests == null) return;

            foreach (var pair in markerToQuests)
            {
                if (!TutorialEventBus.IsMarkerTriggered(pair.Key)) continue;

                foreach (var questName in pair.Value)
                {
                    ActivateQuest(questName);
                }
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
            string[] activeQuests = stateStore?.GetAllQuests(QuestState.Active);
            if (activeQuests != null && activeQuests.Length > 0)
            {
                // 收集需要 auto-complete 的任务，避免在遍历 activeQuests 时
                // 因 CompleteQuest 同步触发新一轮 HandleQuestStateChanged 造成迭代不一致
                List<string> autoCompletePending = null;

                foreach (string questName in activeQuests)
                {
                    if (!tracker.IsTracking(questName))
                    {
                        tracker.StartTracking(questName);
                        OnQuestActivated?.Invoke(questName);

                        if (debugMode)
                            Debug.Log($"[QuestLogicManager] 检测到新激活任务: {questName}");

                        // Dialogue/Lua 通过 SetQuestState 直接激活的任务也要尊重 AutoCompleteOnActivation
                        // （ActivateQuest() 路径之外的兜底）
                        var def = QuestDataService.Instance?.GetDefinition(questName);
                        if (def != null && def.AutoCompleteOnActivation)
                        {
                            autoCompletePending ??= new List<string>();
                            autoCompletePending.Add(questName);
                        }
                    }
                }

                if (autoCompletePending != null)
                {
                    foreach (var questName in autoCompletePending)
                    {
                        if (debugMode)
                            Debug.Log($"[QuestLogicManager] 自动完成任务（外部激活路径）: {questName}");
                        CompleteQuest(questName);
                    }
                }

                // Dialogue/Lua 可以直接把 Entry 设为 success。此路径不会经过
                // QuestProgressTracker.OnEntryCompleted，所以这里需要补一次整任务完成判定。
                CheckActiveQuestCompletions(activeQuests);
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

            // 4. 按天触发 marker（在DDL判定之后）
            if (newDay == 5)
            {
                TutorialEventBus.RaiseMarker(TutorialMarker.LotteryUnlocked);
                TutorialEventBus.RaiseMarker(TutorialMarker.Day5Auditor1stQuestDDL);
            }

            SaveProgress();
        }

        #endregion

        #region 任务完成/失败

        /// <summary>
        /// 完成任务：设置状态、发放奖励、触发事件
        /// </summary>
        private void CompleteQuest(string questName)
        {
            if (QuestLog.GetQuestState(questName) == QuestState.Success)
                return;

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

            var failDef = QuestDataService.Instance?.GetDefinition(questName);
            if (failDef != null && failDef.FailureMarker != TutorialMarker.None)
                TutorialEventBus.RaiseMarker(failDef.FailureMarker);

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
            string[] activeQuests = stateStore?.GetAllQuests(QuestState.Active);
            if (activeQuests == null || activeQuests.Length == 0) return;

            // 兜底修复存量：因历史 bug 滞留在 Active 但应当 auto-complete 的任务，重启时一并完成。
            // 在遍历之外执行，避免 CompleteQuest 修改 stateStore 影响迭代。
            List<string> autoCompletePending = null;

            foreach (string questName in activeQuests)
            {
                if (!tracker.IsTracking(questName))
                {
                    tracker.StartTracking(questName);

                    if (debugMode)
                        Debug.Log($"[QuestLogicManager] 扫描到已激活任务: {questName}");
                }

                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def != null && def.AutoCompleteOnActivation)
                {
                    autoCompletePending ??= new List<string>();
                    autoCompletePending.Add(questName);
                }
            }

            if (autoCompletePending != null)
            {
                foreach (var questName in autoCompletePending)
                {
                    if (debugMode)
                        Debug.Log($"[QuestLogicManager] 启动扫描兜底完成 AutoComplete 任务: {questName}");
                    CompleteQuest(questName);
                }
            }
        }

        /// <summary>
        /// 检查所有 active 任务的 DDL
        /// </summary>
        private void CheckDeadlines()
        {
            string[] activeQuests = stateStore?.GetAllQuests(QuestState.Active);
            if (activeQuests == null || activeQuests.Length == 0) return;

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
        /// 检查 Active 任务是否因为外部 Entry 状态变化而已经满足完成条件。
        /// </summary>
        private void CheckActiveQuestCompletions(string[] activeQuests)
        {
            if (activeQuests == null || activeQuests.Length == 0) return;

            foreach (string questName in activeQuests)
            {
                if (QuestLog.GetQuestState(questName) != QuestState.Active)
                    continue;

                if (AreAllEntriesComplete(questName))
                    CompleteQuest(questName);
            }
        }

        /// <summary>
        /// 检查是否有任务被外部代码直接完成（未经 Tracker）
        /// 如果完成但未发放奖励，则补发
        /// </summary>
        private void CheckExternalCompletions()
        {
            string[] successQuests = stateStore?.GetAllQuests(QuestState.Success);
            if (successQuests == null || successQuests.Length == 0) return;

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

        #region StateStore 初始化

        private void InitializeStateStore()
        {
            // 构建 defMap
            var defMap = new Dictionary<string, QuestDefinition>();
            var allDefs = Resources.LoadAll<QuestDefinition>("ScriptableObjects/Quests");
            foreach (var def in allDefs)
            {
                if (def != null && !string.IsNullOrEmpty(def.QuestName))
                    defMap.TryAdd(def.QuestName, def);
            }

            stateStore = new QuestStateStore();
            stateStore.Initialize(defMap, debugMode);
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

                // 保存任务状态数据
                if (stateStore != null)
                    ES3.Save(ES3_KEY_STATE_DATA, stateStore.GetSaveData(), ES3_FILE);
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
                // 加载任务状态数据（必须在其他数据之前，delegate 已安装）
                if (stateStore != null && ES3.KeyExists(ES3_KEY_STATE_DATA, ES3_FILE))
                {
                    var stateData = ES3.Load<QuestStateSaveData>(ES3_KEY_STATE_DATA, ES3_FILE);
                    stateStore.LoadSaveData(stateData);
                }

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
