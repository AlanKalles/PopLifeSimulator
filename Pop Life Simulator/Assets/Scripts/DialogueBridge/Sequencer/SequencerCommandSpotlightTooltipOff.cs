using System;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// [DEPRECATED] 新系统中 spotlight 与 tooltip 不可分离，关闭 tooltip = 关闭 spotlight。
    /// 转发到 SpotlightOff() 行为。
    ///
    /// 设计师应改用 SpotlightOff()。
    /// </summary>
    [Obsolete("使用 SpotlightOff() 替代")]
    public class SequencerCommandSpotlightTooltipOff : SequencerCommand
    {
        public void Awake()
        {
            if (DialogueDebug.logWarnings)
            {
                Debug.LogWarning("Sequencer: SpotlightTooltipOff() [DEPRECATED] - 请改用 SpotlightOff()");
            }

            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.Hide(CloseReason.Manual);
            }

            Stop();
        }
    }
}
