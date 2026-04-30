using System;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// [DEPRECATED] 已被 ShowSpotlight(targetId, text, [position], [shape], [mode]) 替代。
    /// 第一轮兼容期保留为 thin wrapper，转发到新 SpotlightManager.Show() 接口。
    ///
    /// 兼容期 wrapper 用 NullTextSentinel ("NULL")，仅显示 spotlight 不显示 tooltip；
    /// 提醒设计师迁移到新 ShowSpotlight 命令并补充实际 tooltip 文本。
    ///
    /// 旧用法：Spotlight(BuildButton) / Spotlight(BuildButton, Circle)
    /// 新用法：ShowSpotlight(BuildButton, "Click here to build", Right)
    /// </summary>
    [Obsolete("使用 ShowSpotlight(targetId, text, position, shape, mode) 替代。tooltip 文本必填。")]
    public class SequencerCommandSpotlight : SequencerCommand
    {
        public void Awake()
        {
            string targetName = GetParameter(0);
            string shapeStr = GetParameter(1, "RoundedRectangle");

            if (string.IsNullOrEmpty(targetName))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: Spotlight() - missing target name parameter");
                }
                Stop();
                return;
            }

            SpotlightShape shape = SpotlightShape.RoundedRectangle;
            if (!string.IsNullOrEmpty(shapeStr))
            {
                Enum.TryParse(shapeStr, true, out shape);
            }

            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning(
                    $"Sequencer: Spotlight({targetName}) [DEPRECATED] - 请迁移到 ShowSpotlight(\"{targetName}\", \"<你的提示文字>\", Right)");
            }

            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ById(targetName),
                    text = SpotlightRequest.NullTextSentinel,  // 旧命令无 tooltip 文本，先仅显示高亮
                    shape = shape,
                    position = TooltipPosition.Auto,
                    mode = InteractionMode.Passthrough,
                });
            }

            Stop();
        }
    }
}
