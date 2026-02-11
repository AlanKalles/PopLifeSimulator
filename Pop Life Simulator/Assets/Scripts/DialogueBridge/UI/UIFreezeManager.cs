using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 临时冻结 UI 面板的可交互性，对话节点切换或对话结束时自动恢复。
    /// 使用 CanvasGroup 实现：interactable=false + blocksRaycasts=false
    /// </summary>
    public class UIFreezeManager : MonoBehaviour
    {
        #region Singleton

        public static UIFreezeManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        #endregion

        #region Settings

        [Title("Auto Restore")]
        [Tooltip("节点切换时自动解冻所有面板")]
        [SerializeField] private bool autoUnfreezeOnNodeChange = true;

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        #endregion

        #region State

        /// <summary>
        /// 冻结记录：保存恢复所需的原始状态
        /// </summary>
        private struct FreezeRecord
        {
            public GameObject target;
            public CanvasGroup canvasGroup;
            public bool weAddedCanvasGroup;
            public bool originalInteractable;
            public bool originalBlocksRaycasts;
        }

        private readonly Dictionary<string, FreezeRecord> frozenPanels = new();

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationEnded += OnConversationEnded;
                DialogueManager.instance.conversationLinePrepared += OnConversationLinePrepared;
            }
        }

        private void OnDisable()
        {
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
                DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
            }
        }

        private void OnDestroy()
        {
            // 确保销毁时恢复所有冻结状态
            UnfreezeAll();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 通过名称冻结 UI 面板
        /// </summary>
        public void Freeze(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[UIFreezeManager] Freeze called with empty name");
                return;
            }

            // 已经冻结过则跳过
            if (frozenPanels.ContainsKey(targetName))
            {
                if (debugMode) Debug.Log($"[UIFreezeManager] '{targetName}' already frozen, skipping");
                return;
            }

            var go = GameObject.Find(targetName);
            if (go == null)
            {
                Debug.LogWarning($"[UIFreezeManager] Target not found: {targetName}");
                return;
            }

            Freeze(targetName, go);
        }

        /// <summary>
        /// 通过直接引用冻结 UI 面板
        /// </summary>
        public void Freeze(string key, GameObject target)
        {
            if (target == null) return;

            if (frozenPanels.ContainsKey(key))
            {
                if (debugMode) Debug.Log($"[UIFreezeManager] '{key}' already frozen, skipping");
                return;
            }

            // 获取或添加 CanvasGroup
            var cg = target.GetComponent<CanvasGroup>();
            bool weAdded = false;
            if (cg == null)
            {
                cg = target.AddComponent<CanvasGroup>();
                weAdded = true;
            }

            // 记录原始状态
            var record = new FreezeRecord
            {
                target = target,
                canvasGroup = cg,
                weAddedCanvasGroup = weAdded,
                originalInteractable = cg.interactable,
                originalBlocksRaycasts = cg.blocksRaycasts,
            };

            // 冻结
            cg.interactable = false;
            cg.blocksRaycasts = false;

            frozenPanels[key] = record;

            if (debugMode) Debug.Log($"[UIFreezeManager] Frozen: {key}");
        }

        /// <summary>
        /// 解冻指定面板
        /// </summary>
        public void Unfreeze(string key)
        {
            if (!frozenPanels.TryGetValue(key, out var record)) return;

            RestoreRecord(record);
            frozenPanels.Remove(key);

            if (debugMode) Debug.Log($"[UIFreezeManager] Unfrozen: {key}");
        }

        /// <summary>
        /// 解冻所有面板
        /// </summary>
        [Button("Unfreeze All")]
        public void UnfreezeAll()
        {
            foreach (var kvp in frozenPanels)
            {
                RestoreRecord(kvp.Value);
            }

            int count = frozenPanels.Count;
            frozenPanels.Clear();

            if (debugMode && count > 0) Debug.Log($"[UIFreezeManager] Unfrozen all ({count} panels)");
        }

        /// <summary>
        /// 当前是否有冻结的面板
        /// </summary>
        public bool HasFrozenPanels => frozenPanels.Count > 0;

        #endregion

        #region Internal

        private void RestoreRecord(FreezeRecord record)
        {
            if (record.canvasGroup == null) return;

            record.canvasGroup.interactable = record.originalInteractable;
            record.canvasGroup.blocksRaycasts = record.originalBlocksRaycasts;

            // 如果 CanvasGroup 是我们添加的，恢复后移除
            if (record.weAddedCanvasGroup)
            {
                Destroy(record.canvasGroup);
            }
        }

        #endregion

        #region Dialogue System Events

        private void OnConversationLinePrepared(Subtitle subtitle)
        {
            if (autoUnfreezeOnNodeChange && frozenPanels.Count > 0)
            {
                UnfreezeAll();
            }
        }

        private void OnConversationEnded(Transform actor)
        {
            if (frozenPanels.Count > 0)
            {
                UnfreezeAll();
            }
        }

        #endregion
    }
}
