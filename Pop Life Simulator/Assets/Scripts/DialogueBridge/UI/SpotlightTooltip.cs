using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight Tooltip UI component
    /// Displays tutorial/hint text alongside the Spotlight highlight
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SpotlightTooltip : MonoBehaviour
    {
        #region Serialized Fields

        [Title("UI References")]
        [SerializeField, Required]
        private Image backgroundImage;

        [SerializeField]
        private Image arrowImage;

        [SerializeField, Required]
        private TextMeshProUGUI textTMP;

        [Title("Layout Settings")]
        [SerializeField, Tooltip("Distance between tooltip and spotlight")]
        private float margin = 20f;

        [SerializeField, Tooltip("Minimum distance from screen edges")]
        private float screenPadding = 10f;

        [SerializeField, Tooltip("Maximum width for the tooltip")]
        private float maxWidth = 400f;

        [Title("Animation Settings")]
        [SerializeField]
        private float fadeInDuration = 0.25f;

        [SerializeField]
        private float fadeOutDuration = 0.15f;

        [Title("Arrow Rotation (for single arrow sprite)")]
        [SerializeField, Tooltip("If true, rotate a single arrow sprite instead of using multiple sprites")]
        private bool useSingleArrowSprite = true;

        [SerializeField, ShowIf("useSingleArrowSprite"), Tooltip("Base rotation of the arrow sprite (pointing direction when rotation is 0)")]
        private ArrowDirection baseArrowDirection = ArrowDirection.Up;

        #endregion

        #region Private Fields

        private RectTransform rectTransform;
        private RectTransform arrowRectTransform;
        private CanvasGroup canvasGroup;
        private Canvas parentCanvas;
        private RectTransform parentRectTransform;
        private Camera cachedCamera;
        private bool isVisible = false;
        private Tween currentTween;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureInitialized();
            // Start hidden
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Ensures all components are initialized (lazy initialization)
        /// </summary>
        private void EnsureInitialized()
        {
            if (canvasGroup != null) return; // Already initialized

            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            parentCanvas = GetComponentInParent<Canvas>();
            parentRectTransform = rectTransform.parent as RectTransform;
            var rootCanvas = parentCanvas?.rootCanvas ?? parentCanvas;
            if (rootCanvas != null)
            {
                cachedCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : rootCanvas.worldCamera;
            }

            if (arrowImage != null)
            {
                arrowRectTransform = arrowImage.GetComponent<RectTransform>();
            }

            // Initialize alpha to 0 (Show will fade in)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            // Note: Don't call gameObject.SetActive(false) here
            // Let Show/Hide methods control the active state
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Show the tooltip with the given text at the specified position relative to spotlight
        /// </summary>
        /// <param name="text">Text content to display</param>
        /// <param name="spotlightRect">Screen rect of the spotlight area</param>
        /// <param name="position">Position relative to spotlight</param>
        /// <param name="customOffset">Custom normalized offset (only used when position is Custom)</param>
        public void Show(string text, Rect spotlightRect, TooltipPosition position, Vector2? customOffset = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.LogWarning("[SpotlightTooltip] Show called with empty text");
                return;
            }

            // Ensure components are initialized before Awake() runs
            EnsureInitialized();

            gameObject.SetActive(true);
            isVisible = true;

            // Set text content
            textTMP.text = text;

            // Force layout rebuild to get correct size (need to wait one frame for Content Size Fitter)
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            // Calculate and apply position
            Vector2 tooltipPos = CalculatePosition(spotlightRect, position, customOffset);
            SetScreenPosition(tooltipPos);

            // Fade in
            currentTween.Stop();
            currentTween = Tween.Alpha(canvasGroup, 1f, fadeInDuration, useUnscaledTime: true);
        }

        /// <summary>
        /// Hide the tooltip
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;

            isVisible = false;

            // Ensure components are initialized
            EnsureInitialized();

            currentTween.Stop();
            currentTween = Tween.Alpha(canvasGroup, 0f, fadeOutDuration, useUnscaledTime: true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                });
        }

        /// <summary>
        /// Check if tooltip is currently visible
        /// </summary>
        public bool IsVisible => isVisible;

        #endregion

        #region Position Calculation

        private Vector2 CalculatePosition(Rect spotlightRect, TooltipPosition position, Vector2? customOffset)
        {
            Vector2 tooltipSize = GetTooltipSize();
            Vector2 result;
            ArrowDirection arrowDir = ArrowDirection.None;

            switch (position)
            {
                case TooltipPosition.Left:
                    result = new Vector2(
                        spotlightRect.x - tooltipSize.x - margin,
                        spotlightRect.center.y - tooltipSize.y / 2
                    );
                    arrowDir = ArrowDirection.Right;
                    break;

                case TooltipPosition.Right:
                    result = new Vector2(
                        spotlightRect.xMax + margin,
                        spotlightRect.center.y - tooltipSize.y / 2
                    );
                    arrowDir = ArrowDirection.Left;
                    break;

                case TooltipPosition.Top:
                    result = new Vector2(
                        spotlightRect.center.x - tooltipSize.x / 2,
                        spotlightRect.yMax + margin
                    );
                    arrowDir = ArrowDirection.Down;
                    break;

                case TooltipPosition.Bottom:
                    result = new Vector2(
                        spotlightRect.center.x - tooltipSize.x / 2,
                        spotlightRect.y - tooltipSize.y - margin
                    );
                    arrowDir = ArrowDirection.Up;
                    break;

                case TooltipPosition.Auto:
                    (result, arrowDir) = CalculateAutoPosition(spotlightRect, tooltipSize);
                    break;

                case TooltipPosition.Custom:
                    if (customOffset.HasValue)
                    {
                        result = new Vector2(
                            customOffset.Value.x * Screen.width,
                            customOffset.Value.y * Screen.height
                        );
                    }
                    else
                    {
                        // Fallback to center if no offset provided
                        result = new Vector2(
                            Screen.width / 2 - tooltipSize.x / 2,
                            Screen.height / 2 - tooltipSize.y / 2
                        );
                    }
                    arrowDir = ArrowDirection.None;
                    break;

                default:
                    result = Vector2.zero;
                    break;
            }

            // Apply arrow direction
            SetArrowDirection(arrowDir);

            // Clamp to screen bounds
            return ClampToScreen(result, tooltipSize);
        }

        private (Vector2, ArrowDirection) CalculateAutoPosition(Rect spotlightRect, Vector2 tooltipSize)
        {
            // Calculate available space in each direction
            float spaceLeft = spotlightRect.x;
            float spaceRight = Screen.width - spotlightRect.xMax;
            float spaceTop = Screen.height - spotlightRect.yMax;
            float spaceBottom = spotlightRect.y;

            // Find the direction with the most space
            float maxSpace = Mathf.Max(spaceLeft, spaceRight, spaceTop, spaceBottom);

            // Choose position based on available space
            if (maxSpace == spaceRight && spaceRight >= tooltipSize.x + margin)
            {
                return (
                    new Vector2(spotlightRect.xMax + margin, spotlightRect.center.y - tooltipSize.y / 2),
                    ArrowDirection.Left
                );
            }
            if (maxSpace == spaceLeft && spaceLeft >= tooltipSize.x + margin)
            {
                return (
                    new Vector2(spotlightRect.x - tooltipSize.x - margin, spotlightRect.center.y - tooltipSize.y / 2),
                    ArrowDirection.Right
                );
            }
            if (maxSpace == spaceTop && spaceTop >= tooltipSize.y + margin)
            {
                return (
                    new Vector2(spotlightRect.center.x - tooltipSize.x / 2, spotlightRect.yMax + margin),
                    ArrowDirection.Down
                );
            }

            // Default to bottom
            return (
                new Vector2(spotlightRect.center.x - tooltipSize.x / 2, spotlightRect.y - tooltipSize.y - margin),
                ArrowDirection.Up
            );
        }

        private Vector2 ClampToScreen(Vector2 position, Vector2 size)
        {
            float minX = screenPadding;
            float maxX = Screen.width - size.x - screenPadding;
            float minY = screenPadding;
            float maxY = Screen.height - size.y - screenPadding;

            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY)
            );
        }

        private Vector2 GetTooltipSize()
        {
            // Ensure we have valid size
            if (rectTransform == null) return new Vector2(maxWidth, 100f);

            // For Content Size Fitter, use rect size instead of sizeDelta
            Vector2 size = rectTransform.rect.size;

            // If size is invalid (not yet laid out), use preferred size
            if (size.x <= 0 || size.y <= 0)
            {
                size = new Vector2(
                    Mathf.Min(textTMP.preferredWidth + 40f, maxWidth), // 40 = padding
                    textTMP.preferredHeight + 30f // 30 = padding
                );
            }

            return size;
        }

        private void SetScreenPosition(Vector2 screenPosition)
        {
            if (parentRectTransform == null) return;

            // screenPosition 是 tooltip 左下角的屏幕坐标，偏移到 pivot 位置
            Vector2 tooltipSize = GetTooltipSize();
            Vector2 pivotScreen = screenPosition + tooltipSize * rectTransform.pivot;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRectTransform, pivotScreen, cachedCamera, out Vector2 localPoint))
            {
                rectTransform.localPosition = localPoint;
            }
        }

        #endregion

        #region Arrow Direction

        private void SetArrowDirection(ArrowDirection direction)
        {
            if (arrowImage == null) return;

            if (direction == ArrowDirection.None)
            {
                arrowImage.gameObject.SetActive(false);
                return;
            }

            arrowImage.gameObject.SetActive(true);

            if (useSingleArrowSprite)
            {
                // Rotate single arrow sprite
                float rotation = GetArrowRotation(direction);
                arrowRectTransform.localRotation = Quaternion.Euler(0, 0, rotation);

                // Position arrow on the correct edge
                PositionArrow(direction);
            }
        }

        private float GetArrowRotation(ArrowDirection targetDirection)
        {
            // Calculate rotation needed to point arrow in target direction
            // Based on the base direction of the sprite
            int baseAngle = GetDirectionAngle(baseArrowDirection);
            int targetAngle = GetDirectionAngle(targetDirection);

            return targetAngle - baseAngle;
        }

        private int GetDirectionAngle(ArrowDirection direction)
        {
            return direction switch
            {
                ArrowDirection.Up => 0,
                ArrowDirection.Right => -90,
                ArrowDirection.Down => 180,
                ArrowDirection.Left => 90,
                _ => 0
            };
        }

        private void PositionArrow(ArrowDirection direction)
        {
            if (arrowRectTransform == null) return;

            // Position arrow on the edge closest to the spotlight
            Vector2 anchorMin, anchorMax, pivot;

            switch (direction)
            {
                case ArrowDirection.Left: // Arrow points left (tooltip is on the right)
                    anchorMin = anchorMax = new Vector2(0, 0.5f);
                    pivot = new Vector2(1, 0.5f);
                    break;
                case ArrowDirection.Right: // Arrow points right (tooltip is on the left)
                    anchorMin = anchorMax = new Vector2(1, 0.5f);
                    pivot = new Vector2(0, 0.5f);
                    break;
                case ArrowDirection.Up: // Arrow points up (tooltip is below)
                    anchorMin = anchorMax = new Vector2(0.5f, 1);
                    pivot = new Vector2(0.5f, 0);
                    break;
                case ArrowDirection.Down: // Arrow points down (tooltip is above)
                    anchorMin = anchorMax = new Vector2(0.5f, 0);
                    pivot = new Vector2(0.5f, 1);
                    break;
                default:
                    return;
            }

            arrowRectTransform.anchorMin = anchorMin;
            arrowRectTransform.anchorMax = anchorMax;
            arrowRectTransform.pivot = pivot;
            arrowRectTransform.anchoredPosition = Vector2.zero;
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Test Show")]
        private void TestShow()
        {
            Show("This is a test tooltip message for the spotlight tutorial system.",
                 new Rect(Screen.width / 2 - 100, Screen.height / 2 - 50, 200, 100),
                 TooltipPosition.Right);
        }

        [Button("Test Hide")]
        private void TestHide()
        {
            Hide();
        }
#endif

        #endregion
    }
}
