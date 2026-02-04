using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to show spotlight on a world object (3D/2D)
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   SpotlightWorld(targetName)
    ///   SpotlightWorld(targetName, shape)
    ///
    /// Parameters:
    ///   targetName: Name of the world GameObject to highlight
    ///   shape (optional): Rectangle, Circle, or RoundedRectangle (default: RoundedRectangle)
    ///
    /// Examples:
    ///   SpotlightWorld(Shelf_Lingerie)
    ///   SpotlightWorld(Cashier, Circle)
    /// </summary>
    public class SequencerCommandSpotlightWorld : SequencerCommand
    {
        public void Awake()
        {
            string targetName = GetParameter(0);
            string shapeStr = GetParameter(1, "RoundedRectangle");

            if (string.IsNullOrEmpty(targetName))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: SpotlightWorld() - missing target name parameter");
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

            // Show spotlight on world object
            if (SpotlightManager.Instance != null)
            {
                SpotlightManager.Instance.ShowSpotlightOnWorldObjectByName(targetName, shape);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: SpotlightWorld({targetName}, {shape})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: SpotlightWorld() - SpotlightManager.Instance is null");
            }

            // Command completes immediately (spotlight remains visible)
            Stop();
        }
    }
}
