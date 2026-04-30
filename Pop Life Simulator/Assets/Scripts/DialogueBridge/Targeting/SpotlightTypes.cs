namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight 显示期间玩家与游戏的交互模式
    /// </summary>
    public enum InteractionMode
    {
        /// <summary>
        /// 高亮区域可点击穿透到目标按钮 → 触发 onTargetClicked 并关闭 spotlight。
        /// 外部 mask 拦截点击但不会关闭 spotlight（玩家被强制点目标按钮）。
        /// 适合"必须点这个按钮"型强制教学。
        /// </summary>
        Passthrough,

        /// <summary>
        /// 全屏任意位置点击都关闭 spotlight。
        /// spotlight 期间所有点击被 mask 吃掉，下方 UI 无法交互。
        /// 适合"看一眼就行"型概念展示。
        /// </summary>
        Blocking,
    }

    /// <summary>
    /// Spotlight 关闭的原因，用于 onClosed 回调
    /// </summary>
    public enum CloseReason
    {
        /// <summary>Lua/C# 显式调用 Hide() 或玩家通过其他系统关闭</summary>
        Manual,

        /// <summary>Passthrough 模式下玩家点击了高亮目标</summary>
        TargetClicked,

        /// <summary>Blocking 模式下玩家点击屏幕（任意位置）</summary>
        BackgroundClicked,

        /// <summary>目标 GameObject 销毁或从注册表移除（教程脚本可据此跳过该步骤）</summary>
        TargetLost,
    }
}
