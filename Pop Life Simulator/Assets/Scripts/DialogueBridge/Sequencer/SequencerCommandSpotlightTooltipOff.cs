using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to hide the spotlight tooltip
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   SpotlightTooltipOff()
    ///
    /// Note: This will also restore the dialogue UI if it was hidden by SpotlightTooltip
    /// </summary>
    public class SequencerCommandSpotlightTooltipOff : SequencerCommand
    {
        public void Awake()
        {
            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.HideTooltip();

                if (DialogueDebug.logInfo)
                {
                    Debug.Log("Sequencer: SpotlightTooltipOff()");
                }
            }
            else
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: SpotlightTooltipOff() - SpotlightManager.Instance is null");
                }
            }

            // Command completes immediately
            Stop();
        }
    }
}
