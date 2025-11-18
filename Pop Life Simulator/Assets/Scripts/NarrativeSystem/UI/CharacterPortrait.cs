using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;

namespace PopLife.NarrativeSystem.UI
{
    /// <summary>
    /// 角色肖像控制器，管理对话中的角色立绘显示
    /// Character portrait controller managing character art display in conversations
    /// </summary>
    public class CharacterPortrait : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float bounceScale = 1.1f;
        [SerializeField] private AnimationCurve appearanceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Portrait Effects")]
        [SerializeField] private bool enableBreathing = true;
        [SerializeField] private float breathingSpeed = 1f;
        [SerializeField] private float breathingAmount = 0.02f;

        [Header("Emotion System")]
        [SerializeField] private bool supportEmotions = false;
        [SerializeField] private Sprite[] emotionSprites;  // Different emotion sprites

        // State
        private Coroutine currentAnimation;
        private Vector3 originalScale;
        private bool isVisible;

        // Emotion enum
        public enum Emotion
        {
            Normal,
            Happy,
            Sad,
            Angry,
            Surprised,
            Embarrassed
        }

        private void Awake()
        {
            InitializeComponents();
            originalScale = transform.localScale;
        }

        private void InitializeComponents()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Start hidden
            canvasGroup.alpha = 0;
            isVisible = false;
        }

        private void Update()
        {
            if (isVisible && enableBreathing)
            {
                ApplyBreathingEffect();
            }
        }

        /// <summary>
        /// 设置角色肖像
        /// </summary>
        public void SetCharacter(Sprite portrait, string characterName)
        {
            if (portraitImage != null && portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.SetNativeSize(); // Maintain original aspect ratio
            }

            if (nameLabel != null && !string.IsNullOrEmpty(characterName))
            {
                nameLabel.text = characterName;
            }
        }

        /// <summary>
        /// 显示肖像
        /// </summary>
        public void Show(bool animated = true)
        {
            if (isVisible) return;

            isVisible = true;
            gameObject.SetActive(true);

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            if (animated)
            {
                currentAnimation = StartCoroutine(AnimateAppearance());
            }
            else
            {
                canvasGroup.alpha = 1;
                transform.localScale = originalScale;
            }
        }

        /// <summary>
        /// 隐藏肖像
        /// </summary>
        public void Hide(bool animated = true)
        {
            if (!isVisible) return;

            isVisible = false;

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            if (animated)
            {
                currentAnimation = StartCoroutine(AnimateDisappearance());
            }
            else
            {
                canvasGroup.alpha = 0;
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 设置情绪
        /// </summary>
        public void SetEmotion(Emotion emotion)
        {
            if (!supportEmotions || emotionSprites == null) return;

            int index = (int)emotion;
            if (index >= 0 && index < emotionSprites.Length && emotionSprites[index] != null)
            {
                if (portraitImage != null)
                {
                    portraitImage.sprite = emotionSprites[index];
                    PlayEmotionChangeEffect();
                }
            }
        }

        /// <summary>
        /// 播放说话动画
        /// </summary>
        public void PlayTalkingAnimation()
        {
            if (!isVisible) return;

            // Simple bounce effect with PrimeTween
            Tween.Scale(transform, originalScale * 1.05f, 0.15f, Ease.InOutQuad)
                .OnComplete(() => Tween.Scale(transform, originalScale, 0.15f, Ease.InOutQuad));
        }

        /// <summary>
        /// 播放强调动画
        /// </summary>
        public void PlayEmphasisAnimation()
        {
            if (!isVisible) return;

            // Shake effect with PrimeTween
            var originalPos = transform.localPosition;
            Tween.LocalPosition(transform, originalPos + Vector3.up * 10f, 0.05f, Ease.OutQuad)
                .OnComplete(() =>
                {
                    Tween.LocalPosition(transform, originalPos, 0.05f, Ease.InQuad)
                        .OnComplete(() =>
                        {
                            Tween.LocalPosition(transform, originalPos + Vector3.up * 5f, 0.05f, Ease.OutQuad)
                                .OnComplete(() => Tween.LocalPosition(transform, originalPos, 0.05f, Ease.InQuad));
                        });
                });
        }

        /// <summary>
        /// 出现动画
        /// </summary>
        private IEnumerator AnimateAppearance()
        {
            // Start from scaled down and transparent
            transform.localScale = originalScale * 0.8f;
            canvasGroup.alpha = 0;

            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = appearanceCurve.Evaluate(elapsed / fadeInDuration);

                canvasGroup.alpha = t;
                transform.localScale = Vector3.Lerp(originalScale * 0.8f, originalScale * bounceScale, t);

                yield return null;
            }

            // Settle to normal scale with PrimeTween
            Tween.Scale(transform, originalScale, 0.2f, Ease.OutBack);

            canvasGroup.alpha = 1;
            currentAnimation = null;
        }

        /// <summary>
        /// 消失动画
        /// </summary>
        private IEnumerator AnimateDisappearance()
        {
            float elapsed = 0;
            Vector3 startScale = transform.localScale;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;

                canvasGroup.alpha = 1f - t;
                transform.localScale = Vector3.Lerp(startScale, originalScale * 0.9f, t);

                yield return null;
            }

            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
            currentAnimation = null;
        }

        /// <summary>
        /// 应用呼吸效果
        /// </summary>
        private void ApplyBreathingEffect()
        {
            float breathValue = Mathf.Sin(Time.time * breathingSpeed) * breathingAmount;
            transform.localScale = originalScale * (1f + breathValue);
        }

        /// <summary>
        /// 播放情绪变化效果
        /// </summary>
        private void PlayEmotionChangeEffect()
        {
            // Quick flash effect with PrimeTween
            if (canvasGroup != null)
            {
                Tween.Alpha(canvasGroup, 0.5f, 0.1f, Ease.Linear)
                    .OnComplete(() => Tween.Alpha(canvasGroup, 1f, 0.1f, Ease.Linear));
            }

            // Small jump
            PlayEmphasisAnimation();
        }

        /// <summary>
        /// 设置肖像框架样式
        /// </summary>
        public void SetFrameStyle(Sprite frameSprite)
        {
            if (frameImage != null && frameSprite != null)
            {
                frameImage.sprite = frameSprite;
                frameImage.enabled = true;
            }
        }

        /// <summary>
        /// 高亮效果（当角色说话时）
        /// </summary>
        public void SetHighlight(bool highlighted)
        {
            if (portraitImage != null)
            {
                portraitImage.color = highlighted ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
            }

            if (frameImage != null)
            {
                frameImage.color = highlighted ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }
    }
}