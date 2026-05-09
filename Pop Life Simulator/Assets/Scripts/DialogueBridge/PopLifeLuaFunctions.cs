using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Manager;
using PopLife.Customers.Data;
using PopLife.Customers.Spawner;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Registers Pop Life specific Lua functions for use in Dialogue System
    /// Allows dialogue scripts to directly interact with game systems
    ///
    /// Usage in Dialogue Editor Script fields:
    /// - GiveMoney(100)
    /// - GiveFame(50)
    /// - UnlockBlueprint("ShelfVibrator")
    /// - UnlockCustomer("Customer_001")
    /// - RaiseTutorialMarker("FirstShelfPlaced")
    /// - GetMoney() -> returns current money
    /// - GetFame() -> returns current fame
    /// - GetCurrentDay() -> returns current day number
    /// - IsStoreOpen() -> returns true/false
    /// - SetQuestStateAfter("QuestName", "active") -> 延迟到对话结束后才执行 SetQuestState
    /// - SetQuestEntryStateAfter("QuestName", 1, "success") -> 延迟到对话结束后才执行 SetQuestEntryState
    /// </summary>
    public class PopLifeLuaFunctions : MonoBehaviour
    {
        #region Singleton

        public static PopLifeLuaFunctions Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Deferred Quest State

        /// <summary>
        /// 缓存的 SetQuestState / SetQuestEntryState 调用
        /// </summary>
        private struct DeferredQuestStateCall
        {
            public string questName;
            public string state;
            public int entryNumber; // 0 = 主任务状态，>0 = 条目状态
        }

        private readonly List<DeferredQuestStateCall> deferredQuestCalls = new();

        /// <summary>
        /// 缓存的 RaiseMarker 调用（对话结束后执行）
        /// </summary>
        private readonly List<string> deferredMarkerCalls = new();

        private bool isListeningForConversationEnd;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            RegisterAllFunctions();
        }

        private void OnDisable()
        {
            UnregisterAllFunctions();

            if (DialogueManager.instance != null)
                DialogueManager.instance.conversationEnded -= OnConversationEndedFlushQuests;
            isListeningForConversationEnd = false;
        }

        #endregion

        #region Registration

        /// <summary>
        /// Register all custom Lua functions
        /// </summary>
        private void RegisterAllFunctions()
        {
            // Reward functions
            Lua.RegisterFunction("GiveMoney", this, SymbolExtensions.GetMethodInfo(() => GiveMoney(0)));
            Lua.RegisterFunction("GiveFame", this, SymbolExtensions.GetMethodInfo(() => GiveFame(0)));
            Lua.RegisterFunction("UnlockBlueprint", this, SymbolExtensions.GetMethodInfo(() => UnlockBlueprint(string.Empty)));
            Lua.RegisterFunction("UnlockCustomer", this, SymbolExtensions.GetMethodInfo(() => UnlockCustomer(string.Empty)));
            Lua.RegisterFunction("UnlockCalendar", this, SymbolExtensions.GetMethodInfo(() => UnlockCalendar()));
            Lua.RegisterFunction("GiveReward", this, SymbolExtensions.GetMethodInfo(() => GiveReward(string.Empty, string.Empty)));

            // Tutorial marker functions
            Lua.RegisterFunction("RaiseTutorialMarker", this, SymbolExtensions.GetMethodInfo(() => RaiseTutorialMarker(string.Empty)));
            Lua.RegisterFunction("RaiseMarkerAfter", this, SymbolExtensions.GetMethodInfo(() => RaiseMarkerAfter(string.Empty)));
            Lua.RegisterFunction("IsMarkerTriggered", this, SymbolExtensions.GetMethodInfo(() => IsMarkerTriggered(string.Empty)));

            // Query functions
            Lua.RegisterFunction("GetMoney", this, SymbolExtensions.GetMethodInfo(() => GetMoney()));
            Lua.RegisterFunction("GetFame", this, SymbolExtensions.GetMethodInfo(() => GetFame()));
            Lua.RegisterFunction("GetCurrentDay", this, SymbolExtensions.GetMethodInfo(() => GetCurrentDay()));
            Lua.RegisterFunction("IsStoreOpen", this, SymbolExtensions.GetMethodInfo(() => IsStoreOpen()));
            Lua.RegisterFunction("GetCurrentPhase", this, SymbolExtensions.GetMethodInfo(() => GetCurrentPhase()));
            Lua.RegisterFunction("GetCurrentHour", this, SymbolExtensions.GetMethodInfo(() => GetCurrentHour()));

            // Deferred quest state functions（延迟到对话结束后执行）
            Lua.RegisterFunction("SetQuestStateAfter", this,
                SymbolExtensions.GetMethodInfo(() => SetQuestStateAfter(string.Empty, string.Empty)));
            Lua.RegisterFunction("SetQuestEntryStateAfter", this,
                SymbolExtensions.GetMethodInfo(() => SetQuestEntryStateAfter(string.Empty, (double)0, string.Empty)));

            // Utility functions
            Lua.RegisterFunction("PauseGame", this, SymbolExtensions.GetMethodInfo(() => PauseGame()));
            Lua.RegisterFunction("ResumeGame", this, SymbolExtensions.GetMethodInfo(() => ResumeGame()));

            // Spotlight 系统（与 spotlight 渲染层完全解耦：仅此处 PopLifeLuaFunctions 接触 PixelCrushers Lua）
            Lua.RegisterFunction("ShowSpotlight", this,
                SymbolExtensions.GetMethodInfo(() => ShowSpotlight(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)));
            Lua.RegisterFunction("HideSpotlight", this,
                SymbolExtensions.GetMethodInfo(() => HideSpotlight()));

            // Dialogue UI 视觉隐藏/恢复（视觉 only，不影响 dialogue 状态机）
            Lua.RegisterFunction("HideDialogueUI", this,
                SymbolExtensions.GetMethodInfo(() => HideDialogueUI()));
            Lua.RegisterFunction("ShowDialogueUI", this,
                SymbolExtensions.GetMethodInfo(() => ShowDialogueUI()));

            Debug.Log("[PopLifeLuaFunctions] All Lua functions registered");
        }

        /// <summary>
        /// Unregister all custom Lua functions
        /// </summary>
        private void UnregisterAllFunctions()
        {
            Lua.UnregisterFunction("GiveMoney");
            Lua.UnregisterFunction("GiveFame");
            Lua.UnregisterFunction("UnlockBlueprint");
            Lua.UnregisterFunction("UnlockCustomer");
            Lua.UnregisterFunction("UnlockCalendar");
            Lua.UnregisterFunction("GiveReward");
            Lua.UnregisterFunction("RaiseTutorialMarker");
            Lua.UnregisterFunction("RaiseMarkerAfter");
            Lua.UnregisterFunction("IsMarkerTriggered");
            Lua.UnregisterFunction("GetMoney");
            Lua.UnregisterFunction("GetFame");
            Lua.UnregisterFunction("GetCurrentDay");
            Lua.UnregisterFunction("IsStoreOpen");
            Lua.UnregisterFunction("GetCurrentPhase");
            Lua.UnregisterFunction("GetCurrentHour");
            Lua.UnregisterFunction("SetQuestStateAfter");
            Lua.UnregisterFunction("SetQuestEntryStateAfter");
            Lua.UnregisterFunction("PauseGame");
            Lua.UnregisterFunction("ResumeGame");
            Lua.UnregisterFunction("ShowSpotlight");
            Lua.UnregisterFunction("HideSpotlight");
            Lua.UnregisterFunction("HideDialogueUI");
            Lua.UnregisterFunction("ShowDialogueUI");

            Debug.Log("[PopLifeLuaFunctions] All Lua functions unregistered");
        }

        #endregion

        #region Reward Functions

        /// <summary>
        /// Give money to the player
        /// Lua usage: GiveMoney(100)
        /// </summary>
        public void GiveMoney(double amount)
        {
            if (ResourceManager.Instance != null)
            {
                // Use RefundMoney to avoid counting as sales income
                ResourceManager.Instance.RefundMoney((int)amount);
                Debug.Log($"[PopLifeLuaFunctions] Gave {amount} money to player");
            }
            else
            {
                Debug.LogWarning("[PopLifeLuaFunctions] ResourceManager.Instance is null");
            }
        }

        /// <summary>
        /// Give fame to the player
        /// Lua usage: GiveFame(50)
        /// </summary>
        public void GiveFame(double amount)
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddFame((int)amount);
                Debug.Log($"[PopLifeLuaFunctions] Gave {amount} fame to player");
            }
            else
            {
                Debug.LogWarning("[PopLifeLuaFunctions] ResourceManager.Instance is null");
            }
        }

        /// <summary>
        /// Unlock a blueprint (shelf or facility)
        /// Lua usage: UnlockBlueprint("ShelfVibrator")
        /// </summary>
        public void UnlockBlueprint(string blueprintId)
        {
            if (string.IsNullOrEmpty(blueprintId))
            {
                Debug.LogWarning("[PopLifeLuaFunctions] UnlockBlueprint called with empty ID");
                return;
            }

            if (BlueprintManager.Instance != null)
            {
                BlueprintManager.Instance.AddBlueprint(blueprintId);
                Debug.Log($"[PopLifeLuaFunctions] Unlocked blueprint: {blueprintId}");
            }
            else
            {
                Debug.LogWarning("[PopLifeLuaFunctions] BlueprintManager.Instance is null");
            }
        }

        /// <summary>
        /// Unlock a customer for spawning
        /// Lua usage: UnlockCustomer("Customer_001")
        /// </summary>
        public void UnlockCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                Debug.LogWarning("[PopLifeLuaFunctions] UnlockCustomer called with empty ID");
                return;
            }

            var profile = SpawnerProfile.Load();
            if (profile != null)
            {
                if (CustomerSpawner.Instance != null)
                {
                    CustomerSpawner.Instance.UnlockCustomer(customerId);
                    Debug.Log($"[PopLifeLuaFunctions] Unlocked customer through live spawner: {customerId}");
                }
                else if (!profile.unlockedCustomerIds.Contains(customerId))
                {
                    profile.unlockedCustomerIds.Add(customerId);
                    profile.Save();
                    Debug.Log($"[PopLifeLuaFunctions] Unlocked customer: {customerId}");
                }
                else
                {
                    Debug.Log($"[PopLifeLuaFunctions] Customer already unlocked: {customerId}");
                }
            }
            else
            {
                Debug.LogWarning("[PopLifeLuaFunctions] Failed to load SpawnerProfile");
            }
        }

        /// <summary>
        /// 解锁 Calendar 入口（Day3 教程结束时调用）
        /// Lua usage: UnlockCalendar()
        /// </summary>
        public void UnlockCalendar()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.NotifyCalendarUnlocked();
                Debug.Log("[PopLifeLuaFunctions] Calendar unlocked via Lua");
            }
            else
            {
                Debug.LogWarning("[PopLifeLuaFunctions] UnlockCalendar: GameStateManager.Instance is null");
            }
        }

        /// <summary>
        /// Generic reward function
        /// Lua usage: GiveReward("Money", "100") or GiveReward("Blueprint", "ShelfVibrator")
        /// </summary>
        public void GiveReward(string rewardType, string value)
        {
            if (string.IsNullOrEmpty(rewardType))
            {
                Debug.LogWarning("[PopLifeLuaFunctions] GiveReward called with empty type");
                return;
            }

            switch (rewardType.ToLower())
            {
                case "money":
                    if (double.TryParse(value, out double money))
                        GiveMoney(money);
                    break;

                case "fame":
                    if (double.TryParse(value, out double fame))
                        GiveFame(fame);
                    break;

                case "blueprint":
                    UnlockBlueprint(value);
                    break;

                case "customer":
                    UnlockCustomer(value);
                    break;

                default:
                    Debug.LogWarning($"[PopLifeLuaFunctions] Unknown reward type: {rewardType}");
                    break;
            }
        }

        #endregion

        #region Tutorial Marker Functions

        /// <summary>
        /// Raise a tutorial marker
        /// Lua usage: RaiseTutorialMarker("FirstShelfPlaced")
        /// </summary>
        public void RaiseTutorialMarker(string markerName)
        {
            if (string.IsNullOrEmpty(markerName))
            {
                Debug.LogWarning("[PopLifeLuaFunctions] RaiseTutorialMarker called with empty name");
                return;
            }

            if (Enum.TryParse<TutorialMarker>(markerName, out var marker))
            {
                TutorialEventBus.RaiseMarker(marker);
                Debug.Log($"[PopLifeLuaFunctions] Raised tutorial marker: {markerName}");
            }
            else
            {
                Debug.LogWarning($"[PopLifeLuaFunctions] Unknown tutorial marker: {markerName}");
            }
        }

        /// <summary>
        /// Check if a tutorial marker has been triggered
        /// Lua usage: IsMarkerTriggered("FirstShelfPlaced")
        /// </summary>
        public bool IsMarkerTriggered(string markerName)
        {
            if (string.IsNullOrEmpty(markerName))
            {
                return false;
            }

            if (Enum.TryParse<TutorialMarker>(markerName, out var marker))
            {
                return TutorialEventBus.IsMarkerTriggered(marker);
            }

            Debug.LogWarning($"[PopLifeLuaFunctions] Unknown tutorial marker: {markerName}");
            return false;
        }

        #endregion

        #region Query Functions

        /// <summary>
        /// Get current player money
        /// Lua usage: Variable["Money"] = GetMoney()
        /// </summary>
        public double GetMoney()
        {
            return ResourceManager.Instance?.GetMoney() ?? 0;
        }

        /// <summary>
        /// Get current player fame
        /// Lua usage: Variable["Fame"] = GetFame()
        /// </summary>
        public double GetFame()
        {
            return ResourceManager.Instance?.GetFame() ?? 0;
        }

        /// <summary>
        /// Get current game day
        /// Lua usage: Variable["CurrentDay"] = GetCurrentDay()
        /// </summary>
        public double GetCurrentDay()
        {
            return DayLoopManager.Instance?.currentDay ?? 1;
        }

        /// <summary>
        /// Check if store is currently open
        /// Lua usage: if IsStoreOpen() then ...
        /// </summary>
        public bool IsStoreOpen()
        {
            return DayLoopManager.Instance?.isStoreOpen ?? false;
        }

        /// <summary>
        /// Get current game phase as string
        /// Lua usage: Variable["Phase"] = GetCurrentPhase()
        /// Returns: "BuildPhase" or "OpenPhase"
        /// </summary>
        public string GetCurrentPhase()
        {
            if (DayLoopManager.Instance != null)
            {
                return DayLoopManager.Instance.currentPhase.ToString();
            }
            return "BuildPhase";
        }

        /// <summary>
        /// Get current game hour (0-24)
        /// Lua usage: Variable["Hour"] = GetCurrentHour()
        /// </summary>
        public double GetCurrentHour()
        {
            return DayLoopManager.Instance?.currentHour ?? 6;
        }

        #endregion

        #region Utility Functions

        /// <summary>
        /// Pause the game (set timeScale to 0)
        /// Lua usage: PauseGame()
        /// </summary>
        public void PauseGame()
        {
            Time.timeScale = 0f;
            Debug.Log("[PopLifeLuaFunctions] Game paused");
        }

        /// <summary>
        /// Resume the game (set timeScale to 1)
        /// Lua usage: ResumeGame()
        /// </summary>
        public void ResumeGame()
        {
            Time.timeScale = 1f;
            Debug.Log("[PopLifeLuaFunctions] Game resumed");
        }

        #endregion

        #region Deferred Marker Functions

        /// <summary>
        /// 延迟版 RaiseTutorialMarker - 对话结束后触发 marker
        /// 适合在 conversation 最后一个节点使用，让 OperationGuide 在对话 UI 消失后弹出
        /// Lua usage: RaiseMarkerAfter("FirstShelfPlaced")
        /// </summary>
        public void RaiseMarkerAfter(string markerName)
        {
            if (string.IsNullOrEmpty(markerName)) return;

            if (DialogueManager.isConversationActive)
            {
                deferredMarkerCalls.Add(markerName);
                EnsureListeningForConversationEnd();
                Debug.Log($"[PopLifeLuaFunctions] Deferred RaiseMarker: {markerName} (waiting for conversation end)");
            }
            else
            {
                // 不在对话中，立即执行
                RaiseTutorialMarker(markerName);
            }
        }

        #endregion

        #region Deferred Quest State Functions

        /// <summary>
        /// 延迟版 SetQuestState - 对话进行中缓存调用，对话结束后批量执行
        /// 如果不在对话中调用，则立即执行
        /// Lua usage: SetQuestStateAfter("QuestName", "active")
        /// </summary>
        public void SetQuestStateAfter(string questName, string state)
        {
            if (string.IsNullOrEmpty(questName)) return;

            if (DialogueManager.isConversationActive)
            {
                deferredQuestCalls.Add(new DeferredQuestStateCall
                {
                    questName = questName,
                    state = state,
                    entryNumber = 0
                });
                EnsureListeningForConversationEnd();
                Debug.Log($"[PopLifeLuaFunctions] Deferred SetQuestState: {questName} = {state} (waiting for conversation end)");
            }
            else
            {
                // 不在对话中，立即执行
                QuestLog.SetQuestState(questName, state);
            }
        }

        /// <summary>
        /// 延迟版 SetQuestEntryState - 对话进行中缓存调用，对话结束后批量执行
        /// 如果不在对话中调用，则立即执行
        /// Lua usage: SetQuestEntryStateAfter("QuestName", 1, "success")
        /// 注意：Lua 传递的 entryNumber 是 double 类型
        /// </summary>
        public void SetQuestEntryStateAfter(string questName, double entryNumber, string state)
        {
            if (string.IsNullOrEmpty(questName)) return;

            if (DialogueManager.isConversationActive)
            {
                deferredQuestCalls.Add(new DeferredQuestStateCall
                {
                    questName = questName,
                    state = state,
                    entryNumber = (int)entryNumber
                });
                EnsureListeningForConversationEnd();
                Debug.Log($"[PopLifeLuaFunctions] Deferred SetQuestEntryState: {questName}[{(int)entryNumber}] = {state} (waiting for conversation end)");
            }
            else
            {
                QuestLog.SetQuestEntryState(questName, (int)entryNumber, state);
            }
        }

        /// <summary>
        /// 确保已订阅 conversationEnded 事件（避免重复订阅）
        /// </summary>
        private void EnsureListeningForConversationEnd()
        {
            if (isListeningForConversationEnd) return;
            if (DialogueManager.instance == null) return;

            DialogueManager.instance.conversationEnded += OnConversationEndedFlushQuests;
            isListeningForConversationEnd = true;
        }

        /// <summary>
        /// 对话结束后批量执行缓存的 SetQuestState 和 RaiseMarker 调用
        /// </summary>
        private void OnConversationEndedFlushQuests(Transform actor)
        {
            if (DialogueManager.instance != null)
                DialogueManager.instance.conversationEnded -= OnConversationEndedFlushQuests;
            isListeningForConversationEnd = false;

            // 先处理 quest state 调用
            if (deferredQuestCalls.Count > 0)
            {
                Debug.Log($"[PopLifeLuaFunctions] Flushing {deferredQuestCalls.Count} deferred quest state call(s)");

                // 复制列表后清空，防止回调中再次触发追加
                var calls = new List<DeferredQuestStateCall>(deferredQuestCalls);
                deferredQuestCalls.Clear();

                foreach (var call in calls)
                {
                    if (call.entryNumber > 0)
                    {
                        QuestLog.SetQuestEntryState(call.questName, call.entryNumber, call.state);
                        Debug.Log($"[PopLifeLuaFunctions] Executed deferred SetQuestEntryState: {call.questName}[{call.entryNumber}] = {call.state}");
                    }
                    else
                    {
                        QuestLog.SetQuestState(call.questName, call.state);
                        Debug.Log($"[PopLifeLuaFunctions] Executed deferred SetQuestState: {call.questName} = {call.state}");
                    }
                }
            }

            // 再处理 marker 调用
            if (deferredMarkerCalls.Count > 0)
            {
                Debug.Log($"[PopLifeLuaFunctions] Flushing {deferredMarkerCalls.Count} deferred marker call(s)");

                var markers = new List<string>(deferredMarkerCalls);
                deferredMarkerCalls.Clear();

                foreach (var markerName in markers)
                {
                    RaiseTutorialMarker(markerName);
                    Debug.Log($"[PopLifeLuaFunctions] Executed deferred RaiseMarker: {markerName}");
                }
            }
        }

        #endregion

        #region Spotlight Functions

        /// <summary>
        /// 显示 spotlight + tooltip。Lua 用法：
        /// <code>
        /// ShowSpotlight("BuildButton", "Click here to build", "Right", "RoundedRectangle", "passthrough")
        /// ShowSpotlight("QuestPanel", "Quest list lives here", "Auto", "Rectangle", "blocking")
        /// ShowSpotlight("BuildButton", "NULL", "Right", "RoundedRectangle", "passthrough")  -- 仅高亮，不显示 tooltip
        /// </code>
        ///
        /// targetId 智能解析：
        /// <list type="bullet">
        /// <item>"BuildButton" / "id:BuildButton" → 注册表查找</item>
        /// <item>"rect:100,200,300,150" → 屏幕像素坐标</item>
        /// <item>"norm:0.4,0.4,0.2,0.2" → normalized 坐标</item>
        /// </list>
        ///
        /// text 取值：
        /// <list type="bullet">
        /// <item>普通字符串 → 显示 tooltip</item>
        /// <item>"NULL" → 仅 spotlight 不显示 tooltip</item>
        /// <item>空字符串 → throw（被 catch 后日志报错，spotlight 不显示）</item>
        /// </list>
        ///
        /// 异常处理：核心 SpotlightManager API 对空 text 等会 throw；此 wrapper 吞掉异常仅日志，
        /// 避免对话流程被参数错误打断。
        /// </summary>
        public void ShowSpotlight(string targetId, string text, string position, string shape, string mode)
        {
            try
            {
                if (SpotlightManager.Instance == null)
                {
                    Debug.LogWarning("[PopLifeLuaFunctions] ShowSpotlight: SpotlightManager.Instance is null");
                    return;
                }

                // 不在此处替换空 text —— 让核心 API 的 Validate() 抛出 ArgumentException，
                // 由下方 catch 记录 warning 并 no-op。这样 DialogueDatabase 误填空 text 会被
                // 明确日志报错，而非显示一个空白 tooltip 掩盖错误。
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ParseSmart(targetId),
                    text = text,
                    position = ParseTooltipPosition(position),
                    shape = ParseSpotlightShape(shape),
                    mode = ParseInteractionMode(mode),
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PopLifeLuaFunctions] ShowSpotlight failed: {ex.Message}");
            }
        }

        /// <summary>关闭 spotlight + tooltip。Lua 用法: HideSpotlight()</summary>
        public void HideSpotlight()
        {
            try
            {
                SpotlightManager.Instance?.Hide(CloseReason.Manual);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PopLifeLuaFunctions] HideSpotlight failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 视觉隐藏 Dialogue UI（CanvasGroup alpha=0 + raycast off）。Lua 用法: HideDialogueUI()
        /// 不影响 Dialogue System 状态机；由 ShowDialogueUI() 恢复。
        /// </summary>
        public void HideDialogueUI()
        {
            try
            {
                DialogueUIVisibility.Hide();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PopLifeLuaFunctions] HideDialogueUI failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复 Dialogue UI 到 HideDialogueUI() 之前的状态。Lua 用法: ShowDialogueUI()
        /// 未 Hide 过则 noop。
        /// </summary>
        public void ShowDialogueUI()
        {
            try
            {
                DialogueUIVisibility.Show();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PopLifeLuaFunctions] ShowDialogueUI failed: {ex.Message}");
            }
        }

        // ============== 参数解析（容错友好，未识别值走默认） ==============

        private static TooltipPosition ParseTooltipPosition(string s)
        {
            if (string.IsNullOrEmpty(s)) return TooltipPosition.Auto;
            if (Enum.TryParse<TooltipPosition>(s, true, out var v)) return v;
            return TooltipPosition.Auto;
        }

        private static SpotlightShape ParseSpotlightShape(string s)
        {
            if (string.IsNullOrEmpty(s)) return SpotlightShape.RoundedRectangle;
            if (Enum.TryParse<SpotlightShape>(s, true, out var v)) return v;
            return SpotlightShape.RoundedRectangle;
        }

        private static InteractionMode ParseInteractionMode(string s)
        {
            if (string.IsNullOrEmpty(s)) return InteractionMode.Passthrough;
            if (Enum.TryParse<InteractionMode>(s, true, out var v)) return v;
            return InteractionMode.Passthrough;
        }

        #endregion
    }
}
