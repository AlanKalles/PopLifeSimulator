using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Manager;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Synchronizes game state to Dialogue System Lua variables
    /// This allows dialogue conditions to access current game state
    ///
    /// Synced Variables (accessible in Dialogue Editor conditions):
    /// - Variable["Money"] : current player money
    /// - Variable["Fame"] : current player fame
    /// - Variable["CurrentDay"] : current game day
    /// - Variable["CurrentHour"] : current game hour (0-24)
    /// - Variable["IsStoreOpen"] : true if store is open
    /// - Variable["GamePhase"] : "BuildPhase" or "OpenPhase"
    /// - Variable["TotalShelvesPlaced"] : total shelves placed (from GameStateManager)
    /// - Variable["TotalCustomersServed"] : total customers served
    /// </summary>
    public class GameStateLuaSync : MonoBehaviour
    {
        #region Singleton

        public static GameStateLuaSync Instance { get; private set; }

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

        #region Serialized Fields

        [Title("Sync Settings")]
        [InfoBox("Configure how often game state is synced to Lua variables")]

        [Tooltip("Sync automatically on conversation start")]
        [SerializeField]
        private bool syncOnConversationStart = true;

        [Tooltip("Sync automatically on store open/close")]
        [SerializeField]
        private bool syncOnStoreStateChange = true;

        [Tooltip("Sync automatically on day change")]
        [SerializeField]
        private bool syncOnDayChange = true;

        [Tooltip("Sync automatically when resources change")]
        [SerializeField]
        private bool syncOnResourceChange = false;

        [Title("Debug")]
        [SerializeField]
        private bool debugMode = false;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to Dialogue System events
            DialogueManager.instance.conversationStarted += OnConversationStarted;

            // Subscribe to game events
            if (syncOnStoreStateChange && DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen += OnStoreStateChanged;
                DayLoopManager.Instance.OnStoreClose += OnStoreStateChanged;
                DayLoopManager.Instance.OnBuildPhaseStart += OnStoreStateChanged;
            }

            if (syncOnDayChange && DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged += OnDayChanged;
            }

            // Initial sync
            SyncAllVariables();
        }

        private void OnDisable()
        {
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationStarted -= OnConversationStarted;
            }

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen -= OnStoreStateChanged;
                DayLoopManager.Instance.OnStoreClose -= OnStoreStateChanged;
                DayLoopManager.Instance.OnBuildPhaseStart -= OnStoreStateChanged;
                DayLoopManager.Instance.OnDayChanged -= OnDayChanged;
            }
        }

        private void Start()
        {
            // Delayed subscription in case managers initialize after this component
            if (syncOnStoreStateChange && DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen -= OnStoreStateChanged;
                DayLoopManager.Instance.OnStoreClose -= OnStoreStateChanged;
                DayLoopManager.Instance.OnBuildPhaseStart -= OnStoreStateChanged;

                DayLoopManager.Instance.OnStoreOpen += OnStoreStateChanged;
                DayLoopManager.Instance.OnStoreClose += OnStoreStateChanged;
                DayLoopManager.Instance.OnBuildPhaseStart += OnStoreStateChanged;
            }

            if (syncOnDayChange && DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged -= OnDayChanged;
                DayLoopManager.Instance.OnDayChanged += OnDayChanged;
            }

            // Sync again after all managers are ready
            SyncAllVariables();
        }

        #endregion

        #region Event Handlers

        private void OnConversationStarted(Transform actor)
        {
            if (syncOnConversationStart)
            {
                SyncAllVariables();
            }
        }

        private void OnStoreStateChanged()
        {
            SyncAllVariables();
        }

        private void OnDayChanged(int day)
        {
            SyncAllVariables();
        }

        #endregion

        #region Sync Methods

        /// <summary>
        /// Sync all game state variables to Lua
        /// </summary>
        [Button("Sync All Variables")]
        public void SyncAllVariables()
        {
            SyncResourceVariables();
            SyncTimeVariables();
            SyncGameStateVariables();

            if (debugMode)
            {
                Debug.Log("[GameStateLuaSync] All variables synced to Lua");
            }
        }

        /// <summary>
        /// Sync resource-related variables (Money, Fame)
        /// </summary>
        public void SyncResourceVariables()
        {
            if (ResourceManager.Instance != null)
            {
                DialogueLua.SetVariable("Money", ResourceManager.Instance.GetMoney());
                DialogueLua.SetVariable("Fame", ResourceManager.Instance.GetFame());
                DialogueLua.SetVariable("TotalIncome", ResourceManager.Instance.GetTotalIncome());
                DialogueLua.SetVariable("TotalExpenses", ResourceManager.Instance.GetTotalExpenses());
            }
            else
            {
                // Set defaults if manager not available
                DialogueLua.SetVariable("Money", 0);
                DialogueLua.SetVariable("Fame", 0);
                DialogueLua.SetVariable("TotalIncome", 0);
                DialogueLua.SetVariable("TotalExpenses", 0);
            }
        }

        /// <summary>
        /// Sync time-related variables (Day, Hour, Phase)
        /// </summary>
        public void SyncTimeVariables()
        {
            if (DayLoopManager.Instance != null)
            {
                DialogueLua.SetVariable("CurrentDay", DayLoopManager.Instance.currentDay);
                DialogueLua.SetVariable("CurrentHour", DayLoopManager.Instance.currentHour);
                DialogueLua.SetVariable("IsStoreOpen", DayLoopManager.Instance.isStoreOpen);
                DialogueLua.SetVariable("GamePhase", DayLoopManager.Instance.currentPhase.ToString());
            }
            else
            {
                DialogueLua.SetVariable("CurrentDay", 1);
                DialogueLua.SetVariable("CurrentHour", 6);
                DialogueLua.SetVariable("IsStoreOpen", false);
                DialogueLua.SetVariable("GamePhase", "BuildPhase");
            }
        }

        /// <summary>
        /// Sync game state variables (from GameStateManager)
        /// </summary>
        public void SyncGameStateVariables()
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null)
            {
                DialogueLua.SetVariable("TotalShelvesPlaced", gsm.totalShelvesPlaced);
                DialogueLua.SetVariable("TotalCustomersServed", gsm.totalCustomersServed);
                DialogueLua.SetVariable("HasPlacedFirstShelf", gsm.hasPlacedFirstShelf);
                DialogueLua.SetVariable("HasOpenedStore", gsm.hasOpenedStore);
                DialogueLua.SetVariable("HasServedFirstCustomer", gsm.hasServedFirstCustomer);
                DialogueLua.SetVariable("HasEarnedFirstFame", gsm.hasEarnedFirstFame);
            }
            else
            {
                DialogueLua.SetVariable("TotalShelvesPlaced", 0);
                DialogueLua.SetVariable("TotalCustomersServed", 0);
                DialogueLua.SetVariable("HasPlacedFirstShelf", false);
                DialogueLua.SetVariable("HasOpenedStore", false);
                DialogueLua.SetVariable("HasServedFirstCustomer", false);
                DialogueLua.SetVariable("HasEarnedFirstFame", false);
            }
        }

        /// <summary>
        /// Sync customer data for a specific customer (call before customer dialogue)
        /// </summary>
        public void SyncCustomerData(string customerId, string customerName,
            int loyaltyLevel, int visitCount, float lifetimeSpent, string traitIds)
        {
            DialogueLua.SetVariable("CurrentCustomer_ID", customerId);
            DialogueLua.SetVariable("CurrentCustomer_Name", customerName);
            DialogueLua.SetVariable("CurrentCustomer_LoyaltyLevel", loyaltyLevel);
            DialogueLua.SetVariable("CurrentCustomer_VisitCount", visitCount);
            DialogueLua.SetVariable("CurrentCustomer_LifetimeSpent", lifetimeSpent);
            DialogueLua.SetVariable("CurrentCustomer_Traits", traitIds);

            if (debugMode)
            {
                Debug.Log($"[GameStateLuaSync] Synced customer data: {customerId} ({customerName})");
            }
        }

        /// <summary>
        /// Clear customer data (call after customer dialogue ends)
        /// </summary>
        public void ClearCustomerData()
        {
            DialogueLua.SetVariable("CurrentCustomer_ID", "");
            DialogueLua.SetVariable("CurrentCustomer_Name", "");
            DialogueLua.SetVariable("CurrentCustomer_LoyaltyLevel", 0);
            DialogueLua.SetVariable("CurrentCustomer_VisitCount", 0);
            DialogueLua.SetVariable("CurrentCustomer_LifetimeSpent", 0);
            DialogueLua.SetVariable("CurrentCustomer_Traits", "");
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Print Current Lua Variables")]
        private void PrintLuaVariables()
        {
            Debug.Log("=== Dialogue System Lua Variables ===");
            Debug.Log($"Money: {DialogueLua.GetVariable("Money").asInt}");
            Debug.Log($"Fame: {DialogueLua.GetVariable("Fame").asInt}");
            Debug.Log($"CurrentDay: {DialogueLua.GetVariable("CurrentDay").asInt}");
            Debug.Log($"CurrentHour: {DialogueLua.GetVariable("CurrentHour").asFloat}");
            Debug.Log($"IsStoreOpen: {DialogueLua.GetVariable("IsStoreOpen").asBool}");
            Debug.Log($"GamePhase: {DialogueLua.GetVariable("GamePhase").asString}");
            Debug.Log($"TotalShelvesPlaced: {DialogueLua.GetVariable("TotalShelvesPlaced").asInt}");
            Debug.Log($"TotalCustomersServed: {DialogueLua.GetVariable("TotalCustomersServed").asInt}");
        }
#endif

        #endregion
    }
}
