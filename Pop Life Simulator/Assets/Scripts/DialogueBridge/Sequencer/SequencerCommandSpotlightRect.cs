using System;
using System.Globalization;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// [DEPRECATED] 已被 ShowSpotlight("rect:x,y,w,h", text, ...) 替代。
    /// 第一轮兼容期保留为 thin wrapper。
    ///
    /// 旧用法：SpotlightRect(100, 200, 300, 150) / SpotlightRect(100, 200, 300, 150, Circle)
    /// 新用法：ShowSpotlight("rect:100,200,300,150", "你的提示文字", Right)
    /// </summary>
    [Obsolete("使用 ShowSpotlight(\"rect:x,y,w,h\", text, ...) 替代")]
    public class SequencerCommandSpotlightRect : SequencerCommand
    {
        public void Awake()
        {
            string xStr = GetParameter(0);
            string yStr = GetParameter(1);
            string widthStr = GetParameter(2);
            string heightStr = GetParameter(3);
            string shapeStr = GetParameter(4, "RoundedRectangle");

            if (string.IsNullOrEmpty(xStr) || string.IsNullOrEmpty(yStr) ||
                string.IsNullOrEmpty(widthStr) || string.IsNullOrEmpty(heightStr))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: SpotlightRect() - missing parameters");
                }
                Stop();
                return;
            }

            const NumberStyles style = NumberStyles.Float;
            var ic = CultureInfo.InvariantCulture;
            if (!float.TryParse(xStr, style, ic, out var x)
                || !float.TryParse(yStr, style, ic, out var y)
                || !float.TryParse(widthStr, style, ic, out var w)
                || !float.TryParse(heightStr, style, ic, out var h))
            {
                Debug.LogWarning("Sequencer: SpotlightRect() - invalid number");
                Stop();
                return;
            }

            SpotlightShape shape = SpotlightShape.RoundedRectangle;
            if (!string.IsNullOrEmpty(shapeStr)) Enum.TryParse(shapeStr, true, out shape);

            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning(
                    $"Sequencer: SpotlightRect [DEPRECATED] - 请迁移到 ShowSpotlight(\"rect:{x},{y},{w},{h}\", \"<提示文字>\", Right)");
            }

            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ForScreenRect(new Rect(x, y, w, h)),
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
