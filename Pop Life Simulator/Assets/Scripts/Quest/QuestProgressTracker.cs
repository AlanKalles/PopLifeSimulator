using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Data;
using PopLife.Runtime;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.UI.Quest;

namespace PopLife.Quest
{
    /// <summary>
    /// 任务进度追踪器
    /// 订阅游戏事件，更新计数器，当条件满足时标记 QuestLog Entry 为 success
    /// 非 MonoBehaviour，由 QuestLogicManager 持有和管理
    /// </summary>
    public class QuestProgressTracker
    {
        // questName -> 每个条件的当前计数（索引对应 conditions[]）
        private Dictionary<string, int[]> counters = new();
        // Daily scope 条件的当日计数（每天重置）
        private Dictionary<string, int[]> dailyCounters = new();
        // 正在追踪的任务集合
        private HashSet<string> trackingQuests = new();
        // 遍历用临时列表（避免遍历中修改集合）
        private List<string> trackingSnapshot = new();

        private bool debugMode;

        /// <summary>
        /// 条目完成时触发（questName, entryNumber）
        /// </summary>
        public event Action<string, int> OnEntryCompleted;

        /// <summary>
        /// 设置调试模式
        /// </summary>
        public void SetDebugMode(bool debug)
        {
            debugMode = debug;
        }

        #region 追踪管理

        /// <summary>
        /// 开始追踪指定任务
        /// </summary>
        public void StartTracking(string questName)
        {
            if (trackingQuests.Contains(questName)) return;

            var def = QuestDataService.Instance?.GetDefinition(questName);
            if (def?.Conditions == null || def.Conditions.Length == 0) return;

            trackingQuests.Add(questName);

            // 初始化计数器（如果没有从持久化数据恢复）
            if (!counters.ContainsKey(questName))
                counters[questName] = new int[def.Conditions.Length];

            if (!dailyCounters.ContainsKey(questName))
                dailyCounters[questName] = new int[def.Conditions.Length];

            // 对 Current scope 的条件立即检查一次
            CheckCurrentScopeConditions(questName, def);

            if (debugMode)
                Debug.Log($"[QuestProgressTracker] 开始追踪: {questName}，{def.Conditions.Length} 个条件");
        }

        /// <summary>
        /// 停止追踪指定任务
        /// </summary>
        public void StopTracking(string questName)
        {
            trackingQuests.Remove(questName);
            counters.Remove(questName);
            dailyCounters.Remove(questName);
        }

        /// <summary>
        /// 是否正在追踪指定任务
        /// </summary>
        public bool IsTracking(string questName) => trackingQuests.Contains(questName);

        /// <summary>
        /// 每日重置 Daily scope 的计数器
        /// </summary>
        public void ResetDailyCounters()
        {
            foreach (var kvp in dailyCounters)
            {
                Array.Clear(kvp.Value, 0, kvp.Value.Length);
            }

            if (debugMode)
                Debug.Log("[QuestProgressTracker] Daily 计数器已重置");
        }

        #endregion

        #region 事件订阅

