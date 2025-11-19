using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PrimeTween;

namespace PopLife.NarrativeSystem.UI
{
    /// <summary>
    /// 单个对话框控制器，管理对话框的显示和交互
    /// Single conversation box controller managing display and interaction
    /// </summary>
    public class ConversationBox : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Components")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Outline outlineEffect;

        [Header("Appearance")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color highlightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color centerColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        [SerializeField] private float hoverScale = 1.05f;

        [Header("Animation")]
        [SerializeField] private float fadeSpeed = 2f;
        [SerializeField] private float scaleSpeed = 5f;

        // State
        private bool isVisible;
        private bool isHighlighted;
        private bool isHovered;
        private bool isClickable = true;
        private Vector3 baseScale;  // 基础缩放（由 FanConversationPanel 设置）
        private bool isScaleAnimating = false;  // 标记是否正在进行外部缩放动画

        // Events
        public event Action OnBoxClicked;

        // Properties
        public bool IsVisible => isVisible;
        public bool IsHighlighted => isHighlighted;

        private void Awake()
        {
            InitializeComponents();
            baseScale = Vector3.one;  // 默认基础缩放为1
        }

        private void InitializeComponents()
        {
            // Ensure components exist
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            if (outlineEffect == null)
                outlineEffect = GetComponent<Outline>();

            // Set initial state
            canvasGroup.alpha = 0;
            isVisible = false;
        }

        private void Update()
        {
            // Smooth animations
            UpdateVisibility();
            UpdateScale();
        }

        /// <summary>
        /// 设置对话框内容
        /// </summary>
        public void SetContent(string text, string speaker = null)
        {
            if (contentText != null)
            {
                contentText.text = text;
            }

            if (speakerText != null)
            {
                if (!string.IsNullOrEmpty(speaker))
                {
                    speakerText.text = speaker;
                    speakerText.gameObject.SetActive(true);
                }
                else
                {
                    speakerText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public void Show()
        {
            isVisible = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 隐藏对话框
        /// </summary>
        public void Hide()
        {
            isVisible = false;
        }

        /// <summary>
        /// 设置高亮状态（中间框）
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            isHighlighted = highlighted;

            if (backgroundImage != null)
            {
                backgroundImage.color = highlighted ? centerColor : normalColor;
            }

            if (outlineEffect != null)
            {
                outlineEffect.enabled = highlighted;
            }

            // 完全由 FanConversationPanel 控制 scale，这里不修改
        }

        /// <summary>
        /// 设置基础缩放（由 FanConversationPanel 调用）
        /// </summary>
        public void SetBaseScale(Vector3 scale)
        {
            baseScale = scale;
            if (!isHovered)
            {
                transform.localScale = scale;
            }
        }

        /// <summary>
        /// 标记外部缩放动画状态
        /// </summary>
        public void SetScaleAnimating(bool animating)
        {
            isScaleAnimating = animating;
        }

        /// <summary>
        /// 设置是否可点击
        /// </summary>
        public void SetClickable(bool clickable)
        {
            isClickable = clickable;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = clickable;
            }
        }

        /// <summary>
        /// 清除内容
        /// </summary>
        public void Clear()
        {
            if (contentText != null)
                contentText.text = "";

            if (speakerText != null)
            {
                speakerText.text = "";
                speakerText.gameObject.SetActive(false);
            }
        }

        // IPointerClickHandler
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isClickable || !isVisible || isHighlighted)
                return;

            OnBoxClicked?.Invoke();

            // Visual feedback
            PlayClickAnimation();
        }

        // IPointerEnterHandler
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isClickable || !isVisible || isHighlighted)
                return;

            isHovered = true;

            if (backgroundImage != null)
            {
                backgroundImage.color = highlightColor;
            }
        }

        // IPointerExitHandler
        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;

            if (backgroundImage != null && !isHighlighted)
            {
                backgroundImage.color = normalColor;
            }
        }

        /// <summary>
        /// 更新可见性动画
        /// </summary>
        private void UpdateVisibility()
        {
            if (canvasGroup == null) return;

            float targetAlpha = isVisible ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

            // Disable GameObject when fully hidden
            if (!isVisible && canvasGroup.alpha <= 0.01f)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 更新缩放动画
        /// </summary>
        private void UpdateScale()
        {
            // 如果外部正在进行缩放动画，不要干扰
            if (isScaleAnimating) return;

            // 只处理悬停效果
            if (isHovered && !isHighlighted)
            {
                // 悬停时稍微放大（基于基础scale）
                Vector3 targetScale = baseScale * hoverScale;
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleSpeed * Time.deltaTime);
            }
            else if (!isHovered)
            {
                // 不悬停时恢复到基础缩放
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, scaleSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// 播放点击动画
        /// </summary>
        private void PlayClickAnimation()
        {
            // 使用当前实际的缩放值，避免与其他动画冲突
            Vector3 currentScale = transform.localScale;
            Tween.Scale(transform, currentScale * 0.95f, 0.05f, Ease.InOutQuad)
                .OnComplete(() => Tween.Scale(transform, currentScale, 0.05f, Ease.InOutQuad));
        }

        /// <summary>
        /// 设置为选择框样式
        /// </summary>
        public void SetAsChoice(int choiceIndex)
        {
            // Add choice number prefix
            if (contentText != null && !contentText.text.StartsWith($"{choiceIndex + 1}. "))
            {
                contentText.text = $"{choiceIndex + 1}. {contentText.text}";
            }

            // Different background color for choices
            if (backgroundImage != null)
            {
                backgroundImage.color = new Color(0.2f, 0.25f, 0.3f, 0.95f);
            }
        }
    }
}