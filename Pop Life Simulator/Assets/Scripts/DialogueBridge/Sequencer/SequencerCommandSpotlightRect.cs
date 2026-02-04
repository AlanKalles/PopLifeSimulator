using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to show spotlight on a specific screen rect
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   SpotlightRect(x, y, width, height)
    ///   SpotlightRect(x, y, width, height, shape)
    ///
    /// Parameters:
    ///   x: X position in screen coordinates (pixels from left)
    ///   y: Y position in screen coordinates (pixels from bottom)
    ///   width: Width in pixels
    ///   height: Height in pixels
    ///   shape (optional): Rectangle, Circle, or RoundedRectangle (default: RoundedRectangle)
    ///
    /// Examples:
    ///   SpotlightRect(100, 200, 300, 150)
    ///   SpotlightRect(100, 200, 300, 150, Circle)
    ///
    /// Notes:
    ///   - Screen coordinates: (0,0) is bottom-left corner
    ///   - Use Screen.width and Screen.height as reference
    ///   - For center of 1920x1080 screen: SpotlightRect(760, 440, 400, 200)
    /// </summary>
    public class SequencerCommandSpotlightRect : SequencerCommand
    {
        public void Awake()
        {
            // Parse parameters
            string xStr = GetParameter(0);
            string yStr = GetParameter(1);
            string widthStr = GetParameter(2);
            string heightStr = GetParameter(3);
            string shapeStr = GetParameter(4, "RoundedRectangle");

            // Validate required parameters
            if (string.IsNullOrEmpty(xStr) || string.IsNullOrEmpty(yStr) ||
                string.IsNullOrEmpty(widthStr) || string.IsNullOrEmpty(heightStr))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: SpotlightRect() - missing parameters. Usage: SpotlightRect(x, y, width, height[, shape])");
                }
                Stop();
                return;
            }

            // Parse numeric values
            if (!float.TryParse(xStr, out float x) ||
                !float.TryParse(yStr, out float y) ||
                !float.TryParse(widthStr, out float width) ||
                !float.TryParse(heightStr, out float height))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning($"Sequencer: SpotlightRect() - invalid numeric parameters: ({xStr}, {yStr}, {widthStr}, {heightStr})");
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
                SpotlightManager.Instance.ShowSpotlightRect(x, y, width, height, shape);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: SpotlightRect({x}, {y}, {width}, {height}, {shape})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: SpotlightRect() - SpotlightManager.Instance is null");
            }

            // Command completes immediately (spotlight remains visible)
            Stop();
        }
    }
}
