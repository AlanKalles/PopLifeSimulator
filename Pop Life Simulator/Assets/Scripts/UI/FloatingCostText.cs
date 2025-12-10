using UnityEngine;
using TMPro;
using PrimeTween;

namespace PopLife.UI
{
    public enum FloatDirection { Up, Down }

    /// <summary>
    /// 浮动资源文字效果 - 浮动并淡出
    /// </summary>
    public class FloatingCostText : MonoBehaviour
    {
        [SerializeField] private TMP_Text costText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("动画参数")]
        [SerializeField] private float floatDistance = 1f;
        [SerializeField] private float duration = 1f;
        [SerializeField] private Ease moveEase = Ease.OutQuad;
        [SerializeField] private Ease fadeEase = Ease.InQuad;

        [Header("颜色配置")]
        [SerializeField] private Color costColor = new Color(1f, 0.3f, 0.3f);    // 红色（扣钱）
        [SerializeField] private Color incomeColor = new Color(0.3f, 1f, 0.3f);  // 绿色（收入）
        [SerializeField] private Color fameColor = new Color(1f, 0.85f, 0.3f);   // 金色（Fame）

        /// <summary>
        /// 播放扣钱动画（红色 -$xxx）
        /// </summary>
        public void PlayCost(int cost, FloatDirection direction = FloatDirection.Up, bool useLocalPosition = false)
        {
            costText.text = $"-${cost:N0}";
            costText.color = costColor;
            PlayAnimation(direction, useLocalPosition);
        }

        /// <summary>
        /// 播放收入动画（绿色 +$xxx）
        /// </summary>
        public void PlayIncome(int amount, FloatDirection direction = FloatDirection.Up, bool useLocalPosition = false)
        {
            costText.text = $"+${amount:N0}";
            costText.color = incomeColor;
            PlayAnimation(direction, useLocalPosition);
        }

        /// <summary>
        /// 播放 Fame 动画（金色 +x.x）
        /// </summary>
        public void PlayFame(float amount, FloatDirection direction = FloatDirection.Up, bool useLocalPosition = false)
        {
            costText.text = $"+{amount:F1}";
            costText.color = fameColor;
            PlayAnimation(direction, useLocalPosition);
        }

        /// <summary>
        /// 旧接口兼容 - 等同于 PlayCost
        /// </summary>
        public void Play(int cost, FloatDirection direction = FloatDirection.Up, bool useLocalPosition = false)
        {
            PlayCost(cost, direction, useLocalPosition);
        }

        private void PlayAnimation(FloatDirection direction, bool useLocalPosition)
        {
            canvasGroup.alpha = 1f;
            float distance = direction == FloatDirection.Up ? floatDistance : -floatDistance;

            if (useLocalPosition)
            {
                var startY = transform.localPosition.y;
                var endY = startY + distance;
                Tween.LocalPositionY(transform, endY, duration, moveEase);
            }
            else
            {
                var startY = transform.position.y;
                var endY = startY + distance;
                Tween.PositionY(transform, endY, duration, moveEase);
            }

            Tween.Alpha(canvasGroup, 0f, duration, fadeEase)
                 .OnComplete(() => Destroy(gameObject));
        }
    }
}
