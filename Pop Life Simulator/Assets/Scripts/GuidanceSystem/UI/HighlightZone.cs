using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PopLife.GuidanceSystem.UI
{
    /// <summary>
    /// 高亮区域，显示箭头指示和高亮效果
    /// Highlight zone showing arrow indicators and highlight effects
    /// </summary>
    public class HighlightZone : MonoBehaviour
    {
        [Header("Arrow Components")]
        [SerializeField] private GameObject arrowPrefab;
        [SerializeField] private Image arrowImage;
        [SerializeField] private RectTransform arrowRect;

        [Header("Highlight Box")]
        [SerializeField] private Image highlightBorder;
        [SerializeField] private float borderWidth = 3f;
        [SerializeField] private Color highlightColor = Color.yellow;

        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.1f;
        [SerializeField] private float bounceDistance = 10f;

        // State
        private bool isVisible;
        private Coroutine animationCoroutine;
        private Rect currentTargetRect;

        private void Awake()
        {
            InitializeComponents();
            HideImmediate();
        }

        private void InitializeComponents()
        {
            // Create arrow if not assigned
            if (arrowImage == null && arrowPrefab == null)
            {
                var arrowObj = new GameObject("Arrow");
                arrowObj.transform.SetParent(transform, false);
                arrowImage = arrowObj.AddComponent<Image>();
                arrowRect = arrowObj.GetComponent<RectTransform>();
            }

            // Create highlight border if not assigned
            if (highlightBorder == null)
            {
                var borderObj = new GameObject("HighlightBorder");
                borderObj.transform.SetParent(transform, false);
                highlightBorder = borderObj.AddComponent<Image>();
                highlightBorder.color = highlightColor;
                highlightBorder.raycastTarget = false;
            }
        }

        /// <summary>
        /// 显示高亮
        /// </summary>
        public void ShowHighlight(Rect targetRect, ArrowDirection direction = ArrowDirection.Auto)
        {
            currentTargetRect = targetRect;

            // Position highlight border
            PositionHighlightBorder(targetRect);

            // Position arrow
            if (direction == ArrowDirection.Auto)
            {
                direction = CalculateBestArrowDirection(targetRect);
            }
            PositionArrow(targetRect, direction);

            // Show components
            if (highlightBorder != null)
                highlightBorder.gameObject.SetActive(true);

            if (arrowImage != null)
                arrowImage.gameObject.SetActive(true);

            isVisible = true;

            // Start animation
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            animationCoroutine = StartCoroutine(AnimateHighlight());
        }

        /// <summary>
        /// 隐藏高亮
        /// </summary>
        public void HideHighlight()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            if (highlightBorder != null)
                highlightBorder.gameObject.SetActive(false);

            if (arrowImage != null)
                arrowImage.gameObject.SetActive(false);

            isVisible = false;
        }

        /// <summary>
        /// 立即隐藏
        /// </summary>
        public void HideImmediate()
        {
            HideHighlight();
        }

        /// <summary>
        /// 定位高亮边框
        /// </summary>
        private void PositionHighlightBorder(Rect targetRect)
        {
            if (highlightBorder == null) return;

            var borderRect = highlightBorder.GetComponent<RectTransform>();
            if (borderRect == null) return;

            // Convert screen rect to canvas space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                targetRect.center,
                null,
                out Vector2 localCenter
            );

            borderRect.anchoredPosition = localCenter;
            borderRect.sizeDelta = targetRect.size + Vector2.one * borderWidth * 2;
        }

        /// <summary>
        /// 定位箭头
        /// </summary>
        private void PositionArrow(Rect targetRect, ArrowDirection direction)
        {
            if (arrowRect == null) return;

            Vector2 arrowPos = Vector2.zero;
            float rotation = 0;
            float offset = 50f; // Distance from target

            switch (direction)
            {
                case ArrowDirection.Up:
                    arrowPos = new Vector2(targetRect.center.x, targetRect.yMax + offset);
                    rotation = 180;
                    break;

                case ArrowDirection.Down:
                    arrowPos = new Vector2(targetRect.center.x, targetRect.yMin - offset);
                    rotation = 0;
                    break;

                case ArrowDirection.Left:
                    arrowPos = new Vector2(targetRect.xMin - offset, targetRect.center.y);
                    rotation = -90;
                    break;

                case ArrowDirection.Right:
                    arrowPos = new Vector2(targetRect.xMax + offset, targetRect.center.y);
                    rotation = 90;
                    break;

                case ArrowDirection.UpLeft:
                    arrowPos = new Vector2(targetRect.xMin - offset, targetRect.yMax + offset);
                    rotation = -135;
                    break;

                case ArrowDirection.UpRight:
                    arrowPos = new Vector2(targetRect.xMax + offset, targetRect.yMax + offset);
                    rotation = 135;
                    break;

                case ArrowDirection.DownLeft:
                    arrowPos = new Vector2(targetRect.xMin - offset, targetRect.yMin - offset);
                    rotation = -45;
                    break;

                case ArrowDirection.DownRight:
                    arrowPos = new Vector2(targetRect.xMax + offset, targetRect.yMin - offset);
                    rotation = 45;
                    break;
            }

            // Convert to local position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform,
                arrowPos,
                null,
                out Vector2 localPos
            );

            arrowRect.anchoredPosition = localPos;
            arrowRect.rotation = Quaternion.Euler(0, 0, rotation);
        }

        /// <summary>
        /// 计算最佳箭头方向
        /// </summary>
        private ArrowDirection CalculateBestArrowDirection(Rect targetRect)
        {
            // Find which screen edge has most space
            float topSpace = Screen.height - targetRect.yMax;
            float bottomSpace = targetRect.yMin;
            float leftSpace = targetRect.xMin;
            float rightSpace = Screen.width - targetRect.xMax;

            float maxSpace = Mathf.Max(topSpace, bottomSpace, leftSpace, rightSpace);

            if (maxSpace == topSpace) return ArrowDirection.Up;
            if (maxSpace == bottomSpace) return ArrowDirection.Down;
            if (maxSpace == leftSpace) return ArrowDirection.Left;
            return ArrowDirection.Right;
        }

        /// <summary>
        /// 高亮动画
        /// </summary>
        private IEnumerator AnimateHighlight()
        {
            float time = 0;
            Vector2 originalArrowPos = arrowRect.anchoredPosition;

            while (isVisible)
            {
                time += Time.unscaledDeltaTime;

                // Pulse border
                if (highlightBorder != null)
                {
                    float pulse = Mathf.Sin(time * pulseSpeed) * pulseAmount + 1f;
                    highlightBorder.transform.localScale = Vector3.one * pulse;

                    // Fade alpha
                    Color color = highlightBorder.color;
                    color.a = 0.5f + Mathf.Sin(time * pulseSpeed) * 0.3f;
                    highlightBorder.color = color;
                }

                // Bounce arrow
                if (arrowRect != null)
                {
                    float bounce = Mathf.Sin(time * pulseSpeed * 0.8f) * bounceDistance;
                    arrowRect.anchoredPosition = originalArrowPos + Vector2.up * bounce;
                }

                yield return null;
            }
        }

        /// <summary>
        /// 设置高亮颜色
        /// </summary>
        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
            if (highlightBorder != null)
                highlightBorder.color = color;
        }

        /// <summary>
        /// 设置箭头精灵
        /// </summary>
        public void SetArrowSprite(Sprite sprite)
        {
            if (arrowImage != null)
                arrowImage.sprite = sprite;
        }
    }
}