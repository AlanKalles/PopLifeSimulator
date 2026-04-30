using System;
using System.Globalization;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// [DEPRECATED] 已被 ShowSpotlight("norm:x,y,w,h", text, ...) 替代。
    ///
    /// 旧用法：SpotlightNormalized(0.4, 0.4, 0.2, 0.2)
    /// 新用法：ShowSpotlight("norm:0.4,0.4,0.2,0.2", "你的提示文字", Right)
    /// </summary>
    [Obsolete("使用 ShowSpotlight(\"norm:x,y,w,h\", text, ...) 替代")]
    public class SequencerCommandSpotlightNormalized : SequencerCommand
    {
        public void Awake()
        {
            string xStr = GetParameter(0);
            string yStr = GetParameter(1);
            string widthStr = GetParameter(2);
            string heightStr = GetParameter(3);
            string shapeStr = GetParameter(4, "RoundedRectangle");

            const NumberStyles style = NumberStyles.Float;
            var ic = CultureInfo.InvariantCulture;
            if (!float.TryParse(xStr, style, ic, out var nx)
                || !float.TryParse(yStr, style, ic, out var ny)
                || !float.TryParse(widthStr, style, ic, out var nw)
                || !float.TryParse(heightStr, style, ic, out var nh))
            {
                if (DialogueDebug.logWarnings)
                    Debug.LogWarning($"Sequencer: SpotlightNormalized() - invalid params");
                Stop();
                return;
            }

            SpotlightShape shape = SpotlightShape.RoundedRectangle;
            if (!string.IsNullOrEmpty(shapeStr)) Enum.TryParse(shapeStr, true, out shape);

            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning(
                    $"Sequencer: SpotlightNormalized [DEPRECATED] - 请迁移到 ShowSpotlight(\"norm:{nx},{ny},{nw},{nh}\", \"<提示文字>\", Right)");
            }

            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ForNormalizedRect(new Rect(nx, ny, nw, nh)),
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
