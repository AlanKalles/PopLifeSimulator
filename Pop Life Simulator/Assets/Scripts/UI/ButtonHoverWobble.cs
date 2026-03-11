using UnityEngine;
using UnityEngine.EventSystems;
using PrimeTween;

namespace PopLife.UI
{
    /// <summary>
    /// 通用按钮悬停晃动组件
    /// 鼠标悬停时绕 pivot 做 Z 轴来回旋转，离开时立刻回到原位
    /// 挂在任何带 RectTransform 的 UI 元素上即可使用
    /// </summary>
    public class ButtonHoverWobble : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Wobble Settings")]
        [Tooltip("最大晃动角度（度）")]
        [SerializeField] private float maxAngle = 3f;

        [Tooltip("单次晃动时长（秒，一个方向）")]
        [SerializeField] private float wobbleDuration = 0.15f;

        [Tooltip("晃动间隔（秒，两次晃动之间的停顿）")]
        [SerializeField] private float wobbleInterval = 1f;

        private RectTransform rectTransform;
        private Quaternion originalLocalRotation;
        private Sequence wobbleSequence;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalLocalRotation = rectTransform.localRotation;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StartWobble();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StopWobble();
        }

        private void StartWobble()
        {
            StopWobble();

            // LocalRotation + cycles:-1 无限循环，无递归无GC压力
            wobbleSequence = Sequence.Create(cycles: -1)
                .Chain(Tween.LocalRotation(rectTransform,
                    originalLocalRotation * Quaternion.Euler(0, 0, maxAngle),
                    wobbleDuration, Ease.InOutSine))
                .Chain(Tween.LocalRotation(rectTransform,
                    originalLocalRotation * Quaternion.Euler(0, 0, -maxAngle),
                    wobbleDuration * 2f, Ease.InOutSine))
                .Chain(Tween.LocalRotation(rectTransform,
                    originalLocalRotation,
                    wobbleDuration, Ease.InOutSine))
                .ChainDelay(wobbleInterval);
        }

        private void StopWobble()
        {
            if (wobbleSequence.isAlive)
                wobbleSequence.Stop();
            // 空值保护：动态UI可能在Awake前就被销毁
            if (rectTransform != null)
                rectTransform.localRotation = originalLocalRotation;
        }

        private void OnDisable()
        {
            StopWobble();
        }

        private void OnDestroy()
        {
            StopWobble();
        }
    }
}
