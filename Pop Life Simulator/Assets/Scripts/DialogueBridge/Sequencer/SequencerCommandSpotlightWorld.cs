using System;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// [DEPRECATED] 已被新系统的 ShowSpotlight 通过注册表统一处理替代。
    /// 当前的 wrapper 通过 GameObject.Find 查找世界物体（兼容期，性能差）。
    ///
    /// 推荐做法：在世界物体上挂 SpotlightTarget 组件填 ID，然后
    /// ShowSpotlight("YourId", "你的提示文字", Right) —— 注册表自动识别为世界物体目标。
    /// </summary>
    [Obsolete("使用 SpotlightTarget 组件 + ShowSpotlight(targetId, text, ...) 替代")]
    public class SequencerCommandSpotlightWorld : SequencerCommand
    {
        public void Awake()
        {
            string targetName = GetParameter(0);
            string shapeStr = GetParameter(1, "RoundedRectangle");

            if (string.IsNullOrEmpty(targetName))
            {
                Stop();
                return;
            }

            SpotlightShape shape = SpotlightShape.RoundedRectangle;
            if (!string.IsNullOrEmpty(shapeStr)) Enum.TryParse(shapeStr, true, out shape);

            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning(
                    $"Sequencer: SpotlightWorld({targetName}) [DEPRECATED] - " +
                    $"请在世界物体挂 SpotlightTarget 组件填 ID，再用 ShowSpotlight(\"{targetName}\", \"<提示文字>\", ...)");
            }

            // 兼容期 fallback：用 GameObject.Find 查找
            var go = GameObject.Find(targetName);
            if (go == null)
            {
                Debug.LogWarning($"Sequencer: SpotlightWorld - 找不到 GameObject '{targetName}'");
                Stop();
                return;
            }

            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ForWorld(go),
                    text = SpotlightRequest.NullTextSentinel,
                    shape = shape,
                    position = TooltipPosition.Auto,
                    mode = InteractionMode.Passthrough,
                });
            }

            Stop();
        }
    }
}
