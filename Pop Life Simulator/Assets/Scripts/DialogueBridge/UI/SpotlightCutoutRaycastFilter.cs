using UnityEngine;
using UnityEngine.UI;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 给 shader 模式的 maskImage 提供"cutout 内 raycast 穿透"能力。
    ///
    /// 问题：shader 模式下 maskImage 是单一全屏 Image，raycastTarget=true 会拦截整个屏幕，
    ///       cutout 区域虽然视觉透明但点击仍被吞掉，导致 Passthrough 模式目标按钮收不到点击。
    ///
    /// 解决：实现 ICanvasRaycastFilter — cutout 内返回 false（raycast 穿透到下层 UI），
    ///       cutout 外返回 true（mask 正常拦截）。
    ///
    /// 由 SpotlightPanel.SetTarget 每次更新 cutoutScreenRect。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpotlightCutoutRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private Rect cutoutScreenRect;
        private bool hasCutout;

        public void SetCutout(Rect screenRect)
        {
            cutoutScreenRect = screenRect;
            hasCutout = screenRect.width > 0 && screenRect.height > 0;
        }

        public void ClearCutout()
        {
            hasCutout = false;
        }

        /// <summary>
        /// Unity 的 GraphicRaycaster 在判定 raycast 命中本组件前调用此方法。
        /// 返回 true → 命中，事件交给本 Image；返回 false → 不命中，事件继续向下穿透。
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!hasCutout) return true;   // 没设置 cutout 则 maskImage 正常拦截全屏
            // cutout 内 → 不拦截（穿透到下层目标按钮）
            // cutout 外 → 拦截（外部 mask 防误触）
            return !cutoutScreenRect.Contains(screenPoint);
        }
    }
}
