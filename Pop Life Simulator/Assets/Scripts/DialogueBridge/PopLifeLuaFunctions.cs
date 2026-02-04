using System;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Manager;
using PopLife.Customers.Data;

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

        #region Unity Lifecycle

        private void OnEnable()
        {
            RegisterAllFunctions();
        }

        private void OnDisable()
        {
            UnregisterAllFunctions();
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
            Lua.RegisterFunction("GiveReward", this, SymbolExtensions.GetMethodInfo(() => GiveReward(string.Empty, string.Empty)));

            // Tutorial marker functions
            Lua.RegisterFunction("RaiseTutorialMarker", this, SymbolExtensions.GetMethodInfo(() => RaiseTutorialMarker(string.Empty)));
            Lua.RegisterFunction("IsMarkerTriggered", this, SymbolExtensions.GetMethodInfo(() => IsMarkerTriggered(string.Empty)));

            // Query functions
            Lua.RegisterFunction("GetMoney", this, SymbolExtensions.GetMethodInfo(() => GetMoney()));
            Lua.RegisterFunction("GetFame", this, SymbolExtensions.GetMethodInfo(() => GetFame()));
            Lua.RegisterFunction("GetCurrentDay", this, SymbolExtensions.GetMethodInfo(() => GetCurrentDay()));
            Lua.RegisterFunction("IsStoreOpen", this, SymbolExtensions.GetMethodInfo(() => IsStoreOpen()));
            Lua.RegisterFunction("GetCurrentPhase", this, SymbolExtensions.GetMethodInfo(() => GetCurrentPhase()));
            Lua.RegisterFunction("GetCurrentHour", this, SymbolExtensions.GetMethodInfo(() => GetCurrentHour()));

            // Utility functions
            Lua.RegisterFunction("PauseGame", this, SymbolExtensions.GetMethodInfo(() => PauseGame()));
            Lua.RegisterFunction("ResumeGame", this, SymbolExtensions.GetMethodInfo(() => ResumeGame()));

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
            Lua.UnregisterFunction("GiveReward");
            Lua.UnregisterFunction("RaiseTutorialMarker");
            Lua.UnregisterFunction("IsMarkerTriggered");
            Lua.UnregisterFunction("GetMoney");
            Lua.UnregisterFunction("GetFame");
            Lua.UnregisterFunction("GetCurrentDay");
            Lua.UnregisterFunction("IsStoreOpen");
            Lua.UnregisterFunction("GetCurrentPhase");
            Lua.UnregisterFunction("GetCurrentHour");
            Lua.UnregisterFunction("PauseGame");
            Lua.UnregisterFunction("ResumeGame");

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
                if (!profile.unlockedCustomerIds.Contains(customerId))
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
    }
}
