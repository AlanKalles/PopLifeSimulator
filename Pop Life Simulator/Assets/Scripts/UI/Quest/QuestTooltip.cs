using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 任务 Tooltip - 鼠标悬停时跟随鼠标显示
    /// 复刻 ShelfTooltip 的 CanvasGroup 淡入淡出 + ClampToScreen 模式
    /// </summary>
    public class QuestTooltip : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Transform entriesContainer;
        [SerializeField] private GameObject entryItemPrefab;
        [SerializeField] private TextMeshProUGUI deadlineText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("扩展 UI - 任务图标和类型")]
        [SerializeField] private Image questIcon;
        [SerializeField] private TextMeshProUGUI questTypeLabel;
        [SerializeField] private Image questTypeIcon;

        [Header("Type Icon Sprites")]
        [SerializeField] private Sprite mainQuestSprite;
        [SerializeField] private Sprite sideQuestSprite;

        [Header("Type Label Colors")]
        [SerializeField] private Color mainQuestLabelColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color sideQuestLabelColor = Color.white;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.05f;
        [SerializeField] private float fadeOutDuration = 0.05f;

        [Header("Position Offset")]
        [SerializeField] private Vector3 positionOffset = new Vector3(15f, -10f, 0f);

        private Coroutine fadeCoroutine;
        private bool isShowing = false;
        private readonly List<GameObject> spawnedEntries = new();

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 跟随鼠标位置
            if (isShowing)
            {
                RectTransform rt = GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.position = Input.mousePosition + positionOffset;
                    ClampToScreen(rt);
                }
            }
        }

        /// <summary>
        /// 显示 Tooltip
        /// </summary>
        public void Show(string questName, Vector3 screenPosition)
        {
            var detail = QuestDataService.Instance?.GetQuestDetail(questName);
            if (detail == null) return;

            var data = detail.Value;

            // 填充任务图标
            if (questIcon != null)
            {
                if (data.questIcon != null)
                {
                    questIcon.sprite = data.questIcon;
                    questIcon.gameObject.SetActive(true);
                }
                else
                {
                    questIcon.gameObject.SetActive(false);
                }
            }

            // 填充任务类型标签
            if (questTypeLabel != null)
            {
                questTypeLabel.text = data.questType == QuestType.Main ? "MAIN QUEST" : "SIDE QUEST";
                questTypeLabel.color = data.questType == QuestType.Main
                    ? mainQuestLabelColor
                    : sideQuestLabelColor;
            }

            // 填充任务类型图标（仅切换 Sprite，不变色）
            if (questTypeIcon != null)
            {
                Sprite targetSprite = data.questType == QuestType.Main ? mainQuestSprite : sideQuestSprite;
                if (targetSprite != null)
                {
                    questTypeIcon.sprite = targetSprite;
                    questTypeIcon.enabled = true;
                }
                else
                {
                    questTypeIcon.enabled = false;
                }
            }

            // 填充标题
            if (titleText != null)
                titleText.text = data.displayTitle;

            // 填充描述
            if (descriptionText != null)
                descriptionText.text = data.description;

            // 填充 DDL
            if (deadlineText != null)
            {
                if (data.remainingDays < 0)
                {
                    deadlineText.gameObject.SetActive(false);
                }
                else
                {
                    deadlineText.gameObject.SetActive(true);
                    deadlineText.text = data.remainingDays <= 1
                        ? $"Deadline: {data.remainingDays} day left!"
                        : $"Deadline: {data.remainingDays} days left";
                }
            }

            // 填充条目列表
            PopulateEntries(data.entries);

            // 定位
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.position = screenPosition + positionOffset;
                ClampToScreen(rt);
            }

            // 淡入
            isShowing = true;
            gameObject.SetActive(true);
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 隐藏 Tooltip（带淡出动画）
        /// </summary>
        public void Hide()
        {
            isShowing = false;
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// 立即隐藏（无动画）
        /// </summary>
        public void HideImmediate()
        {
            isShowing = false;
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        #region 内部方法

        private void PopulateEntries(QuestEntryInfo[] entries)
        {
            // 清空旧条目
            foreach (var obj in spawnedEntries)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedEntries.Clear();

            if (entries == null || entries.Length == 0 || entriesContainer == null || entryItemPrefab == null)
                return;

            foreach (var entry in entries)
            {
                var obj = Instantiate(entryItemPrefab, entriesContainer);
                var item = obj.GetComponent<QuestTooltipEntryItem>();
                if (item != null)
                {
                    item.SetData(entry);
                }
                spawnedEntries.Add(obj);
            }
        }

        /// <summary>
        /// 防止 Tooltip 超出屏幕边界
        /// 复刻自 ShelfTooltip.ClampToScreen
        /// </summary>
        private void ClampToScreen(RectTransform rectTransform)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            Vector3 pos = rectTransform.position;

            // 右边界
            if (corners[2].x > screenWidth)
                pos.x -= (corners[2].x - screenWidth);

            // 左边界
            if (corners[0].x < 0)
                pos.x -= corners[0].x;

            // 上边界
            if (corners[1].y > screenHeight)
                pos.y -= (corners[1].y - screenHeight);

            // 下边界
            if (corners[0].y < 0)
                pos.y -= corners[0].y;

            rectTransform.position = pos;
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeInDuration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            fadeCoroutine = null;
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            fadeCoroutine = null;
        }

        #endregion
    }
}
