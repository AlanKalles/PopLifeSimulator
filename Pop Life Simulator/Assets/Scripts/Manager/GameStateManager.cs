using UnityEngine;
using PopLife;
using PopLife.Utility;
using System;
using System.IO;
using System.Collections.Generic;

namespace PopLife.Manager
{
    /// <summary>
    /// Global game state tracker for tutorial and dialogue triggers
    /// 全局游戏状态追踪器，用于教程和对话触发
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        [Header("Debug")]
        [Tooltip("勾选后每次Play时自动清除所有存档，适合Playtest")]
        [SerializeField] bool clearSaveOnStart = false;

        // Tutorial state tracking
        [Header("Tutorial States")]
        public bool hasEnteredBuildMode = false;
        public bool hasEnteredPlaceMode = false;
        public bool hasEnteredPlaceModeSecondTime = false;
        public bool hasPlacedFirstShelf = false;
        public bool hasOpenedStore = false;
        public bool hasServedFirstCustomer = false;
        public bool hasEarnedFirstFame = false;
        public bool hasEnteredMoveMode = false;
        public bool hasEnteredDestroyMode = false;
        public bool hasCompletedFirstRequest = false;
        public bool hasCompletedFirstDay = false;
        public bool hasOpenedCalendar = false;
        public bool hasOpenedCustomerCodex = false;
        public bool hasOpenedItemCodex = false;
        public bool hasDismissedFirstQuestToast = false;

        // Game progress tracking
        [Header("Game Progress")]
        public int totalShelvesPlaced = 0;
        public int totalCustomersServed = 0;
        public int totalFameEarned = 0;
        public int currentDay = 0;

        // Events for tutorial system to subscribe
        public event Action OnBuildModeEntered;
        public event Action OnPlaceModeEntered;
        public event Action OnFirstShelfPlaced;
        public event Action OnStoreOpened;
        public event Action OnFirstCustomerServed;
        public event Action OnFirstFameEarned;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (clearSaveOnStart)
                {
                    ClearAllSaves();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to existing game events
            SubscribeToGameEvents();

            // Raise game started marker
            TutorialEventBus.RaiseMarker(TutorialMarker.GameStarted);
        }

        private void SubscribeToGameEvents()
        {
            // Listen to DayLoopManager events
            if (DayLoopManager.Instance != null)
            {
                // OnBuildPhaseStart subscription removed - now triggered by ShelfListPanel
                // DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStarted;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpenedEvent;
            }

            // Listen to ResourceManager events (if available)
            // Note: You may need to add events to ResourceManager if not present
        }

        private void OnDestroy()
        {
            if (DayLoopManager.Instance != null)
            {
                // DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStarted;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpenedEvent;
            }
        }