        public void SubscribeEvents()
        {
            CustomerEventBus.OnPurchased += HandlePurchased;
            CustomerEventBus.OnCheckedOut += HandleCheckedOut;
            ConstructionManager.OnBuildingPlacedOrDestroyed += HandleBuildingChanged;

            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnMoneyChanged += HandleMoneyChanged;
                ResourceManager.Instance.OnStoreAppealChanged += HandleStoreAppealChanged;
            }

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement += HandleDailySettlement;
            }
        }

        public void UnsubscribeEvents()
        {
            CustomerEventBus.OnPurchased -= HandlePurchased;
            CustomerEventBus.OnCheckedOut -= HandleCheckedOut;
            ConstructionManager.OnBuildingPlacedOrDestroyed -= HandleBuildingChanged;

            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnMoneyChanged -= HandleMoneyChanged;
                ResourceManager.Instance.OnStoreAppealChanged -= HandleStoreAppealChanged;
            }

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement -= HandleDailySettlement;
            }
        }

        #endregion

        #region 事件处理器

        /// <summary>
        /// 顾客购买事件 → SellItems 条件
        /// </summary>
        private void HandlePurchased(CustomerAgent agent, ShelfInstance shelf, int qty, int price)
        {
            var shelfArchetype = shelf.archetype as ShelfArchetype;
            RefreshSnapshot();

            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    var cond = def.Conditions[i];
                    if (cond.conditionType != QuestConditionType.SellItems) continue;

                    // 类别过滤
                    if (cond.useFilterCategory && shelfArchetype != null
                        && shelfArchetype.category != cond.filterCategory)
                        continue;

                    IncrementCounter(questName, i, qty, cond);
                }
            }
        }

        /// <summary>
        /// 顾客结账事件 → ServeCustomers 条件
        /// </summary>
        private void HandleCheckedOut(CustomerAgent agent)
        {
            RefreshSnapshot();
            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    if (def.Conditions[i].conditionType != QuestConditionType.ServeCustomers)
                        continue;

                    IncrementCounter(questName, i, 1, def.Conditions[i]);
                }
            }
        }

        /// <summary>
        /// 建筑放置/拆除事件 → PlaceBuildings 条件
        /// 对 Current scope 重新查询当前建筑总数
        /// </summary>
        private void HandleBuildingChanged()
        {
            RefreshSnapshot();
            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    var cond = def.Conditions[i];
                    if (cond.conditionType == QuestConditionType.PlaceBuildings)
                    {
                        SetCounter(questName, i, CountBuildings(cond), cond);
                    }
                    else if (cond.conditionType == QuestConditionType.PlaceShelves)
                    {
                        SetCounter(questName, i, CountShelves(cond), cond);
                    }
                }
            }
        }

        /// <summary>
        /// 金钱变化事件 → ReachMoneyThreshold / EarnMoney(Current) 条件
        /// </summary>
        private void HandleMoneyChanged(int currentMoney)
        {
            RefreshSnapshot();
            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    var cond = def.Conditions[i];

                    if (cond.conditionType == QuestConditionType.ReachMoneyThreshold)
                    {
                        SetCounter(questName, i, currentMoney, cond);
                    }
                    else if (cond.conditionType == QuestConditionType.EarnMoney && cond.scope == CountScope.Current)
                    {
                        SetCounter(questName, i, currentMoney, cond);
                    }
                }
            }
        }

        /// <summary>
        /// Store Appeal 变化事件 → ReachStoreAppeal 条件
        /// </summary>
        private void HandleStoreAppealChanged(int currentAppeal)
        {
            RefreshSnapshot();
            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    if (def.Conditions[i].conditionType != QuestConditionType.ReachStoreAppeal)
                        continue;

                    SetCounter(questName, i, currentAppeal, def.Conditions[i]);
                }
            }
        }

        /// <summary>
        /// 每日结算事件 → EarnMoney(Cumulative/Daily) / EarnFame 条件
        /// </summary>
        private void HandleDailySettlement(DailySettlementData data)
        {
            RefreshSnapshot();
            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    var cond = def.Conditions[i];

                    if (cond.conditionType == QuestConditionType.EarnMoney && cond.scope != CountScope.Current)
                    {
                        IncrementCounter(questName, i, (int)data.totalSale, cond);
                    }
                    else if (cond.conditionType == QuestConditionType.EarnFame)
                    {
                        IncrementCounter(questName, i, data.fameEarned, cond);
                    }
                }
            }
        }

        /// <summary>
        /// 天数变化 → SurviveDays 条件增量
        /// 由 QuestLogicManager.HandleDayChanged() 显式调用，确保在 ResetDailyCounters 之前执行
        /// </summary>
        public void ProcessDayChanged(int newDay)
        {
            RefreshSnapshot();
            foreach (string questName in trackingSnapshot)
            {
                var def = QuestDataService.Instance?.GetDefinition(questName);
                if (def?.Conditions == null) continue;

                for (int i = 0; i < def.Conditions.Length; i++)
                {
                    if (def.Conditions[i].conditionType != QuestConditionType.SurviveDays)
                        continue;

                    IncrementCounter(questName, i, 1, def.Conditions[i]);
                }
            }
        }

        #endregion

        #region 计数器操作

        /// <summary>
        /// 增量更新计数器
        /// </summary>
        private void IncrementCounter(string questName, int conditionIndex, int delta, QuestCondition cond)
        {
            if (!counters.TryGetValue(questName, out var counts)) return;
            if (conditionIndex >= counts.Length) return;

            // 已经完成的条目不再更新
            int entryNum = conditionIndex + 1;
            if (QuestLog.GetQuestEntryState(questName, entryNum) == QuestState.Success) return;

            if (cond.scope == CountScope.Daily)
            {
                if (!dailyCounters.TryGetValue(questName, out var dailyCounts)) return;
                dailyCounts[conditionIndex] += delta;
                counts[conditionIndex] = dailyCounts[conditionIndex];
            }
            else
            {
                counts[conditionIndex] += delta;
            }

            CheckAndUpdateEntry(questName, conditionIndex, counts[conditionIndex], cond.targetValue);
        }

        /// <summary>
        /// 直接设置计数器值（用于 Current scope 的快照比较）
        /// </summary>
        private void SetCounter(string questName, int conditionIndex, int value, QuestCondition cond)
        {
            if (!counters.TryGetValue(questName, out var counts)) return;
            if (conditionIndex >= counts.Length) return;

            // 已经完成的条目不再更新
            int entryNum = conditionIndex + 1;
            if (QuestLog.GetQuestEntryState(questName, entryNum) == QuestState.Success) return;

            counts[conditionIndex] = value;
            CheckAndUpdateEntry(questName, conditionIndex, value, cond.targetValue);
        }

        /// <summary>
        /// 检查计数是否达标，达标则标记 Entry 完成
        /// </summary>
        private void CheckAndUpdateEntry(string questName, int conditionIndex, int currentValue, int targetValue)
        {
            if (currentValue >= targetValue)
            {
                int entryNum = conditionIndex + 1;
                QuestLog.SetQuestEntryState(questName, entryNum, QuestState.Success);

                if (debugMode)
                    Debug.Log($"[QuestProgressTracker] 条目完成: {questName} Entry{entryNum} ({currentValue}/{targetValue})");

                OnEntryCompleted?.Invoke(questName, entryNum);
            }
        }

        /// <summary>
        /// 检查 Current scope 条件的当前值
        /// </summary>
        private void CheckCurrentScopeConditions(string questName, QuestDefinition def)
        {
            for (int i = 0; i < def.Conditions.Length; i++)
            {
                var cond = def.Conditions[i];
                if (cond.conditionType == QuestConditionType.Manual) continue;
                if (cond.scope != CountScope.Current && cond.conditionType != QuestConditionType.PlaceBuildings
                    && cond.conditionType != QuestConditionType.PlaceShelves
                    && cond.conditionType != QuestConditionType.ReachStoreAppeal
                    && cond.conditionType != QuestConditionType.ReachMoneyThreshold)
                    continue;

                int currentValue = 0;
                switch (cond.conditionType)
                {
                    case QuestConditionType.ReachMoneyThreshold:
                        currentValue = ResourceManager.Instance?.GetMoney() ?? 0;
                        break;
                    case QuestConditionType.ReachStoreAppeal:
                        currentValue = ResourceManager.Instance?.GetStoreAppeal() ?? 0;
                        break;
                    case QuestConditionType.PlaceBuildings:
                        currentValue = CountBuildings(cond);
                        break;
                    case QuestConditionType.PlaceShelves:
                        currentValue = CountShelves(cond);
                        break;
                }

                SetCounter(questName, i, currentValue, cond);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 刷新遍历快照（避免遍历中因事件回调导致集合被修改）
        /// </summary>
        private void RefreshSnapshot()
        {
            trackingSnapshot.Clear();
            trackingSnapshot.AddRange(trackingQuests);
        }

        /// <summary>
        /// 统计当前建筑数量（含设施，可选按类别过滤货架）
        /// </summary>
        private int CountBuildings(QuestCondition cond)
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return 0;

            int count = 0;
            if (cond.useFilterCategory)
            {
                foreach (var shelf in wg.AllShelves())
                {
                    if (shelf.archetype is ShelfArchetype sa && sa.category == cond.filterCategory)
                        count++;
                }
            }
            else
            {
                foreach (var _ in wg.AllBuildings())
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 统计当前货架数量（不含设施）
        /// 过滤优先级（互斥，只走一个分支）：
        /// 1. useFilterArchetypes=true：按 ShelfArchetype 列表 OR 过滤；列表无效时直接返回 0（不回退到 category）
        /// 2. 否则 useFilterCategory=true：按 ProductCategory 过滤
        /// 3. 否则：不过滤
        /// </summary>
        private int CountShelves(QuestCondition cond)
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return 0;

            // 优先级 1: archetype 列表过滤（锁死分支）
            bool useArchetypeFilter = cond.useFilterArchetypes && HasAnyValidArchetype(cond.filterArchetypes);
            if (cond.useFilterArchetypes && !useArchetypeFilter)
                return 0; // 勾选了但列表无效：计数恒 0（防止 fall through 误匹配）

            int count = 0;
            foreach (var shelf in wg.AllShelves())
            {
                var sa = shelf.archetype as ShelfArchetype;
                if (sa == null) continue;

                if (useArchetypeFilter)
                {
                    for (int i = 0; i < cond.filterArchetypes.Length; i++)
                    {
                        var candidate = cond.filterArchetypes[i];
                        if (candidate == null) continue; // 跳过空槽
                        if (candidate == sa) { count++; break; }
                    }
                }
                else if (cond.useFilterCategory)
                {
                    if (sa.category == cond.filterCategory) count++;
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 判断 ShelfArchetype 列表是否含有任何有效元素
        /// 无效情况：null 数组 / 长度 0 / 全部元素为 null
        /// </summary>
        private static bool HasAnyValidArchetype(ShelfArchetype[] list)
        {
            if (list == null || list.Length == 0) return false;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null) return true;
            return false;
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 获取计数器数据用于保存
        /// </summary>
        public Dictionary<string, int[]> GetCountersForSave()
        {
            return new Dictionary<string, int[]>(counters);
        }

        /// <summary>
        /// 从保存数据恢复计数器
        /// </summary>
        public void LoadCounters(Dictionary<string, int[]> saved)
        {
            if (saved == null) return;
            counters = new Dictionary<string, int[]>(saved);
        }

        #endregion
    }
}
