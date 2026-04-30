using System;
using UnityEngine;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Spotlight 显示请求。所有可配置参数集中在此结构。
    ///
    /// text 三种语义：
    /// <list type="bullet">
    /// <item>普通字符串 → 显示 tooltip 文本</item>
    /// <item>C# <c>null</c> 或字符串 <c>"NULL"</c>（区分大小写）→ 仅显示 spotlight，不显示 tooltip</item>
    /// <item>空字符串 <c>""</c> → throw ArgumentException（防止误填）</item>
    /// </list>
    /// target 必须 IsValid。
    /// </summary>
    public struct SpotlightRequest
    {
        /// <summary>"无 tooltip" sentinel 字符串（用于 Lua/Sequencer 等无法传 C# null 的场景）</summary>
        public const string NullTextSentinel = "NULL";

        /// <summary>目标规格（必填）</summary>
        public SpotlightTargetSpec target;

        /// <summary>
        /// Tooltip 文本。普通字符串 → 显示 tooltip；
        /// C# null 或字符串 "NULL" → 不显示 tooltip 仅 spotlight；
        /// 空字符串 "" → throw（防误填）
        /// </summary>
        public string text;

        /// <summary>Spotlight 形状</summary>
        public SpotlightShape shape;

        /// <summary>Tooltip 相对 spotlight 的位置</summary>
        public TooltipPosition position;

        /// <summary>玩家交互模式：Passthrough / Blocking</summary>
        public InteractionMode mode;

        /// <summary>Custom 位置时的偏移（normalized 0..1）</summary>
        public Vector2? customOffset;

        /// <summary>Passthrough 模式下，目标按钮被点击时触发（在 Hide 之前调用）</summary>
        public Action onTargetClicked;

        /// <summary>spotlight 关闭时触发，参数为关闭原因</summary>
        public Action<CloseReason> onClosed;

        /// <summary>
        /// 构造默认请求：text 必填，其他参数采用默认值（RoundedRectangle / Auto / Passthrough）
        /// </summary>
        public static SpotlightRequest Default(SpotlightTargetSpec target, string text)
        {
            return new SpotlightRequest
            {
                target = target,
                text = text,
                shape = SpotlightShape.RoundedRectangle,
                position = TooltipPosition.Auto,
                mode = InteractionMode.Passthrough,
            };
        }

        /// <summary>
        /// 是否抑制 tooltip（仅显示 spotlight）。判定规则：text 为 C# null 或字符串 "NULL"。
        /// </summary>
        public bool IsTextSuppressed => text == null || text == NullTextSentinel;

        /// <summary>
        /// 校验请求合法性，不合法即 throw。在 SpotlightManager.Show 入口调用。
        ///
        /// 注意：text == null 不算非法（IsTextSuppressed = true，跳过 tooltip）；
        /// 但 text == "" 仍然 throw（防止设计师误填空字符串）。
        /// </summary>
        public void Validate()
        {
            // text == null 是合法的 "no tooltip" 表示；只有 "" 才视为误填
            if (text != null && text.Length == 0)
            {
                throw new ArgumentException(
                    "[SpotlightRequest] text 为空字符串非法。如需无 tooltip 请传 C# null 或字符串 \"NULL\"。");
            }
            if (!target.IsValid)
            {
                throw new ArgumentException(
                    "[SpotlightRequest] target 无效（kind=None）——必须通过工厂方法构造");
            }
        }
    }
}
