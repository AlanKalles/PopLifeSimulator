using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// 日历事件浮动提示面板
    /// CanvasGroup 淡入淡出 + ClampToScreen + 鼠标跟随
    /// </summary>
    public class CalendarTooltip : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Position")]
        [SerializeField] private Vector2 offset = new(15f, -15f);

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.05f;
        [SerializeField] private float fadeOutDuration = 0.05f;

        private RectTransform cachedRectTransform;
        private RectTransform parentRectTransform;
        private Camera cachedCamera;
        private Coroutine fadeCoroutine;
        private bool isShowing;

        private void Awake()
        {
            cachedRectTransform = GetComponent<RectTransform>();

            // 缓存 Canvas 引用
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            parentRectTransform = transform.parent as RectTransform;
            if (rootCanvas != null)
            {
                cachedCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : rootCanvas.worldCamera;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // tooltip 不拦截鼠标，防止与 label 抢射线导致闪烁
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 显示 Tooltip 并设置内容
        /// </summary>
        public void Show(CalendarEventEntry entry, Vector2 screenPos)
        {
            // 1. 先激活（Layout 计算需要 activeInHierarchy）
            isShowing = true;
            gameObject.SetActive(true);

            // 2. 设置文本内容
            if (titleText != null)
                titleText.text = entry.displayName;

            if (bodyText != null)
            {
                bodyText.text = entry.type switch
                {
                    CalendarEventType.DueDate =>
                        $"Quest: {entry.displayName}\nAssigned By: {entry.giverName}",
                    _ => entry.description
                };
            }

            // 3. 强制刷新 TMP mesh + Layout（仅刷新此 tooltip，不影响其他 Canvas）
            if (titleText != null) titleText.ForceMeshUpdate();
            if (bodyText != null) bodyText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cachedRectTransform);

            // 4. 定位（此时 RectTransform 尺寸已正确）
            if (cachedRectTransform != null && parentRectTransform != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRectTransform, screenPos + offset, cachedCamera, out Vector2 localPos))
                {
                    cachedRectTransform.localPosition = localPos;
                }
                ClampToScreen();
            }

            // 5. 淡入
            if (!gameObject.activeInHierarchy)
            {
                canvasGroup.alpha = 1f;
                return;
            }

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
        /// 立即隐藏（无动画），用于面板关闭时
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

            // 移到屏幕外，防止重开面板时闪现旧位置
            if (cachedRectTransform != null)
                cachedRectTransform.position = new Vector3(-10000f, -10000f, 0f);

            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 跟随鼠标
            if (isShowing && cachedRectTransform != null && parentRectTransform != null)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRectTransform, (Vector2)Input.mousePosition + offset, cachedCamera,
                    out Vector2 localPos))
                {
                    cachedRectTransform.localPosition = localPos;
                }
                ClampToScreen();
            }
        }

        #region ClampToScreen

        private void ClampToScreen()
        {
            if (parentRectTransform == null || cachedRectTransform == null) return;

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

        #endregion

        #region Fade Animation

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
