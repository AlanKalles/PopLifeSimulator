using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to show spotlight using normalized screen coordinates (0-1)
    /// This is resolution-independent and easier to use than pixel coordinates
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   SpotlightNormalized(x, y, width, height)
    ///   SpotlightNormalized(x, y, width, height, shape)
    ///
    /// Parameters:
    ///   x: X position as percentage of screen width (0-1, where 0=left, 1=right)
    ///   y: Y position as percentage of screen height (0-1, where 0=bottom, 1=top)
    ///   width: Width as percentage of screen width (0-1)
    ///   height: Height as percentage of screen height (0-1)
    ///   shape (optional): Rectangle, Circle, or RoundedRectangle (default: RoundedRectangle)
    ///
    /// Examples:
    ///   SpotlightNormalized(0.4, 0.4, 0.2, 0.2)           // Center of screen, 20% size
    ///   SpotlightNormalized(0, 0.8, 0.3, 0.15)            // Top-left area
    ///   SpotlightNormalized(0.35, 0.35, 0.3, 0.3, Circle) // Center circle
    ///
    /// Common positions:
    ///   Center:       x=0.4, y=0.4, w=0.2, h=0.2
    ///   Top-left:     x=0, y=0.8
    ///   Top-right:    x=0.7, y=0.8
    ///   Bottom-left:  x=0, y=0
    ///   Bottom-right: x=0.7, y=0
    /// </summary>
    public class SequencerCommandSpotlightNormalized : SequencerCommand
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
                    Debug.LogWarning("Sequencer: SpotlightNormalized() - missing parameters. Usage: SpotlightNormalized(x, y, width, height[, shape])");
                }
                Stop();
                return;
            }

            // Parse numeric values
            if (!float.TryParse(xStr, out float normalizedX) ||
                !float.TryParse(yStr, out float normalizedY) ||
                !float.TryParse(widthStr, out float normalizedWidth) ||
                !float.TryParse(heightStr, out float normalizedHeight))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning($"Sequencer: SpotlightNormalized() - invalid numeric parameters: ({xStr}, {yStr}, {widthStr}, {heightStr})");
                }
                Stop();
                return;
            }

            // Convert normalized coordinates to screen pixels
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            float x = normalizedX * screenWidth;
            float y = normalizedY * screenHeight;
            float width = normalizedWidth * screenWidth;
            float height = normalizedHeight * screenHeight;

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
                    Debug.Log($"Sequencer: SpotlightNormalized({normalizedX}, {normalizedY}, {normalizedWidth}, {normalizedHeight}, {shape}) -> Rect({x}, {y}, {width}, {height})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: SpotlightNormalized() - SpotlightManager.Instance is null");
            }

            // Command completes immediately (spotlight remains visible)
            Stop();
        }
    }
}
