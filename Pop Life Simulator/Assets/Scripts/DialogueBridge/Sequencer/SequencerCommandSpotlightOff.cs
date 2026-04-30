using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 关闭 spotlight + tooltip。在新系统中仍是合法、推荐的命令（语义不变）。
    ///
    /// 用法：SpotlightOff()
    /// 等同于 Lua: HideSpotlight()
    /// </summary>
    public class SequencerCommandSpotlightOff : SequencerCommand
    {
        public void Awake()
        {
            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.Hide(CloseReason.Manual);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log("Sequencer: SpotlightOff()");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: SpotlightOff() - SpotlightManager.Instance is null");
            }

            Stop();
        }
    }
}
