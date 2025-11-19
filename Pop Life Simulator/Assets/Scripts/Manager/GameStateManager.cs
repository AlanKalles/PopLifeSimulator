using UnityEngine;
using PopLife;
using System;

namespace PopLife.Manager
{
    /// <summary>
    /// Global game state tracker for tutorial and dialogue triggers
    /// 全局游戏状态追踪器，用于教程和对话触发
    /// </summary>
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        // Tutorial state tracking
        [Header("Tutorial States")]
        public bool hasEnteredBuildMode = false;
        public bool hasEnteredPlaceMode = false;
        public bool hasEnteredPlaceModeSecondTime = false;
        public bool hasPlacedFirstShelf = false;
        public bool hasOpenedStore = false;
        public bool hasServedFirstCustomer = false;
        public bool hasEarnedFirstFame = false;
        public bool hasCompletedFirstRequest = false;

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
            hasCompletedFirstRequest = false;

            totalShelvesPlaced = 0;
            totalCustomersServed = 0;
            totalFameEarned = 0;

            Debug.Log("[GameState] Tutorial states reset");
        }
    }
}
