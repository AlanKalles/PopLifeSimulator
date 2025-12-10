using UnityEngine;
using TMPro;
using PrimeTween;

namespace PopLife.UI
{
    public enum FloatDirection { Up, Down }

    /// <summary>
    /// 浮动扣钱文字效果 - 浮动并淡出
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

        /// <summary>
        /// 播放浮动动画
        /// </summary>
        /// <param name="cost">扣除的金额</param>
        /// <param name="direction">浮动方向</param>
        /// <param name="useLocalPosition">是否使用本地坐标（UI用true，世界物体用false）</param>
        public void Play(int cost, FloatDirection direction = FloatDirection.Up, bool useLocalPosition = false)
        {
            costText.text = $"-${cost:N0}";
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
