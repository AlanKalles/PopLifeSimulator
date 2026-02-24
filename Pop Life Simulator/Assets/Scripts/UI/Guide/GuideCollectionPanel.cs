using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;
using PopLife.Manager;

namespace PopLife.UI.Guide
{
    /// <summary>
    /// 指南集合面板 - 列表展示所有已解锁的操作指南
    /// </summary>
    public class GuideCollectionPanel : MonoBehaviour
    {
        [SerializeField] private Transform contentRoot;
        [SerializeField] private GuideCollectionEntry entryPrefab;
        [SerializeField] private GameObject emptyStateText;

        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;

        private bool isShowing;
        private List<GuideCollectionEntry> spawnedEntries = new List<GuideCollectionEntry>();

        private void OnEnable()
        {
            if (OperationGuideManager.Instance != null)
            {
                OperationGuideManager.Instance.OnGuideUnlocked += OnGuideStateChanged;
                OperationGuideManager.Instance.OnGuideViewed += OnGuideStateChanged;
            }
        }

        private void OnDisable()
        {
            if (OperationGuideManager.Instance != null)
            {
                OperationGuideManager.Instance.OnGuideUnlocked -= OnGuideStateChanged;
                OperationGuideManager.Instance.OnGuideViewed -= OnGuideStateChanged;
            }
        }

        private void Update()
        {
            if (!isShowing) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        /// <summary>
        /// 显示集合面板
        /// </summary>
        public void Show()
        {
            isShowing = true;
            if (panelRoot != null)
                panelRoot.SetActive(true);

            RefreshList();
        }

        /// <summary>
        /// 隐藏集合面板
        /// </summary>
        public void Hide()
        {
            if (!isShowing) return;

            isShowing = false;
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        /// <summary>
        /// 切换显示/隐藏
        /// </summary>
        public void Toggle()
        {
            if (isShowing)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// 当前是否正在显示
        /// </summary>
        public bool IsShowing() => isShowing;

        private void OnGuideStateChanged(string guideId)
        {
            if (isShowing)
                RefreshList();
        }

        private void RefreshList()
        {
            // 清除旧条目
            foreach (var entry in spawnedEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            spawnedEntries.Clear();

            if (OperationGuideManager.Instance == null) return;

            var guides = OperationGuideManager.Instance.GetUnlockedGuides();

            // 空状态提示
            if (emptyStateText != null)
                emptyStateText.SetActive(guides.Count == 0);

            // 生成条目
            if (entryPrefab == null || contentRoot == null) return;

            foreach (var guideData in guides)
            {
                var entry = Instantiate(entryPrefab, contentRoot);
                bool isNew = !OperationGuideManager.Instance.IsViewed(guideData.guideId);
                entry.Setup(guideData, isNew, OnEntryClicked);
                spawnedEntries.Add(entry);
            }
        }

        private void OnEntryClicked(OperationGuideData data)
        {
            if (OperationGuideManager.Instance != null)
            {
                OperationGuideManager.Instance.ShowGuide(data);
            }
        }
    }
}
