using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;

namespace PopLife
{
    /// <summary>
    /// 顾客图鉴状态管理器 - 追踪玩家是否查看过每个顾客条目
    /// 持久化到ES3，在Awake中初始化
    /// </summary>
    public class CustomerCodexStateManager : MonoBehaviour
    {
        public static CustomerCodexStateManager Instance;

        // ES3 持久化常量
        private const string ES3_FILE = "CustomerCodex.es3";
        private const string KEY_SEEN = "seenCustomerIds";

        // 已查看的顾客ID集合
        private HashSet<string> seenCustomerIds = new HashSet<string>();

        /// <summary>
        /// 玩家查看了某个顾客条目时触发
        /// </summary>
        public event Action<string> OnCustomerSeen;

        /// <summary>
        /// 新顾客解锁且尚未被查看时触发
        /// </summary>
        public event Action<string> OnNewCustomerAvailable;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            LoadState();
        }

        // 兜底：如果场景里忘了挂载这个管理器，AfterSceneLoad 自动建一个，避免顾客图鉴 Mark All As Seen 静默失败
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceExists()
        {
            if (Instance != null) return;

            var go = new GameObject(nameof(CustomerCodexStateManager));
            go.AddComponent<CustomerCodexStateManager>();
            Debug.LogWarning($"[{nameof(CustomerCodexStateManager)}] 场景未挂载该组件，已在运行时自动创建。建议手动添加到场景以便持久化配置。");
        }

        #region ES3 持久化

        private void LoadState()
        {
            try
            {
                if (ES3.KeyExists(KEY_SEEN, ES3_FILE))
                {
                    var list = ES3.Load<List<string>>(KEY_SEEN, ES3_FILE);
                    seenCustomerIds = new HashSet<string>(list);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CustomerCodexStateManager] 加载状态失败: {e.Message}");
                seenCustomerIds = new HashSet<string>();
            }
        }

        private void SaveState()
        {
            try
            {
                ES3.Save(KEY_SEEN, new List<string>(seenCustomerIds), ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CustomerCodexStateManager] 保存状态失败: {e.Message}");
            }
        }

        #endregion

        #region 公共 API

        /// <summary>
        /// 检查顾客是否已被玩家查看过
        /// </summary>
        public bool IsCustomerSeen(string customerId)
        {
            return seenCustomerIds.Contains(customerId);
        }

        /// <summary>
        /// 标记某个顾客为已查看
        /// </summary>
        public void MarkCustomerAsSeen(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return;

            if (seenCustomerIds.Add(customerId))
            {
                SaveState();
                OnCustomerSeen?.Invoke(customerId);
            }
        }

        /// <summary>
        /// 将所有已解锁的顾客标记为已查看
        /// </summary>
        public void MarkAllAsSeen()
        {
            var profile = SpawnerProfile.Load();
            if (profile == null) return;

            // 收集本次新增的 ID，事件需要在 SaveState 后逐个广播
            List<string> newlySeenIds = null;

            foreach (var id in profile.unlockedCustomerIds)
            {
                if (seenCustomerIds.Add(id))
                {
                    newlySeenIds ??= new List<string>();
                    newlySeenIds.Add(id);
                }
            }

            if (newlySeenIds == null) return;

            SaveState();

            // 广播事件，与 MarkCustomerAsSeen 行为一致，方便外部订阅者刷新
            foreach (var id in newlySeenIds)
                OnCustomerSeen?.Invoke(id);
        }

        /// <summary>
        /// 获取未查看的已解锁顾客数量
        /// </summary>
        public int GetUnseenCount()
        {
            var profile = SpawnerProfile.Load();
            if (profile == null) return 0;

            int count = 0;

            foreach (var id in profile.unlockedCustomerIds)
            {
                if (!seenCustomerIds.Contains(id))
                    count++;
            }

            return count;
        }

        #endregion
    }
}
