using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to raise a tutorial marker
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   RaiseMarker(markerName)
    ///
    /// Parameters:
    ///   markerName: Name of the TutorialMarker enum value
    ///
    /// Examples:
    ///   RaiseMarker(FirstShelfPlaced)
    ///   RaiseMarker(StoreOpened)
    /// </summary>
    public class SequencerCommandRaiseMarker : SequencerCommand
    {
        public void Awake()
        {
            string markerName = GetParameter(0);

            if (string.IsNullOrEmpty(markerName))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: RaiseMarker() - missing marker name parameter");
                }
                Stop();
                return;
            }

            if (PopLifeLuaFunctions.Instance != null)
            {
                PopLifeLuaFunctions.Instance.RaiseTutorialMarker(markerName);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: RaiseMarker({markerName})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: RaiseMarker() - PopLifeLuaFunctions.Instance is null");
            }

            Stop();
        }
    }
}
