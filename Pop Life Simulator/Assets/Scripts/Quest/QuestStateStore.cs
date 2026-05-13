using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Data;

namespace PopLife.Quest
{
    /// <summary>
    /// 任务状态存储 - 替代 Dialogue System QuestLog 作为状态后端
    /// 通过 QuestLog 的 override delegate 拦截所有状态读写，
    /// 使现有代码（C# 和 Lua）无需改动即可路由到此 store
    /// </summary>
    [Serializable]
    public class QuestStateSaveData
    {
        public Dictionary<string, string> questStates = new();
        public Dictionary<string, string[]> entryStates = new();
        public Dictionary<string, bool> trackingFlags = new();
    }

    public class QuestStateStore
    {
        private Dictionary<string, string> questStates = new();
        private Dictionary<string, string[]> entryStates = new();
        private Dictionary<string, bool> trackingFlags = new();
        private Dictionary<string, QuestDefinition> defMap = new();

        private bool debugMode;

        /// <summary>
        /// 初始化 store 并安装 QuestLog override delegate
        /// </summary>
        public void Initialize(Dictionary<string, QuestDefinition> definitions, bool debug = false)
        {
            defMap = definitions ?? new();
            debugMode = debug;

            // 安装 4 个 override delegate
            QuestLog.CurrentQuestStateOverride = GetQuestStateString;
            QuestLog.SetQuestStateOverride = SetQuestStateFromString;
            QuestLog.CurrentQuestEntryStateOverride = GetEntryStateString;
            QuestLog.SetQuestEntryStateOverride = SetEntryStateFromString;

            if (debugMode)
                Debug.Log($"[QuestStateStore] 初始化完成，已安装 override delegate，{defMap.Count} 个定义");
        }

        /// <summary>
        /// 清除 override delegate
        /// </summary>
        public void Dispose()
        {
            QuestLog.CurrentQuestStateOverride = null;
            QuestLog.SetQuestStateOverride = null;
            QuestLog.CurrentQuestEntryStateOverride = null;
            QuestLog.SetQuestEntryStateOverride = null;

            if (debugMode)
                Debug.Log("[QuestStateStore] 已清除 override delegate");
        }

        #region Override Delegate 实现

        /// <summary>
        /// CurrentQuestStateOverride delegate 实现
        /// </summary>
        private string GetQuestStateString(string questName)
        {
            if (questStates.TryGetValue(questName, out string state))
                return state;
            return "unassigned";
        }

        /// <summary>
        /// SetQuestStateOverride delegate 实现
        /// </summary>
        private void SetQuestStateFromString(string questName, string state)
        {
            // 同状态短路：状态未变则不写不广播，避免 dialogue/Lua 反复调 SetQuestState 触发重复 toast 和重置
            if (questStates.TryGetValue(questName, out string oldState)
                && string.Equals(oldState, state, StringComparison.OrdinalIgnoreCase))
            {
                if (debugMode)
                    Debug.Log($"[QuestStateStore] SetQuestState skip (unchanged): {questName} = {state}");
                return;
            }

            questStates[questName] = state;

            // 初始化 entry 状态数组（如果还没有）
            EnsureEntryStates(questName);

            if (debugMode)
                Debug.Log($"[QuestStateStore] SetQuestState: {questName} → {state}");

            // 触发 BroadcastMessage 通知（QuestDataService 依赖这个）
            QuestLog.InformQuestStateChange(questName);
        }

        /// <summary>
        /// CurrentQuestEntryStateOverride delegate 实现
        /// </summary>
        private string GetEntryStateString(string questName, int entryNumber)
        {
            if (entryStates.TryGetValue(questName, out string[] entries))
            {
                int idx = entryNumber - 1; // entry 从 1 开始
                if (idx >= 0 && idx < entries.Length)
                    return entries[idx];
            }
            return "unassigned";
        }

        /// <summary>
        /// SetQuestEntryStateOverride delegate 实现
        /// </summary>
        private void SetEntryStateFromString(string questName, int entryNumber, string state)
        {
            EnsureEntryStates(questName);

            if (entryStates.TryGetValue(questName, out string[] entries))
            {
                int idx = entryNumber - 1;
                if (idx >= 0 && idx < entries.Length)
                {
                    entries[idx] = state;

                    if (debugMode)
                        Debug.Log($"[QuestStateStore] SetQuestEntryState: {questName}[{entryNumber}] → {state}");
                }
            }

            // 触发 entry 变更广播和 tracker 更新
            if (QuestLog.invokeOnQuestStateChangeForEntries)
                QuestLog.InformQuestStateChange(questName);
            QuestLog.InformQuestEntryStateChange(questName, entryNumber);
        }

