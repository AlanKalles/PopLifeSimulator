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
    ///   SpotlightTooltip(position, triggerMode, offsetX, offsetY)
    ///   SpotlightTooltip(position, customX, customY)
    ///   SpotlightTooltip(position, triggerMode, customX, customY)
    ///
    /// Parameters:
    ///   position: Auto | Left | Right | Top | Bottom | Custom
    ///   triggerMode (optional): ClickAnywhere | ClickSpotlight | ClickButton (default: ClickAnywhere)
    ///   offsetX/offsetY (optional): ClickButton 模式下 tooltip 额外偏移（像素），默认 55
    ///   customX/customY (optional): Custom 模式下屏幕百分比坐标 (0-1)
    ///
    /// Examples:
    ///   SpotlightTooltip(Right)                              // Default ClickAnywhere
    ///   SpotlightTooltip(Right, ClickSpotlight)              // Click spotlight area to continue
    ///   SpotlightTooltip(Auto, ClickButton)                  // ClickButton, default offset 55,55
    ///   SpotlightTooltip(Auto, ClickButton, 5, 5)            // ClickButton, custom offset
    ///   SpotlightTooltip(Custom, 0.7, 0.5)                   // Backward compatible
    ///   SpotlightTooltip(Custom, ClickSpotlight, 0.7, 0.5)   // Full format
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
            string numXStr = null;
            string numYStr = null;

            if (!string.IsNullOrEmpty(param1))
            {
                // Try to parse as ContinueTriggerMode
                if (System.Enum.TryParse(param1, true, out ContinueTriggerMode parsedTrigger))
                {
                    triggerMode = parsedTrigger;
                    numXStr = param2;
                    numYStr = param3;
                }
                else
                {
                    // Backward compatible: param1 is customX
                    numXStr = param1;
                    numYStr = param2;
                }
            }

            // Parse offsets based on mode
            Vector2? customOffset = null;
            Vector2? clickButtonOffset = null;

            if (position == TooltipPosition.Custom)
            {
                // Custom 模式：numX/numY 作为屏幕百分比坐标 (0-1)
                if (!string.IsNullOrEmpty(numXStr) && !string.IsNullOrEmpty(numYStr))
                {
                    if (float.TryParse(numXStr, out float x) && float.TryParse(numYStr, out float y))
                    {
                        customOffset = new Vector2(x, y);
                    }
                    else
                    {
                        if (DialogueDebug.logWarnings)
                        {
                            Debug.LogWarning($"Sequencer: SpotlightTooltip() - Invalid custom offset: ({numXStr}, {numYStr}). Using center.");
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
            else if (triggerMode == ContinueTriggerMode.ClickButton)
            {
                // ClickButton 模式：numX/numY 作为像素偏移，默认 55
                float ox = 55f, oy = 55f;
                if (!string.IsNullOrEmpty(numXStr) && float.TryParse(numXStr, out float parsedX))
                    ox = parsedX;
                if (!string.IsNullOrEmpty(numYStr) && float.TryParse(numYStr, out float parsedY))
                    oy = parsedY;
                clickButtonOffset = new Vector2(ox, oy);
            }

            // Show tooltip
            SpotlightManager.Instance.ShowTooltipFromDialogue(position, customOffset, triggerMode, clickButtonOffset);

            if (DialogueDebug.logInfo)
            {
                string offsetInfo = customOffset.HasValue ? $", customOffset: {customOffset.Value}" : "";
                if (clickButtonOffset.HasValue)
                    offsetInfo += $", clickBtnOffset: {clickButtonOffset.Value}";
                Debug.Log($"Sequencer: SpotlightTooltip({position}, {triggerMode}{offsetInfo})");
            }

            // Command completes immediately
            Stop();
        }
    }
}
