using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Data;
using PopLife.Quest;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// ViewModel：追踪面板用的轻量数据
    /// </summary>
    public struct QuestViewModel
    {
        public string questName;
        public string displayTitle;
        public QuestType questType;
        public int remainingDays;   // -1 = 永不过期
        public int deadlineDay;     // 绝对日期（absoluteDay），-1 表示无截止
        public int completedEntries;
        public int totalEntries;
        public int sortPriority;
    }

    /// <summary>
    /// ViewModel：详情面板用的完整数据
    /// </summary>
    public struct QuestDetailViewModel
    {
        public string questName;
        public string displayTitle;
        public string description;
        public QuestType questType;
        public int remainingDays;
        public int deadlineDay;     // 绝对日期（absoluteDay），-1 表示无截止
        public string giverName;
        public Sprite giverPortrait;
        public Sprite questIcon;
        public QuestReward[] rewards;
        public QuestEntryInfo[] entries;
        public QuestState questState;
    }

    /// <summary>
    /// 任务日志分组ViewModel - 按状态分组的任务列表
    /// </summary>
    public struct QuestLogGroupViewModel
    {
        public string groupLabel;
        public List<QuestViewModel> quests;
    }

    /// <summary>
    /// 任务条目信息
    /// </summary>
    public struct QuestEntryInfo
    {
        public int entryNumber;
        public string text;
        public bool isCompleted;
    }

    /// <summary>
    /// 任务数据桥接服务
    /// 合并 QuestStateStore（状态/进度）和 QuestDefinition SO（元数据）的数据，供 UI 消费
    ///
    /// 挂载位置：DialogueManager 的子物体上（接收 OnQuestStateChange BroadcastMessage）
    /// </summary>
    public class QuestDataService : MonoBehaviour
    {
        public static QuestDataService Instance { get; private set; }

        // 自动从 Resources/ScriptableObjects/Quests 加载所有 QuestDefinition
        private QuestDefinition[] questDefinitions;

        [Header("调试")]
        [SerializeField] private bool debugMode = false;

        // questName → QuestDefinition 的快速查找
        private Dictionary<string, QuestDefinition> definitionMap = new();
        // questName → 激活时的 currentDay（用于 DDL 计算）
        private Dictionary<string, int> activationDayMap = new();

        // 快捷访问 QuestStateStore
        private QuestStateStore Store => QuestLogicManager.Instance?.StateStore;

        /// <summary>
        /// UI 订阅此事件以刷新追踪面板
        /// </summary>
        public event Action OnTrackedQuestsChanged;

        /// <summary>
        /// 任何任务状态变化时触发（QuestLogPanel 订阅）
        /// </summary>
        public event Action OnQuestStateChanged;

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

            questDefinitions = Resources.LoadAll<QuestDefinition>("ScriptableObjects/Quests");
            BuildDefinitionMap();
        }

        private void OnEnable()
        {
            // 订阅 DayLoopManager 天数变化（DDL 需要随天数刷新）
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged -= OnDayChanged;
                DayLoopManager.Instance.OnDayChanged += OnDayChanged;
            }
        }

        private void Start()
        {
            // 延迟订阅，确保 DayLoopManager 已初始化
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged -= OnDayChanged;
                DayLoopManager.Instance.OnDayChanged += OnDayChanged;
            }

            // 初始化时扫描已有的 active 任务，补全缺失的激活日
            ScanActiveQuests();
        }

        private void OnDisable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged -= OnDayChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region 初始化

        private void BuildDefinitionMap()
        {
            definitionMap.Clear();
            if (questDefinitions == null) return;

            foreach (var def in questDefinitions)
            {
                if (def == null || string.IsNullOrEmpty(def.QuestName)) continue;

                if (definitionMap.ContainsKey(def.QuestName))
                {
                    Debug.LogWarning($"[QuestDataService] 重复的 questName: {def.QuestName}");
                    continue;
                }

                definitionMap[def.QuestName] = def;
            }

            if (debugMode)
            {
                Debug.Log($"[QuestDataService] 已加载 {definitionMap.Count} 个 QuestDefinition");
            }
        }

        /// <summary>
        /// 扫描当前所有 active 的任务，补全缺失的激活日记录
        /// </summary>
        private void ScanActiveQuests()
        {
            string[] activeQuests = Store?.GetAllQuests(QuestState.Active);
            if (activeQuests == null || activeQuests.Length == 0) return;

            int currentDay = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;

            foreach (string qName in activeQuests)
            {
                if (!activationDayMap.ContainsKey(qName))
                {
                    // 未知激活日，以当前天数为默认值
                    activationDayMap[qName] = currentDay;

                    if (debugMode)
                    {
                        Debug.Log($"[QuestDataService] 补全激活日: {qName} → Day {currentDay}");
                    }
                }
            }
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// Dialogue System BroadcastMessage 回调
        /// 当任务状态变化时由 DialogueManager 广播
        /// </summary>
        private void OnQuestStateChange(string questName)
        {
            // 如果任务刚变为 active，记录激活日
            var state = Store?.GetQuestState(questName) ?? QuestState.Unassigned;
            if (state == QuestState.Active && !activationDayMap.ContainsKey(questName))
            {
                int currentDay = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
                activationDayMap[questName] = currentDay;

                if (debugMode)
                {
                    Debug.Log($"[QuestDataService] 任务激活: {questName} → Day {currentDay}");
                }
            }

            // 兜底：对话或 Lua 直接 SetQuestState 激活的任务也自动开启 tracking
            if (state == QuestState.Active && Store != null && !Store.IsTrackingEnabled(questName))
            {
                Store.SetTracking(questName, true);

                if (debugMode)
                {
                    Debug.Log($"[QuestDataService] 自动开启任务追踪: {questName}");
                }
            }

            OnTrackedQuestsChanged?.Invoke();
            OnQuestStateChanged?.Invoke();
        }

        /// <summary>
        /// 天数变化回调（DDL 需要刷新）
        /// </summary>
        private void OnDayChanged(int newDay)
        {
            OnTrackedQuestsChanged?.Invoke();
            OnQuestStateChanged?.Invoke();
        }

        #endregion

        #region 公共 API

        /// <summary>
        /// 获取所有正在追踪的任务列表
        /// 排序规则：主线优先 > sortPriority 降序 > DDL 升序
        /// </summary>
        public List<QuestViewModel> GetTrackedQuests()
        {
            var result = new List<QuestViewModel>();
            string[] activeQuests = Store?.GetAllQuests(QuestState.Active);
            if (activeQuests == null || activeQuests.Length == 0) return result;

            foreach (string qName in activeQuests)
            {
                if (Store != null && !Store.IsTrackingEnabled(qName)) continue;

                var vm = new QuestViewModel
                {
                    questName = qName,
                    displayTitle = Store?.GetTitle(qName) ?? qName,
                    questType = GetQuestType(qName),
                    remainingDays = GetRemainingDays(qName),
                    deadlineDay = GetDeadlineDay(qName),
                    sortPriority = GetSortPriority(qName)
                };
                (vm.completedEntries, vm.totalEntries) = GetProgress(qName);
                result.Add(vm);
            }

            // 排序：主线优先 > sortPriority 降序 > DDL 升序（永不过期排最后）
            result.Sort((a, b) =>
            {
                int typeCompare = a.questType.CompareTo(b.questType); // Main=0 < Side=1
                if (typeCompare != 0) return typeCompare;

                int prioCompare = b.sortPriority.CompareTo(a.sortPriority);
                if (prioCompare != 0) return prioCompare;

                int aDeadline = a.remainingDays < 0 ? int.MaxValue : a.remainingDays;
                int bDeadline = b.remainingDays < 0 ? int.MaxValue : b.remainingDays;
                return aDeadline.CompareTo(bDeadline);
            });

            return result;
        }

        /// <summary>
        /// 获取任务详情（供详情面板使用）
        /// </summary>
        public QuestDetailViewModel? GetQuestDetail(string questName)
        {
            if (string.IsNullOrEmpty(questName) || Store == null) return null;

            var state = Store.GetQuestState(questName);
            if (state == QuestState.Unassigned) return null;

            definitionMap.TryGetValue(questName, out var def);

            // 构建条目列表
            int entryCount = Store.GetEntryCount(questName);
            var entries = new QuestEntryInfo[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                int entryNum = i + 1; // entry 从 1 开始
                entries[i] = new QuestEntryInfo
                {
                    entryNumber = entryNum,
                    text = Store.GetEntry(questName, entryNum),
                    isCompleted = QuestLog.GetQuestEntryState(questName, entryNum) == QuestState.Success
                };
            }

            return new QuestDetailViewModel
            {
                questName = questName,
                displayTitle = Store.GetTitle(questName),
                description = Store.GetDescription(questName),
                questType = def != null ? def.QuestType : QuestType.Side,
                remainingDays = GetRemainingDays(questName),
                deadlineDay = GetDeadlineDay(questName),
                giverName = def != null ? def.GiverName : "",
                giverPortrait = def?.GiverPortrait,
                questIcon = def?.QuestIcon,
                rewards = def?.Rewards,
                entries = entries,
                questState = state
            };
        }

        /// <summary>
        /// 获取任务剩余天数
        /// 返回 -1 表示永不过期
        /// </summary>
        public int GetRemainingDays(string questName)
        {
            if (!definitionMap.TryGetValue(questName, out var def) || def.DeadlineDays <= 0)
                return -1;

            if (!activationDayMap.TryGetValue(questName, out int activateDay))
                return -1;

            int deadlineDay = activateDay + def.DeadlineDays;
            int currentDay = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
            return deadlineDay - currentDay;
        }

        /// <summary>
        /// 获取任务截止的绝对天数（absoluteDay）
        /// 返回 -1 表示无截止日期
        /// </summary>
        public int GetDeadlineDay(string questName)
        {
            if (!definitionMap.TryGetValue(questName, out var def) || def.DeadlineDays <= 0)
                return -1;

            if (!activationDayMap.TryGetValue(questName, out int activateDay))
                return -1;

            return activateDay + def.DeadlineDays;
        }

        /// <summary>
        /// 获取任务进度（已完成/总数）
        /// </summary>
        public (int completed, int total) GetProgress(string questName)
        {
            int total = Store?.GetEntryCount(questName) ?? 0;
            int completed = 0;
            for (int i = 1; i <= total; i++)
            {
                // entry 状态通过 override delegate 路由到 store
                if (QuestLog.GetQuestEntryState(questName, i) == QuestState.Success)
                    completed++;
            }
            return (completed, total);
        }

        /// <summary>
        /// 获取所有非 Unassigned 的任务，按状态分组
        /// 顺序：Active → Completed → Failed，空组不返回
        /// </summary>
        public List<QuestLogGroupViewModel> GetAllQuestsGrouped()
        {
            var result = new List<QuestLogGroupViewModel>();

            var activeGroup = BuildGroup("Active", QuestState.Active);
            if (activeGroup.quests.Count > 0) result.Add(activeGroup);

            var successGroup = BuildGroup("Completed", QuestState.Success);
            if (successGroup.quests.Count > 0) result.Add(successGroup);

            var failedGroup = BuildGroup("Failed", QuestState.Failure);
            if (failedGroup.quests.Count > 0) result.Add(failedGroup);

            return result;
        }

        private QuestLogGroupViewModel BuildGroup(string label, QuestState state)
        {
            var group = new QuestLogGroupViewModel
            {
                groupLabel = label,
                quests = new List<QuestViewModel>()
            };

            string[] questNames = Store?.GetAllQuests(state);
            if (questNames == null || questNames.Length == 0) return group;

            foreach (string qName in questNames)
            {
                var vm = new QuestViewModel
                {
                    questName = qName,
                    displayTitle = Store?.GetTitle(qName) ?? qName,
                    questType = GetQuestType(qName),
                    remainingDays = GetRemainingDays(qName),
                    deadlineDay = GetDeadlineDay(qName),
                    sortPriority = GetSortPriority(qName)
                };
                (vm.completedEntries, vm.totalEntries) = GetProgress(qName);
                group.quests.Add(vm);
            }

            // Active 组使用完整排序（主线优先 > 优先级 > DDL）
            if (state == QuestState.Active)
            {
                group.quests.Sort((a, b) =>
                {
                    int typeCompare = a.questType.CompareTo(b.questType);
                    if (typeCompare != 0) return typeCompare;
                    int prioCompare = b.sortPriority.CompareTo(a.sortPriority);
                    if (prioCompare != 0) return prioCompare;
                    int aDeadline = a.remainingDays < 0 ? int.MaxValue : a.remainingDays;
                    int bDeadline = b.remainingDays < 0 ? int.MaxValue : b.remainingDays;
                    return aDeadline.CompareTo(bDeadline);
                });
            }

            return group;
        }

        #endregion

        /// <summary>
        /// 获取所有有截止日期的活跃任务及其截止日（absoluteDay）
        /// 供 CalendarPanel 显示 Due Date 标签
        /// </summary>
        public List<(string questName, string displayTitle, int dueAbsoluteDay, string giverName)> GetActiveQuestDeadlines()
        {
            var result = new List<(string, string, int, string)>();
            string[] activeQuests = Store?.GetAllQuests(QuestState.Active);
            if (activeQuests == null || activeQuests.Length == 0) return result;

            foreach (string qName in activeQuests)
            {
                if (!definitionMap.TryGetValue(qName, out var def)) continue;
                if (def.DeadlineDays <= 0) continue;
                if (!activationDayMap.TryGetValue(qName, out int activateDay)) continue;

                int dueDay = activateDay + def.DeadlineDays;
                result.Add((qName, Store?.GetTitle(qName) ?? qName, dueDay, def.GiverName));
            }
            return result;
        }

        /// <summary>
        /// 获取所有已注册的 QuestDefinition
        /// </summary>
        public QuestDefinition[] GetAllDefinitions() => questDefinitions;

        /// <summary>
        /// 根据 questName 获取对应的 QuestDefinition
        /// </summary>
        public QuestDefinition GetDefinition(string questName)
        {
            if (string.IsNullOrEmpty(questName)) return null;
            definitionMap.TryGetValue(questName, out var def);
            return def;
        }

        /// <summary>
        /// 获取激活日映射副本（用于持久化）
        /// </summary>
        public Dictionary<string, int> GetActivationDayMap() => new(activationDayMap);

        /// <summary>
        /// 设置激活日映射（从持久化数据恢复）
        /// </summary>
        public void SetActivationDayMap(Dictionary<string, int> map)
        {
            activationDayMap = map ?? new();
        }

        #region 内部辅助

        private QuestType GetQuestType(string questName)
        {
            if (definitionMap.TryGetValue(questName, out var def))
                return def.QuestType;

            // 从 store 推断
            string group = Store?.GetGroup(questName);
            if (!string.IsNullOrEmpty(group) && group.Equals("Main", StringComparison.OrdinalIgnoreCase))
                return QuestType.Main;

            return QuestType.Side;
        }

        private int GetSortPriority(string questName)
        {
            if (definitionMap.TryGetValue(questName, out var def))
                return def.SortPriority;
            return 50; // 默认优先级
        }

        #endregion
    }
}
