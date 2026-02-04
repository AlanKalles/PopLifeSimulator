using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to show spotlight on a UI element
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   Spotlight(targetName)
    ///   Spotlight(targetName, shape)
    ///
    /// Parameters:
    ///   targetName: Name of the UI GameObject to highlight
    ///   shape (optional): Rectangle, Circle, or RoundedRectangle (default: RoundedRectangle)
    ///
    /// Examples:
    ///   Spotlight(BuildButton)
    ///   Spotlight(MoneyDisplay, Circle)
    /// </summary>
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

            // Parse shape
            SpotlightShape shape = SpotlightShape.RoundedRectangle;
            if (!string.IsNullOrEmpty(shapeStr))
            {
                System.Enum.TryParse(shapeStr, true, out shape);
            }

            // Show spotlight
            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.ShowSpotlightByName(targetName, shape);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: Spotlight({targetName}, {shape})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: Spotlight() - SpotlightManager.Instance is null");
            }

            // Command completes immediately (spotlight remains visible)
            Stop();
        }
    }
}
