using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to manually unfreeze a UI panel (or all panels).
    /// Normally not needed since FreezeUI auto-restores on node change.
    ///
    /// Usage:
    ///   UnfreezeUI(panelName)   - unfreeze specific panel
    ///   UnfreezeUI()            - unfreeze all panels
    ///
    /// Examples:
    ///   UnfreezeUI(ShelfListPanelRoot)
    ///   UnfreezeUI()
    /// </summary>
    public class SequencerCommandUnfreezeUI : SequencerCommand
    {
        public void Awake()
        {
            string targetName = GetParameter(0);

            if (UIFreezeManager.Instance != null)
            {
                if (string.IsNullOrEmpty(targetName))
                {
                    UIFreezeManager.Instance.UnfreezeAll();

                    if (DialogueDebug.logInfo)
                    {
                        Debug.Log("Sequencer: UnfreezeUI() - all panels");
                    }
                }
                else
                {
                    UIFreezeManager.Instance.Unfreeze(targetName);

                    if (DialogueDebug.logInfo)
                    {
                        Debug.Log($"Sequencer: UnfreezeUI({targetName})");
                    }
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: UnfreezeUI() - UIFreezeManager.Instance is null");
            }

            Stop();
        }
    }
}
