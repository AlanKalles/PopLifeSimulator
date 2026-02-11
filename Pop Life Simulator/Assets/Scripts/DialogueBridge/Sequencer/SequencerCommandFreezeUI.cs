using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to temporarily freeze a UI panel's interactability.
    /// Auto-restores on next dialogue node or conversation end.
    ///
    /// Usage:
    ///   FreezeUI(panelName)
    ///
    /// Examples:
    ///   FreezeUI(ShelfListPanelRoot)
    ///   FreezeUI(BuildButton)
    ///
    /// Combine with Spotlight:
    ///   Spotlight(ShelfListPanelRoot); SpotlightTooltip(Auto); FreezeUI(ShelfListPanelRoot)
    /// </summary>
    public class SequencerCommandFreezeUI : SequencerCommand
    {
        public void Awake()
        {
            string targetName = GetParameter(0);

            if (string.IsNullOrEmpty(targetName))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: FreezeUI() - missing panel name parameter");
                }
                Stop();
                return;
            }

            if (UIFreezeManager.Instance != null)
            {
                UIFreezeManager.Instance.Freeze(targetName);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: FreezeUI({targetName})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: FreezeUI() - UIFreezeManager.Instance is null");
            }

            Stop();
        }
    }
}
