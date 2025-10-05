using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PopLife.Utility;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 运行时可编辑的解锁customer配置
    /// 存储位置:
    /// - 编辑器: Assets/StreamingAssets/SpawnerProfile.json
    /// - 运行时: persistentDataPath/SpawnerProfile.json
    /// </summary>
    [Serializable]
    public class SpawnerProfile
    {
        [Tooltip("已解锁的顾客ID列表")]
        public List<string> unlockedCustomerIds = new List<string>();

        /// <summary>
        /// 加载SpawnerProfile (使用SavePathManager自动处理路径)
        /// </summary>
        public static SpawnerProfile Load()
        {
            string path = SavePathManager.GetReadPath("SpawnerProfile.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SpawnerProfile] File not found at {path}, creating default profile");
                return new SpawnerProfile();
            }

            try
            {
                string json = File.ReadAllText(path);
                var profile = JsonUtility.FromJson<SpawnerProfile>(json);

                if (profile == null)
                {
                    Debug.LogWarning("[SpawnerProfile] Failed to parse JSON, creating default profile");
                    return new SpawnerProfile();
                }

                if (profile.unlockedCustomerIds == null)
                {
                    profile.unlockedCustomerIds = new List<string>();
                }

                Debug.Log($"[SpawnerProfile] Loaded {profile.unlockedCustomerIds.Count} unlocked customers from {path}");
                return profile;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpawnerProfile] Failed to load: {e.Message}");
                return new SpawnerProfile();
            }
        }

        /// <summary>
        /// 保存SpawnerProfile
        /// </summary>
        public void Save()
        {
            string path = SavePathManager.GetWritePath("SpawnerProfile.json");

            try
            {
                SavePathManager.EnsureDirectoryExists(path);
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(path, json);
                Debug.Log($"[SpawnerProfile] Saved {unlockedCustomerIds.Count} unlocked customers to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpawnerProfile] Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// 解锁顾客
        /// </summary>
        public void UnlockCustomer(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                Debug.LogWarning("[SpawnerProfile] Cannot unlock customer with empty ID");
                return;
            }

            if (!unlockedCustomerIds.Contains(customerId))
            {
                unlockedCustomerIds.Add(customerId);
                Debug.Log($"[SpawnerProfile] Unlocked customer: {customerId}");
            }
        }

        /// <summary>
        /// 锁定顾客
        /// </summary>
        public void LockCustomer(string customerId)
        {
            if (unlockedCustomerIds.Remove(customerId))
            {
                Debug.Log($"[SpawnerProfile] Locked customer: {customerId}");
            }
        }

        /// <summary>
        /// 检查顾客是否已解锁
        /// </summary>
        public bool IsUnlocked(string customerId)
        {
            return unlockedCustomerIds.Contains(customerId);
        }

        /// <summary>
        /// 批量解锁顾客
        /// </summary>
        public void UnlockCustomers(IEnumerable<string> customerIds)
        {
            foreach (var id in customerIds)
            {
                UnlockCustomer(id);
            }
        }

        /// <summary>
        /// 清空所有解锁
        /// </summary>
        public void ClearAll()
        {
            unlockedCustomerIds.Clear();
            Debug.Log("[SpawnerProfile] Cleared all unlocked customers");
        }
    }
}
