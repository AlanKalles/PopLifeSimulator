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
        private RectTransform cachedRectTransform;
        private RectTransform parentRectTransform;
        private Camera cachedCamera;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 缓存 Canvas 引用，用于 ScreenPointToLocalPointInRectangle
            cachedRectTransform = GetComponent<RectTransform>();
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            parentRectTransform = transform.parent as RectTransform;
            if (rootCanvas != null)
            {
                cachedCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : rootCanvas.worldCamera;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 跟随鼠标位置
            if (isShowing && cachedRectTransform != null && parentRectTransform != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRectTransform, (Vector2)Input.mousePosition, cachedCamera, out Vector2 localPoint))
                {
                    cachedRectTransform.localPosition = localPoint + (Vector2)positionOffset;
                }
                ClampToScreen();
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

            // 填充 DDL：无截止显示 "Unlimited time"，有截止显示绝对日期 "Spring Day 11"
            if (deadlineText != null)
            {
                deadlineText.gameObject.SetActive(true);
                if (data.deadlineDay < 0)
                {
                    deadlineText.text = "Unlimited time";
                }
                else
                {
                    int startingYear = DayLoopManager.Instance != null
                        ? DayLoopManager.Instance.StartingYear
                        : 2126;
                    deadlineText.text = CalendarUtils.FormatShortDate(data.deadlineDay, startingYear);
                }
            }

            // 填充条目列表
            PopulateEntries(data.entries);

            // 定位
            if (cachedRectTransform != null && parentRectTransform != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRectTransform, (Vector2)screenPosition, cachedCamera, out Vector2 localPoint))
                {
                    cachedRectTransform.localPosition = localPoint + (Vector2)positionOffset;
                }
                ClampToScreen();
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
            if (!gameObject.activeInHierarchy)
            {
                // GO已经inactive，无法启动协程，也无需淡出
                fadeCoroutine = null;
                return;
            }
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

        private void ClampToScreen()
        {
            if (parentRectTransform == null) return;

            Vector3[] corners = new Vector3[4];
            cachedRectTransform.GetWorldCorners(corners);

            // Overlay 模式 world == screen；Camera 模式需转换
            Vector2 bl = cachedCamera != null
                ? (Vector2)cachedCamera.WorldToScreenPoint(corners[0])
                : (Vector2)corners[0];
            Vector2 tr = cachedCamera != null
                ? (Vector2)cachedCamera.WorldToScreenPoint(corners[2])
                : (Vector2)corners[2];

            float sw = Screen.width;
            float sh = Screen.height;
            Vector2 delta = Vector2.zero;

            if (tr.x > sw) delta.x = sw - tr.x;
            if (bl.x < 0) delta.x = -bl.x;
            if (tr.y > sh) delta.y = sh - tr.y;
            if (bl.y < 0) delta.y = -bl.y;

            if (delta.sqrMagnitude < 0.01f) return;

            Vector2 currentScreen = cachedCamera != null
                ? (Vector2)cachedCamera.WorldToScreenPoint(cachedRectTransform.position)
                : (Vector2)cachedRectTransform.position;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform, currentScreen + delta, cachedCamera, out Vector2 newLocal))
            {
                cachedRectTransform.localPosition = newLocal;
            }
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
