using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PopLife.GuidanceSystem.UI
{
    /// <summary>
    /// 指引遮罩，提供全屏遮罩和镂空功能
    /// Guidance mask providing fullscreen overlay and cutout functionality
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GuidanceMask : MonoBehaviour, IPointerClickHandler
    {
        [Header("Mask Components")]
        [SerializeField] private Image maskImage;
        [SerializeField] private Material cutoutMaterial;  // Custom material with cutout shader
        [SerializeField] private RectTransform maskRect;

        [Header("Appearance Settings")]
        [SerializeField] private Color maskColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private float defaultOpacity = 0.7f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Cutout Settings")]
        [SerializeField] private float cutoutPadding = 10f;
        [SerializeField] private float cutoutCornerRadius = 10f;
        [SerializeField] private bool animateCutout = true;
        [SerializeField] private float cutoutAnimationDuration = 0.3f;

        [Header("Click Handling")]
        [SerializeField] private bool blockOutsideClicks = true;
        [SerializeField] private bool allowCutoutClick = true;

        // Current state
        private Rect currentCutoutRect;
        private HighlightShape currentShape;
        private bool isVisible;
        private Coroutine fadeCoroutine;
        private Coroutine cutoutAnimationCoroutine;

        // Events
        public event System.Action OnMaskClicked;
        public event System.Action OnCutoutClicked;

        private void Awake()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // Get or create mask image
            if (maskImage == null)
            {
                maskImage = GetComponent<Image>();
                if (maskImage == null)
                {
                    maskImage = gameObject.AddComponent<Image>();
                }
            }

            // Get rect transform
            if (maskRect == null)
            {
                maskRect = GetComponent<RectTransform>();
            }

            // Set to full screen
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.sizeDelta = Vector2.zero;
            maskRect.anchoredPosition = Vector2.zero;

            // Setup mask image
            maskImage.color = maskColor;
            maskImage.raycastTarget = blockOutsideClicks;

            // Try to load cutout material if not assigned
            if (cutoutMaterial == null)
            {
                // In production, load from Resources or create programmatically
                // For now, use standard UI material
                maskImage.material = null;
            }
            else
            {
                maskImage.material = cutoutMaterial;
            }

            // Start hidden
            gameObject.SetActive(false);
            isVisible = false;
        }

        /// <summary>
        /// 显示全屏遮罩（无镂空）
        /// </summary>
        public void ShowFullMask(float opacity = -1)
        {
            if (opacity < 0)
                opacity = defaultOpacity;

            currentCutoutRect = Rect.zero;
            UpdateMaskAppearance(opacity);

            gameObject.SetActive(true);
            isVisible = true;
        }

        /// <summary>
        /// 创建镂空区域
        /// </summary>
        public void CreateCutout(Rect screenRect, HighlightShape shape = HighlightShape.Rectangle)
        {
            if (!isVisible)
            {
                gameObject.SetActive(true);
                isVisible = true;
            }

            // Add padding
            screenRect.x -= cutoutPadding;
            screenRect.y -= cutoutPadding;
            screenRect.width += cutoutPadding * 2;
            screenRect.height += cutoutPadding * 2;

            currentShape = shape;

            if (animateCutout && currentCutoutRect != Rect.zero)
            {
                // Animate transition
                if (cutoutAnimationCoroutine != null)
                    StopCoroutine(cutoutAnimationCoroutine);

                cutoutAnimationCoroutine = StartCoroutine(AnimateCutoutTransition(currentCutoutRect, screenRect));
            }
            else
            {
                // Immediate update
                currentCutoutRect = screenRect;
                ApplyCutout(screenRect, shape);
            }
        }

        /// <summary>
        /// 创建UI元素的镂空
        /// </summary>
        public void CreateCutoutForUI(RectTransform target)
        {
            if (target == null) return;

            // Get world corners
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            // Convert to screen space
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                maskRect,
                RectTransformUtility.WorldToScreenPoint(null, corners[0]),
                null,
                out Vector3 worldPos))
            {
                Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

                Rect screenRect = new Rect(
                    min.x,
                    min.y,
                    max.x - min.x,
                    max.y - min.y
                );

                CreateCutout(screenRect);
            }
        }

        /// <summary>
        /// 创建世界对象的镂空
        /// </summary>
        public void CreateCutoutForWorldObject(Transform target, Vector2 size)
        {
            if (target == null || Camera.main == null) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position);
            Rect screenRect = new Rect(
                screenPos.x - size.x / 2,
                screenPos.y - size.y / 2,
                size.x,
                size.y
            );

            CreateCutout(screenRect);
        }

        /// <summary>
        /// 清除镂空
        /// </summary>
        public void ClearCutout()
        {
            currentCutoutRect = Rect.zero;
            ApplyCutout(Rect.zero, HighlightShape.Rectangle);
        }

        /// <summary>
        /// 淡入显示
        /// </summary>
        public void FadeIn(float duration = 0.3f)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            gameObject.SetActive(true);
            fadeCoroutine = StartCoroutine(FadeRoutine(0, maskColor.a, duration));
            isVisible = true;
        }

        /// <summary>
        /// 淡出隐藏
        /// </summary>
        public void FadeOut(float duration = 0.3f)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeRoutine(maskColor.a, 0, duration, () =>
            {
                gameObject.SetActive(false);
                isVisible = false;
            }));
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

            if (cutoutAnimationCoroutine != null)
            {
                StopCoroutine(cutoutAnimationCoroutine);
                cutoutAnimationCoroutine = null;
            }

            gameObject.SetActive(false);
            isVisible = false;
        }

        /// <summary>
        /// 应用镂空效果
        /// </summary>
        private void ApplyCutout(Rect screenRect, HighlightShape shape)
        {
            if (cutoutMaterial != null)
            {
                // Convert screen rect to normalized coordinates
                Vector2 center = new Vector2(
                    screenRect.center.x / Screen.width,
                    screenRect.center.y / Screen.height
                );

                Vector2 size = new Vector2(
                    screenRect.width / Screen.width,
                    screenRect.height / Screen.height
                );

                // Set shader properties
                cutoutMaterial.SetVector("_CutoutCenter", center);
                cutoutMaterial.SetVector("_CutoutSize", size);
                cutoutMaterial.SetFloat("_CutoutShape", (float)shape);
                cutoutMaterial.SetFloat("_CornerRadius", cutoutCornerRadius);
            }
            else
            {
                // Fallback: Create cutout using multiple UI elements
                CreateFallbackCutout(screenRect, shape);
            }
        }

        /// <summary>
        /// 创建备用镂空（使用多个UI元素）
        /// </summary>
        private void CreateFallbackCutout(Rect screenRect, HighlightShape shape)
        {
            // This is a simplified approach using 4 mask panels around the cutout
            // In production, would need proper implementation

            // For now, just reduce mask opacity in cutout area
            // This doesn't actually create a hole, just makes it less visible
            Color tempColor = maskImage.color;
            tempColor.a = 0.3f;
            maskImage.color = tempColor;
        }

        /// <summary>
        /// 更新遮罩外观
        /// </summary>
        private void UpdateMaskAppearance(float opacity)
        {
            Color newColor = maskColor;
            newColor.a = opacity;
            maskImage.color = newColor;
        }

        /// <summary>
        /// 淡入淡出协程
        /// </summary>
        private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, System.Action onComplete = null)
        {
            float elapsed = 0;
            Color color = maskImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeCurve.Evaluate(elapsed / duration);
                color.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                maskImage.color = color;
                yield return null;
            }

            color.a = toAlpha;
            maskImage.color = color;

            onComplete?.Invoke();
            fadeCoroutine = null;
        }

        /// <summary>
        /// 镂空过渡动画
        /// </summary>
        private IEnumerator AnimateCutoutTransition(Rect fromRect, Rect toRect)
        {
            float elapsed = 0;

            while (elapsed < cutoutAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeCurve.Evaluate(elapsed / cutoutAnimationDuration);

                Rect interpolatedRect = new Rect(
                    Mathf.Lerp(fromRect.x, toRect.x, t),
                    Mathf.Lerp(fromRect.y, toRect.y, t),
                    Mathf.Lerp(fromRect.width, toRect.width, t),
                    Mathf.Lerp(fromRect.height, toRect.height, t)
                );

                ApplyCutout(interpolatedRect, currentShape);
                yield return null;
            }

            currentCutoutRect = toRect;
            ApplyCutout(toRect, currentShape);
            cutoutAnimationCoroutine = null;
        }

        // IPointerClickHandler
        public void OnPointerClick(PointerEventData eventData)
        {
            // Check if click is inside cutout area
            if (currentCutoutRect != Rect.zero)
            {
                Vector2 clickPos = eventData.position;
                if (currentCutoutRect.Contains(clickPos))
                {
                    if (allowCutoutClick)
                    {
                        OnCutoutClicked?.Invoke();

                        // Allow click to pass through
                        eventData.Use();

                        // Trigger advancement in GuidanceManager
                        GuidanceManager.Instance?.AdvanceStep();
                    }
                }
                else
                {
                    if (blockOutsideClicks)
                    {
                        OnMaskClicked?.Invoke();
                        eventData.Use(); // Block the click
                    }
                }
            }
            else
            {
                OnMaskClicked?.Invoke();

                // Check if current step allows clicking anywhere
                var activeSequence = GuidanceManager.Instance?.GetActiveSequence();
                if (activeSequence != null && activeSequence.CurrentStep != null)
                {
                    if (activeSequence.CurrentStep.ActionType == ActionType.ClickAnywhere)
                    {
                        GuidanceManager.Instance?.AdvanceStep();
                    }
                }
            }
        }

        /// <summary>
        /// 设置是否阻止外部点击
        /// </summary>
        public void SetBlockOutsideClicks(bool block)
        {
            blockOutsideClicks = block;
            maskImage.raycastTarget = block;
        }

        /// <summary>
        /// 获取当前镂空区域
        /// </summary>
        public Rect GetCurrentCutout()
        {
            return currentCutoutRect;
        }

        /// <summary>
        /// 检查点是否在镂空内
        /// </summary>
        public bool IsPointInCutout(Vector2 screenPoint)
        {
            if (currentCutoutRect == Rect.zero)
                return false;

            return currentCutoutRect.Contains(screenPoint);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Visualize cutout area in Scene view
            if (currentCutoutRect != Rect.zero)
            {
                Gizmos.color = Color.yellow;
                Vector3 center = new Vector3(currentCutoutRect.center.x, currentCutoutRect.center.y, 0);
                Vector3 size = new Vector3(currentCutoutRect.width, currentCutoutRect.height, 0);
                Gizmos.DrawWireCube(center, size);
            }
        }
#endif
    }
}