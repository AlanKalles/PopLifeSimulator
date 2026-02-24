using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.Manager;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to show an operation guide
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   ShowGuide(guideId)
    ///
    /// Parameters:
    ///   guideId: The ID of the OperationGuideData asset (matches asset filename)
    ///
    /// Examples:
    ///   ShowGuide(Guide_BuildingBasics)
    ///   ShowGuide(Guide_CustomerSystem)
    /// </summary>
    public class SequencerCommandShowGuide : SequencerCommand
    {
        public void Awake()
        {
            string guideId = GetParameter(0);

            if (string.IsNullOrEmpty(guideId))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: ShowGuide() - missing guideId parameter");
                }
                Stop();
                return;
            }

            if (OperationGuideManager.Instance != null)
            {
                OperationGuideManager.Instance.ShowGuide(guideId);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: ShowGuide({guideId})");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: ShowGuide() - OperationGuideManager.Instance is null");
            }

            // 命令立即完成
            Stop();
        }
    }
}
