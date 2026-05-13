using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using PopLife.Quest;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 任务完成二级窗口 - 列出该任务的奖励
    /// 触发：QuestNotificationToast 的 QUEST COMPLETE 一级窗口关闭后调用 Show()
    /// 关闭：点击窗口面板（ScrollView）外的背景遮罩
    /// 内部 ScrollView 的 Raycast 会吸收点击，不会冒泡触发关闭
    /// </summary>
    public class QuestSuccessRewardPanel : MonoBehaviour, IPointerClickHandler
    {
        public static QuestSuccessRewardPanel Instance { get; private set; }

        [Header("UI 根节点")]
        [Tooltip("整个二级窗口的根节点（含背景遮罩与中央 panel）")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("背景遮罩")]
        [Tooltip("全屏背景遮罩 GameObject。仅当 click 落在它身上时才关闭窗口（点 ScrollView/Panel 内不关闭）")]
        [SerializeField] private GameObject backgroundOverlay;

        [Header("标题")]
        [Tooltip("可选：显示任务标题")]
        [SerializeField] private TextMeshProUGUI questNameText;

        [Header("奖励列表")]
        [Tooltip("ScrollView 中的 Content 容器（reward item prefab 实例化进这里）")]
        [SerializeField] private RectTransform rewardContainer;
        [Tooltip("success reward item prefab，预期挂 QuestRewardItem 脚本")]
        [SerializeField] private GameObject rewardItemPrefab;

        [Header("动画")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        /// <summary>
        /// 当前是否处于显示状态（toast 队列用于等待二级窗口关闭后再继续）
        /// </summary>
        public bool IsVisible { get; private set; }

        private readonly List<GameObject> spawnedItems = new();
        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (panelRoot != null) panelRoot.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            IsVisible = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #region 公共 API

        /// <summary>
        /// 显示指定任务的奖励列表。无 reward 的任务直接跳过（不弹窗）
        /// </summary>
        public void Show(string questName)
        {
            if (string.IsNullOrEmpty(questName)) return;

            var def = QuestDataService.Instance?.GetDefinition(questName);
            if (def == null) return;
            if (def.Rewards == null || def.Rewards.Length == 0) return;

            // 填充标题
            if (questNameText != null)
            {
                var store = QuestLogicManager.Instance?.StateStore;
                questNameText.text = store?.GetTitle(questName) ?? questName;
            }

            // 清空旧 items，实例化新 items
            ClearItems();
            if (rewardItemPrefab != null && rewardContainer != null)
            {
                foreach (var reward in def.Rewards)
                {
                    var go = Instantiate(rewardItemPrefab, rewardContainer);
                    spawnedItems.Add(go);
                    if (go.TryGetComponent<QuestRewardItem>(out var item))
                        item.SetData(reward);
                }
            }

            // 显示并淡入
            if (panelRoot != null) panelRoot.SetActive(true);
            IsVisible = true;

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeIn());
        }

        public void Hide()
        {
            if (!IsVisible) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOut());
        }

        #endregion

        #region 点击处理

        public void OnPointerClick(PointerEventData eventData)
        {
            // 仅当 click 命中"背景遮罩"GameObject 才关闭。
            // ScrollView/Panel 上的 Image (Raycast Target=true) 会先吸收点击，hit 目标不是背景遮罩，事件被忽略。
            if (backgroundOverlay != null
                && eventData.pointerCurrentRaycast.gameObject == backgroundOverlay)
            {
                Hide();
            }
        }

        #endregion

        #region 内部

        private void ClearItems()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null) Destroy(spawnedItems[i]);
            }
            spawnedItems.Clear();
        }

        private IEnumerator FadeIn()
        {
            if (canvasGroup == null) yield break;
            float elapsed = 0f;
            canvasGroup.alpha = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            fadeRoutine = null;
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);
            ClearItems();
            IsVisible = false;
            fadeRoutine = null;
        }

        #endregion
    }
}