        // Called when player opens the shelf list panel for the first time
        // This method is now called by ShelfListPanel instead of DayLoopManager
        public void NotifyBuildModeFirstEntered()
        {
            if (!hasEnteredBuildMode)
            {
                hasEnteredBuildMode = true;
                OnBuildModeEntered?.Invoke();

                // Raise tutorial marker
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstBuildPhaseEntered);

                Debug.Log("[GameState] Build mode (ShelfListPanel) entered for the first time");
            }
        }

        /// <summary>
        /// Call when player enters place mode in ConstructionManager for the first time
        /// 当玩家首次进入建造Place模式时调用
        /// </summary>
        public void NotifyPlaceModeEntered()
        {
            if (!hasEnteredPlaceMode)
            {
                hasEnteredPlaceMode = true;
                OnPlaceModeEntered?.Invoke();

                // Raise tutorial marker
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstTimeBuild);

                Debug.Log("[GameState] Place mode entered for the first time");
            }
            else if (!hasEnteredPlaceModeSecondTime && hasPlacedFirstShelf)
            {
                // 第二次进入place mode（第一个货架已放置后）
                hasEnteredPlaceModeSecondTime = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.BeforeTwoShelvesPlaced);

                Debug.Log("[GameState] Place mode entered for the second time");
            }
        }

        // Called when store opens
        private void OnStoreOpenedEvent()
        {
            if (!hasOpenedStore)
            {
                hasOpenedStore = true;
                OnStoreOpened?.Invoke();

                // Raise tutorial marker
                TutorialEventBus.RaiseMarker(TutorialMarker.StoreOpened);

                Debug.Log("[GameState] Store opened for the first time");
            }
        }

        // Public methods for other systems to notify state changes

        /// <summary>
        /// Call when a shelf is placed
        /// </summary>
        public void NotifyShelfPlaced()
        {
            totalShelvesPlaced++;

            if (!hasPlacedFirstShelf)
            {
                hasPlacedFirstShelf = true;
                OnFirstShelfPlaced?.Invoke();

                // Raise tutorial marker
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstShelfPlaced);

                Debug.Log("[GameState] First shelf placed");
            }

            // Check for 2 shelves placed
            if (totalShelvesPlaced == 2)
            {
                TutorialEventBus.RaiseMarker(TutorialMarker.TwoShelvesPlaced);
                Debug.Log("[GameState] Two shelves placed");
            }
        }

        /// <summary>
        /// Call when a customer completes checkout
        /// </summary>
        public void NotifyCustomerServed()
        {
            totalCustomersServed++;

            if (!hasServedFirstCustomer)
            {
                hasServedFirstCustomer = true;
                OnFirstCustomerServed?.Invoke();

                // Raise tutorial marker
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstCustomerCheckedOut);

                Debug.Log("[GameState] First customer served");
            }
        }

        /// <summary>
        /// Call when player enters Move mode for the first time
        /// 当玩家首次进入Move模式时调用
        /// </summary>
        public void NotifyMoveModeEntered()
        {
            if (!hasEnteredMoveMode)
            {
                hasEnteredMoveMode = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstMoveMode);
                Debug.Log("[GameState] Move mode entered for the first time");
            }
        }

        /// <summary>
        /// Call when player enters Destroy mode for the first time
        /// 当玩家首次进入Destroy模式时调用
        /// </summary>
        public void NotifyDestroyModeEntered()
        {
            if (!hasEnteredDestroyMode)
            {
                hasEnteredDestroyMode = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstDestroyMode);
                Debug.Log("[GameState] Destroy mode entered for the first time");
            }
        }

        /// <summary>
        /// Call when fame is earned
        /// </summary>
        public void NotifyFameEarned(int amount)
        {
            totalFameEarned += amount;

            if (!hasEarnedFirstFame && totalFameEarned > 0)
            {
                hasEarnedFirstFame = true;
                OnFirstFameEarned?.Invoke();

                // Raise tutorial marker
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstFameEarned);

                Debug.Log("[GameState] First fame earned");
            }
        }

        /// <summary>
        /// Call when the first day settlement is confirmed (player clicks Continue)
        /// 当首日结算确认（玩家按下Continue）时调用
        /// </summary>
        public void NotifyFirstDayCompleted()
        {
            if (!hasCompletedFirstDay)
            {
                hasCompletedFirstDay = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstDayCompleted);
                Debug.Log("[GameState] First day completed");
            }
        }

        /// <summary>
        /// 首次打开日历面板时调用
        /// </summary>
        public void NotifyCalendarOpened()
        {
            if (!hasOpenedCalendar)
            {
                hasOpenedCalendar = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstCalendarOpened);
                Debug.Log("[GameState] Calendar opened for the first time");
            }
        }

        /// <summary>
        /// 首次打开顾客图鉴面板时调用
        /// </summary>
        public void NotifyCustomerCodexOpened()
        {
            if (!hasOpenedCustomerCodex)
            {
                hasOpenedCustomerCodex = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstCustomerCodexOpened);
                Debug.Log("[GameState] Customer Codex opened for the first time");
            }
        }

        /// <summary>
        /// 首次打开物品图鉴面板时调用
        /// </summary>
        public void NotifyItemCodexOpened()
        {
            if (!hasOpenedItemCodex)
            {
                hasOpenedItemCodex = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstItemCodexOpened);
                Debug.Log("[GameState] Item Codex opened for the first time");
            }
        }

        /// <summary>
        /// 首个任务通知Toast完全消失时调用
        /// </summary>
        public void NotifyFirstQuestToastDismissed()
        {
            if (!hasDismissedFirstQuestToast)
            {
                hasDismissedFirstQuestToast = true;
                TutorialEventBus.RaiseMarker(TutorialMarker.FirstQuestToastDismissed);
                Debug.Log("[GameState] First quest toast dismissed");
            }
        }

        /// <summary>
        /// Reset tutorial states (for testing)
        /// </summary>
        [ContextMenu("Reset Tutorial States")]
        public void ResetTutorialStates()
        {
            hasEnteredBuildMode = false;
            hasEnteredPlaceMode = false;
            hasEnteredPlaceModeSecondTime = false;
            hasPlacedFirstShelf = false;
            hasOpenedStore = false;
            hasServedFirstCustomer = false;
            hasEarnedFirstFame = false;
            hasEnteredMoveMode = false;
            hasEnteredDestroyMode = false;
            hasCompletedFirstRequest = false;
            hasCompletedFirstDay = false;
            hasOpenedCalendar = false;
            hasOpenedCustomerCodex = false;
            hasOpenedItemCodex = false;
            hasDismissedFirstQuestToast = false;

            totalShelvesPlaced = 0;
            totalCustomersServed = 0;
            totalFameEarned = 0;

            Debug.Log("[GameState] Tutorial states reset");
        }

        /// <summary>
        /// 清除所有存档数据，恢复到初始状态
        /// 包括：ES3存档、JSON运行时数据、教程状态
        /// </summary>
        [ContextMenu("Clear All Saves")]
        public void ClearAllSaves()
        {
            Debug.Log("[GameState] ========== 清除所有存档 ==========");

            // 1. 清除 ES3 存档文件
            string[] es3Files = {
                "SettlementHistory.es3",
                "AlanBot.es3",
                "OperationGuides.es3",
                "QuestProgress.es3",
                "ItemCodex.es3",
                "CustomerCodex.es3"
            };
            foreach (var file in es3Files)
            {
                try
                {
                    if (ES3.FileExists(file))
                    {
                        ES3.DeleteFile(file);
                        Debug.Log($"[GameState] 已删除 ES3 文件: {file}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GameState] 删除 {file} 失败: {e.Message}");
                }
            }

            // LotteryManager 的 Awake (-40) 早于 GameStateManager (0)，内存已加载旧存档。
            // 必须同时重置内存与磁盘，否则下一次 SaveState 会把旧数据写回。
            if (LotteryManager.Instance != null)
            {
                LotteryManager.Instance.ClearSaveAndResetState();
                Debug.Log("[GameState] 已重置 Lottery 存档与内存状态");
            }
            else
            {
                LotteryManager.ClearSave();
                Debug.Log("[GameState] LotteryManager 未初始化，仅删除了 Lottery.es3 文件");
            }

            // 2. 重置 Customers.json（清除运行时统计数据，保留身份信息）
            ResetCustomersJson();

            // 3. 重置 Dialogue System 数据（Lua变量和Quest状态）
            try
            {
                PixelCrushers.DialogueSystem.PersistentDataManager.Reset();
                Debug.Log("[GameState] 已重置 Dialogue System 数据");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameState] 重置 Dialogue System 失败: {e.Message}");
            }

            // 4. 重置教程状态
            ResetTutorialStates();

            Debug.Log("[GameState] ========== 存档清除完成 ==========");
        }

        /// <summary>
        /// 重置 Customers.json 中的运行时数据（访问次数、忠诚度、经验等），保留身份信息
        /// </summary>
        void ResetCustomersJson()
        {
            try
            {
                string path = SavePathManager.GetWritePath("Customers.json");
                if (!File.Exists(path))
                {
                    Debug.LogWarning("[GameState] Customers.json 不存在，跳过重置");
                    return;
                }

                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<CustomerListWrapper>(json);
                if (wrapper?.items == null) return;

                foreach (var record in wrapper.items)
                {
                    record.trust = 0;
                    record.loyaltyLevel = 1;
                    record.xp = 0;
                    record.visitCount = 0;
                    record.lastVisitDay = "";
                    record.lastLeaveReason = "";
                    record.lifetimeSpent = 0;
                }

                string resetJson = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(path, resetJson);
                Debug.Log($"[GameState] 已重置 Customers.json（{wrapper.items.Length} 条记录）");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameState] 重置 Customers.json 失败: {e.Message}");
            }
        }

        /// <summary>
        /// Customers.json 反序列化包装类（与 CustomerRepository 格式一致）
        /// </summary>
        [Serializable]
        class CustomerListWrapper
        {
            public CustomerItem[] items;
        }

        /// <summary>
        /// 仅包含需要重置的字段，其余字段自动保留
        /// </summary>
        [Serializable]
        class CustomerItem
        {
            // 身份字段（保留不动）
            public string customerId;
            public string name;
            public string bio;
            public string archetypeId;
            public string[] favoriteShelfIds;
            public string[] availableNarrativeIds;
            public int walletCapBase;
            public int schemaVersion;

            // 运行时字段（需要重置）
            public int trust;
            public int loyaltyLevel;
            public int xp;
            public int visitCount;
            public string lastVisitDay;
            public string lastLeaveReason;
            public int lifetimeSpent;
        }
    }
}
