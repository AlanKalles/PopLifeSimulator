using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Manager
{
    /// <summary>
    /// 操作指南管理器 - 管理指南解锁/查看状态，ES3持久化，公共API
    /// </summary>
    public class OperationGuideManager : MonoBehaviour
    {
        public static OperationGuideManager Instance;

        // ES3 持久化键名和文件名
        private const string ES3_FILE = "OperationGuides.es3";
        private const string KEY_UNLOCKED = "guideUnlockedIds";
        private const string KEY_VIEWED = "guideViewedIds";

        // 状态
        private HashSet<string> unlockedGuideIds = new HashSet<string>();
        private HashSet<string> viewedGuideIds = new HashSet<string>();

        // 缓存所有SO
        private OperationGuideData[] allGuides;

        // marker → guide 查找表
        private Dictionary<TutorialMarker, OperationGuideData> markerToGuide;

        // 事件
        public event Action<string> OnGuideUnlocked;
        public event Action<string> OnGuideViewed;

        private void Awake()
        {
            Instance = this;
            LoadAllGuideAssets();
            BuildMarkerLookup();
            LoadState();
        }

        private void OnEnable()
        {
            TutorialEventBus.OnMarkerTriggered += OnMarkerTriggered;
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;
        }

        #region 资源加载

        private void LoadAllGuideAssets()
        {
            allGuides = Resources.LoadAll<OperationGuideData>("ScriptableObjects/OperationGuides");
        }

        /// <summary>
        /// 构建 marker → guide 查找表，用于自动触发
        /// </summary>
        private void BuildMarkerLookup()
        {
            markerToGuide = new Dictionary<TutorialMarker, OperationGuideData>();
            if (allGuides == null) return;

            foreach (var guide in allGuides)
            {
                if (guide.activationMarker != TutorialMarker.None)
                {
                    if (!markerToGuide.TryAdd(guide.activationMarker, guide))
                    {
                        Debug.LogWarning($"[OperationGuideManager] Duplicate marker {guide.activationMarker} on guide: {guide.guideId}");
                    }
                }
            }
        }

        /// <summary>
        /// 当 marker 触发时，自动显示对应的操作指南
        /// </summary>
        private void OnMarkerTriggered(TutorialMarker marker)
        {
            if (markerToGuide != null && markerToGuide.TryGetValue(marker, out var guide))
            {
                ShowGuide(guide);
            }
        }

        /// <summary>
        /// 通过guideId查找SO资产
        /// </summary>
        private OperationGuideData FindGuideById(string guideId)
        {
            if (allGuides == null) LoadAllGuideAssets();
            return allGuides?.FirstOrDefault(g => g.guideId == guideId);
        }

        #endregion

        #region ES3 持久化

        private void LoadState()
        {
            if (ES3.KeyExists(KEY_UNLOCKED, ES3_FILE))
            {
                var list = ES3.Load<List<string>>(KEY_UNLOCKED, ES3_FILE);
                unlockedGuideIds = new HashSet<string>(list);
            }

            if (ES3.KeyExists(KEY_VIEWED, ES3_FILE))
            {
                var list = ES3.Load<List<string>>(KEY_VIEWED, ES3_FILE);
                viewedGuideIds = new HashSet<string>(list);
            }
        }

        private void SaveState()
        {
            ES3.Save(KEY_UNLOCKED, new List<string>(unlockedGuideIds), ES3_FILE);
            ES3.Save(KEY_VIEWED, new List<string>(viewedGuideIds), ES3_FILE);
        }

        #endregion

        #region 核心 API

        /// <summary>
        /// 按guideId显示指南（自动解锁 + 打开面板）
        /// </summary>
        public void ShowGuide(string guideId)
        {
            var data = FindGuideById(guideId);
            if (data == null)
            {
                Debug.LogWarning($"[OperationGuideManager] Guide not found: {guideId}");
                return;
            }
            ShowGuide(data);
        }

        /// <summary>
        /// 按SO引用显示指南（自动解锁 + 打开面板）
        /// </summary>
        public void ShowGuide(OperationGuideData data)
        {
            if (data == null) return;

            // 自动解锁
            UnlockGuide(data.guideId);

            // 构建关闭回调：如果配置了 closingMarker，关闭时自动触发
            Action onClose = null;
            if (data.closingMarker != TutorialMarker.None)
            {
                var marker = data.closingMarker;
                onClose = () => TutorialEventBus.RaiseMarker(marker);
            }

            // 通过UIManager显示面板
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowOperationGuide(data, onClose);
            }
            else
            {
                Debug.LogWarning("[OperationGuideManager] UIManager.Instance is null");
            }
        }

        /// <summary>
        /// 仅解锁，不显示面板
        /// </summary>
        public void UnlockGuide(string guideId)
        {
            if (string.IsNullOrEmpty(guideId)) return;
            if (unlockedGuideIds.Add(guideId))
            {
                SaveState();
                OnGuideUnlocked?.Invoke(guideId);
            }
        }

        /// <summary>
        /// 标记为已查看
        /// </summary>
        public void MarkAsViewed(string guideId)
        {
            if (string.IsNullOrEmpty(guideId)) return;
            if (viewedGuideIds.Add(guideId))
            {
                SaveState();
                OnGuideViewed?.Invoke(guideId);
            }
        }

        /// <summary>
        /// 检查是否已解锁
        /// </summary>
        public bool IsUnlocked(string guideId)
        {
            return unlockedGuideIds.Contains(guideId);
        }

        /// <summary>
        /// 检查是否已查看
        /// </summary>
        public bool IsViewed(string guideId)
        {
            return viewedGuideIds.Contains(guideId);
        }

        /// <summary>
        /// 获取所有已解锁的指南（按sortOrder排序）
        /// </summary>
        public List<OperationGuideData> GetUnlockedGuides()
        {
            if (allGuides == null) LoadAllGuideAssets();

            return allGuides
                .Where(g => unlockedGuideIds.Contains(g.guideId))
                .OrderBy(g => g.sortOrder)
                .ToList();
        }

        /// <summary>
        /// 获取未读指南数量（红点提示用）
        /// </summary>
        public int GetUnviewedCount()
        {
            int count = 0;
            foreach (var id in unlockedGuideIds)
            {
                if (!viewedGuideIds.Contains(id))
                    count++;
            }
            return count;
        }

        #endregion
    }
}
