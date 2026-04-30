using UnityEngine;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 标记一个 UI / 物体可以被 Lua 字符串 ID 调出 Spotlight。
    /// 挂载即自动注册到 SpotlightTargetRegistry，OnDisable 时自动移除。
    ///
    /// 多个实例可以共享同一 ID（例如 QuestTracker 的 entry 列表）。
    /// 查找时按 priority 降序选中；priority 相同取最后注册的。
    /// </summary>
    [AddComponentMenu("PopLife/UI/Spotlight Target")]
    [DisallowMultipleComponent]
    public class SpotlightTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("Spotlight 通过此 ID 在注册表中查找目标。多实例允许同 ID")]
        private string spotlightId;

        [SerializeField, Tooltip("多实例同 ID 时，priority 大者优先；相同则取最后注册的实例")]
        private int priority = 0;

        [SerializeField, Tooltip("覆盖默认的 RectTransform。留空则使用挂载物体的 RectTransform")]
        private RectTransform overrideRect;

        [SerializeField, Tooltip("Passthrough 模式下，若目标 Graphic.raycastTarget=false，是否在 spotlight 显示期间临时打开")]
        private bool ensureRaycastTarget = true;

        public string Id => spotlightId;
        public int Priority => priority;
        public bool EnsureRaycastTarget => ensureRaycastTarget;

        /// <summary>
        /// 返回用于 spotlight 计算的 RectTransform。
        /// 如果挂载物不是 UI（无 RectTransform）也返回 null，调用方应走 ForWorld 路径。
        /// </summary>
        public RectTransform GetRect()
        {
            if (overrideRect != null) return overrideRect;
            return transform as RectTransform;
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(spotlightId))
            {
                Debug.LogWarning($"[SpotlightTarget] '{name}' 的 spotlightId 为空，未注册到 Registry", this);
                return;
            }
            SpotlightTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (string.IsNullOrEmpty(spotlightId)) return;
            SpotlightTargetRegistry.Unregister(this);
        }
    }
}
