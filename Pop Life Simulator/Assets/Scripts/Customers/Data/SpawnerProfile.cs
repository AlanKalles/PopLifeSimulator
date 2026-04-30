using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PopLife.Utility;

namespace PopLife.Customers.Data
{
    /// <summary>
    /// 解锁顾客配置
    /// - 模板（只读）：Assets/StreamingAssets/SpawnerProfile.json，由 SpawnerProfileEditor 维护，进 git
    /// - 运行时存档：persistentDataPath/SpawnerProfile.json，首次读取自动从模板复制
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

        /// <summary>
        /// 删除运行时存档（GameStateManager 重置流程使用）。
        /// 始终删除 persistentDataPath/SpawnerProfile.json；下次 Load 会自动从 StreamingAssets 模板复制。
        /// 不会触碰 StreamingAssets 的设计模板。
        /// 注意：调用方需同时清空 CustomerSpawner 等模块的缓存，否则下次 Save 会把旧数据写回。
        /// </summary>
        public static void ClearSaveFile()
        {
            try
            {
                string path = SavePathManager.GetWritePath("SpawnerProfile.json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[SpawnerProfile] 已删除 SpawnerProfile.json: {path}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpawnerProfile] 删除 SpawnerProfile.json 失败: {e.Message}");
            }
        }
    }
}