        #endregion

        #region 公共 API（替代无 override delegate 的 QuestLog 方法）

        /// <summary>
        /// 获取任务状态（类型安全版本）
        /// </summary>
        public QuestState GetQuestState(string questName)
        {
            return QuestLog.StringToState(GetQuestStateString(questName));
        }

        /// <summary>
        /// 获取所有指定状态的任务名称（替代 QuestLog.GetAllQuests）
        /// </summary>
        public string[] GetAllQuests(string stateFilter)
        {
            var result = new List<string>();
            foreach (var kvp in questStates)
            {
                if (string.Equals(kvp.Value, stateFilter, StringComparison.OrdinalIgnoreCase))
                    result.Add(kvp.Key);
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        /// <summary>
        /// 获取所有指定状态的任务名称（QuestState 枚举版本）
        /// </summary>
        public string[] GetAllQuests(QuestState state)
        {
            return GetAllQuests(QuestLog.StateToString(state));
        }

        /// <summary>
        /// 是否启用追踪
        /// </summary>
        public bool IsTrackingEnabled(string questName)
        {
            return trackingFlags.TryGetValue(questName, out bool enabled) && enabled;
        }

        /// <summary>
        /// 设置追踪状态
        /// </summary>
        public void SetTracking(string questName, bool enabled)
        {
            trackingFlags[questName] = enabled;
        }

        #endregion

        #region 文本访问器（从 QuestDefinition SO 读取）

        public string GetTitle(string questName)
        {
            if (defMap.TryGetValue(questName, out var def))
                return def.Title;
            return questName;
        }

        public string GetDescription(string questName)
        {
            if (defMap.TryGetValue(questName, out var def))
                return def.Description;
            return "";
        }

        public string GetSuccessDescription(string questName)
        {
            if (defMap.TryGetValue(questName, out var def))
                return def.SuccessDescription;
            return "";
        }

        public string GetEntry(string questName, int entryNumber)
        {
            if (defMap.TryGetValue(questName, out var def))
            {
                int idx = entryNumber - 1;
                if (def.EntryTexts != null && idx >= 0 && idx < def.EntryTexts.Length)
                    return def.EntryTexts[idx];
            }
            return "";
        }

        public int GetEntryCount(string questName)
        {
            if (defMap.TryGetValue(questName, out var def))
            {
                // AutoComplete 任务不读 condition，UI 视为 0 个子目标
                if (def.AutoCompleteOnActivation) return 0;
                return def.Conditions?.Length ?? 0;
            }
            return 0;
        }

        public string GetGroup(string questName)
        {
            if (defMap.TryGetValue(questName, out var def))
                return def.QuestType.ToString();
            return "Side";
        }

        #endregion

        #region 持久化

        public QuestStateSaveData GetSaveData()
        {
            return new QuestStateSaveData
            {
                questStates = new Dictionary<string, string>(questStates),
                entryStates = DeepCopyEntryStates(),
                trackingFlags = new Dictionary<string, bool>(trackingFlags)
            };
        }

        public void LoadSaveData(QuestStateSaveData data)
        {
            if (data == null) return;
            questStates = data.questStates ?? new();
            entryStates = data.entryStates ?? new();
            trackingFlags = data.trackingFlags ?? new();

            if (debugMode)
                Debug.Log($"[QuestStateStore] 已加载状态: {questStates.Count} 个任务");
        }

        #endregion

        #region 内部辅助

        /// <summary>
        /// 确保某任务有对应的 entry 状态数组
        /// </summary>
        private void EnsureEntryStates(string questName)
        {
            if (entryStates.ContainsKey(questName)) return;

            int count = 0;
            if (defMap.TryGetValue(questName, out var def) && def.Conditions != null)
                count = def.Conditions.Length;

            var entries = new string[count];
            for (int i = 0; i < count; i++)
                entries[i] = "unassigned";

            entryStates[questName] = entries;
        }

        private Dictionary<string, string[]> DeepCopyEntryStates()
        {
            var copy = new Dictionary<string, string[]>();
            foreach (var kvp in entryStates)
            {
                var arr = new string[kvp.Value.Length];
                Array.Copy(kvp.Value, arr, arr.Length);
                copy[kvp.Key] = arr;
            }
            return copy;
        }

        #endregion
    }
}
