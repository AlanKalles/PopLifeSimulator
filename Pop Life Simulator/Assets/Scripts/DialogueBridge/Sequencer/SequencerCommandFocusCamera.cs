using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;
using PopLife.Runtime;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Smoothly focuses the main camera on a world object, optionally hiding Dialogue UI
    /// until the next subtitle line that contains visible text.
    ///
    /// Usage:
    /// FocusCamera(targetName, [duration=0.6], [orthoSize=0], [restoreAfter=0], [showUI=false])
    /// </summary>
    public class SequencerCommandFocusCamera : SequencerCommand
    {
        public void Awake()
        {
            string targetName = GetParameter(0);
            float duration = GetParameterAsFloat(1, 0.6f);
            float orthoSize = GetParameterAsFloat(2, 0f);
            int restoreAfter = GetParameterAsInt(3, 0);
            bool showUI = string.Equals(
                GetParameter(4, "false"),
                "true",
                System.StringComparison.OrdinalIgnoreCase);

            var runner = DialogueManager.instance;
            if (runner != null)
            {
                runner.StartCoroutine(Run(targetName, duration, orthoSize, restoreAfter, showUI));
            }
            else
            {
                Debug.LogWarning("Sequencer: FocusCamera - DialogueManager.instance is null");
            }

            Stop();
        }

        private static IEnumerator Run(string targetName, float duration, float orthoSize, int restoreAfter, bool showUI)
        {
            int uiToken = 0;
            if (!showUI)
            {
                uiToken = DialogueUIVisibility.Hide();
                StartRestoreDialogueUIWhenNextTextLinePrepared(uiToken);
            }

            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("Sequencer: FocusCamera - missing targetName parameter");
                DialogueUIVisibility.Show(uiToken);
                yield break;
            }

            var go = GameObject.Find(targetName);
            if (go == null)
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning($"Sequencer: FocusCamera - 找不到 GameObject '{targetName}'");
                }
                DialogueUIVisibility.Show(uiToken);
                yield break;
            }

            var controller = Object.FindAnyObjectByType<CameraController>();
            if (controller == null)
            {
                Debug.LogWarning("Sequencer: FocusCamera - 找不到 CameraController");
                DialogueUIVisibility.Show(uiToken);
                yield break;
            }

            var snapshot = controller.Snapshot();
            float? size = orthoSize > 0f ? orthoSize : (float?)null;
            FocusCameraRestoreTracker.Schedule(
                controller,
                snapshot,
                restoreAfter,
                restoreDuration: 0.6f);

            yield return controller.FocusOn(go.transform, duration, size);
        }

        private static void StartRestoreDialogueUIWhenNextTextLinePrepared(int uiToken)
        {
            if (uiToken <= 0) return;

            var runner = DialogueManager.instance;
            if (runner == null)
            {
                DialogueUIVisibility.Show(uiToken);
                return;
            }

            runner.StartCoroutine(RestoreDialogueUIWhenNextTextLinePrepared(uiToken));
        }

        private static IEnumerator RestoreDialogueUIWhenNextTextLinePrepared(int uiToken)
        {
            var manager = DialogueManager.instance;
            if (manager == null)
            {
                DialogueUIVisibility.Show(uiToken);
                yield break;
            }

            bool shouldRestore = false;
            bool conversationEnded = false;
            manager.conversationLinePrepared += OnConversationLinePrepared;
            manager.conversationEnded += OnConversationEnded;

            while (!shouldRestore && !conversationEnded)
            {
                DialogueUIVisibility.KeepHidden(uiToken);
                yield return null;
            }

            if (DialogueManager.hasInstance)
            {
                DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
            }

            DialogueUIVisibility.Show(uiToken);

            void OnConversationLinePrepared(Subtitle subtitle)
            {
                string text = subtitle?.formattedText?.text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    shouldRestore = true;
                }
            }

            void OnConversationEnded(Transform actor)
            {
                conversationEnded = true;
            }
        }
    }
}
