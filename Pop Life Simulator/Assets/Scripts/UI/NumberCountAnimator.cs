using System.Collections;
using UnityEngine;
using TMPro;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// 数字滚动动画工具 — 将TextMeshProUGUI的数值从起始值动画过渡到目标值
    /// 配合tick音效，使用EaseOutCubic缓动曲线
    /// </summary>
    public static class NumberCountAnimator
    {
        /// <summary>
        /// 数字滚动动画协程
        /// </summary>
        /// <param name="text">目标TextMeshProUGUI</param>
        /// <param name="startValue">起始值（通常为0）</param>
        /// <param name="endValue">目标值</param>
        /// <param name="duration">动画持续时间（秒）</param>
        /// <param name="format">数字格式化字符串，如 "${0:F0}" 或 "+{0:F0}"</param>
        /// <param name="tickInterval">tick音效间隔（秒）</param>
        /// <param name="finalColor">最终颜色（可选，动画结束时设置）</param>
        public static IEnumerator AnimateValue(
            TextMeshProUGUI text,
            float startValue,
            float endValue,
            float duration,
            string format = "${0:F0}",
            float tickInterval = 0.05f,
            Color? finalColor = null)
        {
            if (text == null) yield break;

            // 如果起止值相同或duration为0，直接设置最终值
            if (Mathf.Approximately(startValue, endValue) || duration <= 0f)
            {
                text.text = string.Format(format, endValue);
                if (finalColor.HasValue) text.color = finalColor.Value;
                yield break;
            }

            float elapsed = 0f;
            float lastTickTime = -tickInterval; // 确保第一帧就播放tick

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutCubic缓动（快开始慢结束）
                t = 1f - Mathf.Pow(1f - t, 3f);

                float currentValue = Mathf.Lerp(startValue, endValue, t);
                text.text = string.Format(format, currentValue);

                // 定期播放tick音效
                if (elapsed - lastTickTime >= tickInterval)
                {
                    lastTickTime = elapsed;
                    AudioManager.Instance?.PlaySound(AudioKeys.SETTLEMENT_TICK, 0.3f);
                }

                yield return null;
            }

            // 确保最终值精确
            text.text = string.Format(format, endValue);

            if (finalColor.HasValue)
            {
                text.color = finalColor.Value;
            }
        }
    }
}
