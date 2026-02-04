using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to hide the spotlight
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   SpotlightOff()
    ///
    /// Example:
    ///   Spotlight(BuildButton); Delay(2); SpotlightOff()
    /// </summary>
    public class SequencerCommandSpotlightOff : SequencerCommand
    {
        public void Awake()
        {
            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.HideSpotlight();

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
