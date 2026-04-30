using System;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// [DEPRECATED] 新系统中 spotlight 与 tooltip 不可分离，此命令已 no-op。
    ///
    /// 旧用法：Spotlight(BuildButton); SpotlightTooltip(Right, ClickButton)
    /// 新用法：ShowSpotlight(BuildButton, "你的提示文字", Right)
    ///
    /// 设计师在 DialogueDatabase 资产中遇到此命令需迁移到 ShowSpotlight 单一调用。
    /// </summary>
    [Obsolete("新系统中 spotlight 与 tooltip 不可分离。使用 ShowSpotlight(...) 单一命令并提供 text 参数")]
    public class SequencerCommandSpotlightTooltip : SequencerCommand
    {
        public void Awake()
        {
            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning(
                    "Sequencer: SpotlightTooltip(...) [DEPRECATED] - no-op。请迁移到 ShowSpotlight(targetId, text, position) 单一命令");
            }
            Stop();
        }
    }
}
