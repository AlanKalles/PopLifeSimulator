using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife
{
    /// <summary>
    /// 图鉴状态管理器 - 追踪玩家是否查看过每个货架条目
    /// 持久化到ES3，在Awake中初始化
    /// 即使Codex UI关闭也持续监听蓝图解锁事件
    /// </summary>
    public class CodexStateManager : MonoBehaviour
    {
        public static CodexStateManager Instance;

        // ES3 持久化常量
        private const string ES3_FILE = "ItemCodex.es3";
        private const string KEY_SEEN = "seenShelfIds";

        // 已查看的货架ID集合
        private HashSet<string> seenShelfIds = new HashSet<string>();

        /// <summary>
        /// 玩家查看了某个图鉴条目时触发
        /// </summary>
        public event Action<string> OnShelfSeen;

        /// <summary>
        /// 新蓝图解锁且尚未被查看时触发
        /// </summary>
        public event Action<string> OnNewShelfAvailable;

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

        private void OnEnable()
        {
            // 订阅蓝图解锁事件（即使UI关闭也要监听）
            BlueprintManager.OnShelfUnlocked += OnBlueprintShelfUnlocked;
        }

        private void OnDisable()
        {
            BlueprintManager.OnShelfUnlocked -= OnBlueprintShelfUnlocked;
        }

        /// <summary>
        /// 蓝图解锁回调：新解锁的货架如果尚未查看，广播事件
        /// </summary>
        private void OnBlueprintShelfUnlocked(ShelfArchetype shelf)
        {
            if (shelf == null) return;

            if (!seenShelfIds.Contains(shelf.archetypeId))
            {
                OnNewShelfAvailable?.Invoke(shelf.archetypeId);
            }
        }

        #region ES3 持久化

        private void LoadState()
        {
            try
            {
                if (ES3.KeyExists(KEY_SEEN, ES3_FILE))
                {
                    var list = ES3.Load<List<string>>(KEY_SEEN, ES3_FILE);
                    seenShelfIds = new HashSet<string>(list);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodexStateManager] 加载状态失败: {e.Message}");
                seenShelfIds = new HashSet<string>();
            }
        }

        private void SaveState()
        {
            try
            {
                ES3.Save(KEY_SEEN, new List<string>(seenShelfIds), ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodexStateManager] 保存状态失败: {e.Message}");
            }
        }

        #endregion

        #region 公共 API

        /// <summary>
        /// 检查货架是否已被玩家查看过
        /// </summary>
        public bool IsShelfSeen(string shelfId)
        {
            return seenShelfIds.Contains(shelfId);
        }

        /// <summary>
        /// 标记某个货架为已查看
        /// </summary>
        public void MarkShelfAsSeen(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId)) return;

            if (seenShelfIds.Add(shelfId))
            {
                SaveState();
                OnShelfSeen?.Invoke(shelfId);
            }
        }

        /// <summary>
        /// 将所有已解锁的货架标记为已查看
        /// </summary>
        public void MarkAllAsSeen()
        {
            if (BlueprintManager.Instance == null) return;

            var unlockedIds = BlueprintManager.Instance.GetUnlockedShelfIds();
            bool changed = false;

            foreach (var id in unlockedIds)
            {
                if (seenShelfIds.Add(id))
                    changed = true;
            }

            if (changed)
            {
                SaveState();
            }
        }

        /// <summary>
        /// 获取未查看的已解锁货架数量
        /// </summary>
        public int GetUnseenCount()
        {
            if (BlueprintManager.Instance == null) return 0;

            var unlockedIds = BlueprintManager.Instance.GetUnlockedShelfIds();
            int count = 0;

            foreach (var id in unlockedIds)
            {
                if (!seenShelfIds.Contains(id))
                    count++;
            }

            return count;
        }

        #endregion
    }
}
