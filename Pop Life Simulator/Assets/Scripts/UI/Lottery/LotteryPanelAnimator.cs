using System;
using UnityEngine;
using PrimeTween;

namespace PopLife.UI
{
    /// <summary>
    /// Lottery 面板动画控制器
    /// 处理面板开闭动画和购买按钮反馈效果
    /// </summary>
    public class LotteryPanelAnimator : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private RectTransform panelRootRect;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Purchase Button")]
        [SerializeField] private RectTransform purchaseButtonRect;

        [Header("Panel Open Settings")]
        [SerializeField] private float openDuration = 0.4f;
        [SerializeField] private float openScaleFrom = 0.6f;

        [Header("Panel Close Settings")]
        [SerializeField] private float closeDuration = 0.2f;
        [SerializeField] private float closeScaleTo = 0.85f;

        [Header("Purchase Success")]
        [SerializeField] private float punchScale = 1.15f;
        [SerializeField] private float punchHalfDuration = 0.15f;

        [Header("Purchase Failure")]
        [SerializeField] private float shakeDuration = 0.35f;
        [SerializeField] private float shakeAmplitude = 8f;
        [SerializeField] private float shakeFrequency = 25f;

        // 活跃动画引用
        private Sequence openSequence;
        private Sequence closeSequence;
        private Sequence purchaseSequence;
        private Tween shakeTween;

        /// <summary>
        /// 面板打开动画：scale OutBack 弹入 + alpha 淡入
        /// </summary>
        public void PlayOpenAnimation()
        {
            KillAll();

            if (panelRootRect == null || panelCanvasGroup == null) return;

            // 设置初始状态
            panelCanvasGroup.alpha = 0f;
            panelRootRect.localScale = Vector3.one * openScaleFrom;

            // 淡入比 scale 快，先显形再弹
            float alphaDuration = openDuration * 0.6f;

            openSequence = Sequence.Create()
                .Group(Tween.Alpha(panelCanvasGroup, 1f, alphaDuration))
                .Group(Tween.Scale(panelRootRect, Vector3.one, openDuration, Ease.OutBack));
        }

        /// <summary>
        /// 面板关闭动画：scale InBack 收缩 + alpha 淡出
        /// </summary>
        public void PlayCloseAnimation(Action onComplete)
        {
            KillAll();

            if (panelRootRect == null || panelCanvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            closeSequence = Sequence.Create()
                .Group(Tween.Scale(panelRootRect, Vector3.one * closeScaleTo, closeDuration, Ease.InBack))
                .Group(Tween.Alpha(panelCanvasGroup, 0f, closeDuration, Ease.InQuad))
                .ChainCallback(this, target =>
                {
                    // 重置状态
                    if (target.panelRootRect != null)
                        target.panelRootRect.localScale = Vector3.one;
                    if (target.panelCanvasGroup != null)
                        target.panelCanvasGroup.alpha = 1f;
                    onComplete?.Invoke();
                });
        }

        /// <summary>
        /// 购买成功：按钮放大弹回
        /// </summary>
        public void PlayPurchaseSuccess()
        {
            if (purchaseSequence.isAlive)
                purchaseSequence.Stop();

            if (purchaseButtonRect == null) return;

            purchaseButtonRect.localScale = Vector3.one;

            purchaseSequence = Sequence.Create()
                .Chain(Tween.Scale(purchaseButtonRect, Vector3.one * punchScale, punchHalfDuration, Ease.OutQuad))
                .Chain(Tween.Scale(purchaseButtonRect, Vector3.one, punchHalfDuration, Ease.InQuad));
        }

        /// <summary>
        /// 购买失败：水平正弦衰减抖动
        /// </summary>
        public void PlayPurchaseFailure()
        {
            if (shakeTween.isAlive)
                shakeTween.Stop();

            if (purchaseButtonRect == null) return;

            float originalX = purchaseButtonRect.anchoredPosition.x;

            shakeTween = Tween.Custom(this, 0f, 1f, shakeDuration,
                (target, t) =>
                {
                    if (target.purchaseButtonRect == null) return;
                    float offset = Mathf.Sin(t * Mathf.PI * target.shakeFrequency)
                                 * (1f - t) * target.shakeAmplitude;
                    var pos = target.purchaseButtonRect.anchoredPosition;
                    pos.x = originalX + offset;
                    target.purchaseButtonRect.anchoredPosition = pos;
                },
                Ease.Linear);
        }

        private void KillAll()
        {
            if (openSequence.isAlive) openSequence.Stop();
            if (closeSequence.isAlive) closeSequence.Stop();
        }

        private void OnDestroy()
        {
            KillAll();
            if (purchaseSequence.isAlive) purchaseSequence.Stop();
            if (shakeTween.isAlive) shakeTween.Stop();
        }
    }
}
