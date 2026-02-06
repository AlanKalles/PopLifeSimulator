using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to show tooltip alongside spotlight
    /// Automatically reads text from current dialogue node
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   SpotlightTooltip(position)
    ///   SpotlightTooltip(position, triggerMode)
    ///   SpotlightTooltip(position, customX, customY)
    ///   SpotlightTooltip(position, triggerMode, customX, customY)
    ///
    /// Parameters:
    ///   position: Auto | Left | Right | Top | Bottom | Custom
    ///   triggerMode (optional): ClickAnywhere | ClickSpotlight | ClickButton (default: ClickAnywhere)
    ///   customX (optional): X position as percentage of screen width (0-1), only used with Custom
    ///   customY (optional): Y position as percentage of screen height (0-1), only used with Custom
    ///
    /// Examples:
    ///   SpotlightTooltip(Right)                          // Default ClickAnywhere
    ///   SpotlightTooltip(Right, ClickSpotlight)          // Click spotlight area to continue
    ///   SpotlightTooltip(Right, ClickButton)             // Click target button to continue
    ///   SpotlightTooltip(Custom, 0.7, 0.5)               // Backward compatible
    ///   SpotlightTooltip(Custom, ClickSpotlight, 0.7, 0.5)  // Full format
    ///
    /// Note: Call Spotlight() or SpotlightNormalized() before this command
    ///       to ensure there's a spotlight to position the tooltip relative to.
    /// </summary>
    public class SequencerCommandSpotlightTooltip : SequencerCommand
    {
        public void Awake()
        {
            // Parse position parameter
            string positionStr = GetParameter(0, "Auto");
            string param1 = GetParameter(1);
            string param2 = GetParameter(2);
            string param3 = GetParameter(3);

            // Validate manager
            if (SpotlightManager.Instance == null)
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: SpotlightTooltip() - SpotlightManager.Instance is null");
                }
                Stop();
                return;
            }

            // Parse position enum
            TooltipPosition position = TooltipPosition.Auto;
            if (!string.IsNullOrEmpty(positionStr))
            {
                if (!System.Enum.TryParse(positionStr, true, out position))
                {
                    if (DialogueDebug.logWarnings)
                    {
                        Debug.LogWarning($"Sequencer: SpotlightTooltip() - Invalid position: {positionStr}. Using Auto.");
                    }
                    position = TooltipPosition.Auto;
                }
            }

            // Smart parameter parsing:
            // - If param1 is a ContinueTriggerMode enum → parse as triggerMode
            // - If param1 is a number → parse as customX (backward compatible)
            ContinueTriggerMode triggerMode = ContinueTriggerMode.ClickAnywhere;
            string customXStr = null;
            string customYStr = null;

            if (!string.IsNullOrEmpty(param1))
            {
                // Try to parse as ContinueTriggerMode
                if (System.Enum.TryParse(param1, true, out ContinueTriggerMode parsedTrigger))
                {
                    triggerMode = parsedTrigger;
                    // customX and customY are at param2 and param3
                    customXStr = param2;
                    customYStr = param3;
                }
                else
                {
                    // Backward compatible: param1 is customX
                    customXStr = param1;
                    customYStr = param2;
                }
            }

            // Parse custom offset if position is Custom
            Vector2? customOffset = null;
            if (position == TooltipPosition.Custom)
            {
                if (!string.IsNullOrEmpty(customXStr) && !string.IsNullOrEmpty(customYStr))
                {
                    if (float.TryParse(customXStr, out float x) && float.TryParse(customYStr, out float y))
                    {
                        customOffset = new Vector2(x, y);
                    }
                    else
                    {
                        if (DialogueDebug.logWarnings)
                        {
                            Debug.LogWarning($"Sequencer: SpotlightTooltip() - Invalid custom offset: ({customXStr}, {customYStr}). Using center.");
                        }
                        customOffset = new Vector2(0.5f, 0.5f);
                    }
                }
                else
                {
                    if (DialogueDebug.logWarnings)
                    {
                        Debug.LogWarning("Sequencer: SpotlightTooltip(Custom) - Missing customX and customY parameters. Using center.");
                    }
                    customOffset = new Vector2(0.5f, 0.5f);
                }
            }

            // Show tooltip
            SpotlightManager.Instance.ShowTooltipFromDialogue(position, customOffset, triggerMode);

            if (DialogueDebug.logInfo)
            {
                string offsetInfo = customOffset.HasValue ? $", offset: {customOffset.Value}" : "";
                Debug.Log($"Sequencer: SpotlightTooltip({position}, {triggerMode}{offsetInfo})");
            }

            // Command completes immediately
            Stop();
        }
    }
}
