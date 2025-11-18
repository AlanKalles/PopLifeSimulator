using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

namespace PopLife.GuidanceSystem.UI
{
    /// <summary>
    /// 指示文本框，显示指引文字
    /// Instruction box displaying guidance text
    /// </summary>
    public class InstructionBox : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private RectTransform boxRect;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Appearance")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private float padding = 20f;
        [SerializeField] private Vector2 minSize = new Vector2(300, 100);
        [SerializeField] private Vector2 maxSize = new Vector2(600, 200);

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // State
        private bool isVisible;
        private Coroutine fadeCoroutine;
        private TextAnchor currentPosition = TextAnchor.UpperCenter;

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Get or create components
            if (boxRect == null)
                boxRect = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
                if (backgroundImage == null)
                {
                    backgroundImage = gameObject.AddComponent<Image>();
                }
            }

            // Setup appearance
            backgroundImage.color = backgroundColor;

            if (instructionText != null)
            {
                instructionText.color = textColor;
            }

            // Start hidden
            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            isVisible = false;
        }

        /// <summary>
        /// 设置指示文本
        /// </summary>
        public void SetText(string text)
        {
            if (instructionText != null)
            {
                instructionText.text = text;
                AdjustSize();
            }
        }

        /// <summary>
        /// 设置位置锚点
        /// </summary>
        public void SetPosition(TextAnchor anchor)
        {
            currentPosition = anchor;
            UpdatePosition();
        }

        /// <summary>
        /// 显示文本框
        /// </summary>
        public void Show()
        {
            if (isVisible) return;

            gameObject.SetActive(true);
            isVisible = true;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 隐藏文本框
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeOut());
        }

        /// <summary>
        /// 立即隐藏
        /// </summary>
        public void HideImmediate()
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            isVisible = false;
        }

        /// <summary>
        /// 调整大小以适应文本
        /// </summary>
        private void AdjustSize()
        {
            if (instructionText == null) return;

            // Calculate preferred size
            Vector2 preferredSize = instructionText.GetPreferredValues(
                instructionText.text,
                maxSize.x - padding * 2,
                Mathf.Infinity
            );

            // Add padding
            preferredSize.x += padding * 2;
            preferredSize.y += padding * 2;

            // Clamp to min/max
            preferredSize.x = Mathf.Clamp(preferredSize.x, minSize.x, maxSize.x);
            preferredSize.y = Mathf.Clamp(preferredSize.y, minSize.y, maxSize.y);

            // Apply size
            boxRect.sizeDelta = preferredSize;
        }

        /// <summary>
        /// 更新位置
        /// </summary>
        private void UpdatePosition()
        {
            switch (currentPosition)
            {
                case TextAnchor.UpperLeft:
                    boxRect.anchorMin = new Vector2(0, 1);
                    boxRect.anchorMax = new Vector2(0, 1);
                    boxRect.pivot = new Vector2(0, 1);
                    boxRect.anchoredPosition = new Vector2(50, -50);
                    break;

                case TextAnchor.UpperCenter:
                    boxRect.anchorMin = new Vector2(0.5f, 1);
                    boxRect.anchorMax = new Vector2(0.5f, 1);
                    boxRect.pivot = new Vector2(0.5f, 1);
                    boxRect.anchoredPosition = new Vector2(0, -50);
                    break;

                case TextAnchor.UpperRight:
                    boxRect.anchorMin = new Vector2(1, 1);
                    boxRect.anchorMax = new Vector2(1, 1);
                    boxRect.pivot = new Vector2(1, 1);
                    boxRect.anchoredPosition = new Vector2(-50, -50);
                    break;

                case TextAnchor.MiddleLeft:
                    boxRect.anchorMin = new Vector2(0, 0.5f);
                    boxRect.anchorMax = new Vector2(0, 0.5f);
                    boxRect.pivot = new Vector2(0, 0.5f);
                    boxRect.anchoredPosition = new Vector2(50, 0);
                    break;

                case TextAnchor.MiddleCenter:
                    boxRect.anchorMin = new Vector2(0.5f, 0.5f);
                    boxRect.anchorMax = new Vector2(0.5f, 0.5f);
                    boxRect.pivot = new Vector2(0.5f, 0.5f);
                    boxRect.anchoredPosition = Vector2.zero;
                    break;

                case TextAnchor.MiddleRight:
                    boxRect.anchorMin = new Vector2(1, 0.5f);
                    boxRect.anchorMax = new Vector2(1, 0.5f);
                    boxRect.pivot = new Vector2(1, 0.5f);
                    boxRect.anchoredPosition = new Vector2(-50, 0);
                    break;

                case TextAnchor.LowerLeft:
                    boxRect.anchorMin = new Vector2(0, 0);
                    boxRect.anchorMax = new Vector2(0, 0);
                    boxRect.pivot = new Vector2(0, 0);
                    boxRect.anchoredPosition = new Vector2(50, 50);
                    break;

                case TextAnchor.LowerCenter:
                    boxRect.anchorMin = new Vector2(0.5f, 0);
                    boxRect.anchorMax = new Vector2(0.5f, 0);
                    boxRect.pivot = new Vector2(0.5f, 0);
                    boxRect.anchoredPosition = new Vector2(0, 50);
                    break;

                case TextAnchor.LowerRight:
                    boxRect.anchorMin = new Vector2(1, 0);
                    boxRect.anchorMax = new Vector2(1, 0);
                    boxRect.pivot = new Vector2(1, 0);
                    boxRect.anchoredPosition = new Vector2(-50, 50);
                    break;
            }
        }

        /// <summary>
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeIn()
        {
            float elapsed = 0;
            Vector3 startScale = Vector3.one * 0.9f;
            Vector3 endScale = Vector3.one;

            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed / fadeInDuration);

                canvasGroup.alpha = t;
                transform.localScale = Vector3.Lerp(startScale, endScale, t);

                yield return null;
            }

            canvasGroup.alpha = 1;
            transform.localScale = endScale;
            fadeCoroutine = null;
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOut()
        {
            float elapsed = 0;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = Vector3.one * 0.95f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = animationCurve.Evaluate(elapsed / fadeOutDuration);

                canvasGroup.alpha = 1f - t;
                transform.localScale = Vector3.Lerp(startScale, endScale, t);

                yield return null;
            }

            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            isVisible = false;
            fadeCoroutine = null;
        }

        /// <summary>
        /// 设置样式
        /// </summary>
        public void SetStyle(Color bgColor, Color txtColor)
        {
            backgroundColor = bgColor;
            textColor = txtColor;

            if (backgroundImage != null)
                backgroundImage.color = backgroundColor;

            if (instructionText != null)
                instructionText.color = textColor;
        }

        /// <summary>
        /// 播放强调动画
        /// </summary>
        public void PlayEmphasisAnimation()
        {
            if (!isVisible) return;

            // PrimeTween scale animation with ping-pong effect
            Tween.Scale(transform, Vector3.one * 1.05f, 0.2f, Ease.InOutQuad)
                .OnComplete(() => Tween.Scale(transform, Vector3.one, 0.2f, Ease.InOutQuad));
        }
    }
}