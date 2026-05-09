using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Runtime;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Tracks when a FocusCamera sequence should restore the camera.
    /// One active tracker is supported; newer schedules replace older ones.
    /// </summary>
    public class FocusCameraRestoreTracker : MonoBehaviour
    {
        private static FocusCameraRestoreTracker active;

        private CameraController controller;
        private CameraController.CameraSnapshot snapshot;
        private int counter;
        private float restoreDuration;
        private bool restored;
        private bool subscribed;

        public static void Schedule(
            CameraController cameraController,
            CameraController.CameraSnapshot cameraSnapshot,
            int restoreAfter,
            float restoreDuration)
        {
            if (cameraController == null) return;

            if (active != null)
            {
                active.Cancel();
                Destroy(active.gameObject);
            }

            var go = new GameObject("[FocusCameraRestoreTracker]");
            DontDestroyOnLoad(go);

            var tracker = go.AddComponent<FocusCameraRestoreTracker>();
            tracker.controller = cameraController;
            tracker.snapshot = cameraSnapshot;
            tracker.counter = Mathf.Max(0, restoreAfter);
            tracker.restoreDuration = restoreDuration;
            active = tracker;
            tracker.Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed || DialogueManager.instance == null) return;

            DialogueManager.instance.conversationLinePrepared += OnLinePrepared;
            DialogueManager.instance.conversationEnded += OnConversationEnded;
            subscribed = true;
        }

        private void OnLinePrepared(Subtitle subtitle)
        {
            if (restored) return;

            if (counter <= 0)
            {
                Restore();
            }
            else
            {
                counter--;
            }
        }

        private void OnConversationEnded(Transform actor)
        {
            Restore();
        }

        private void Restore()
        {
            if (restored) return;

            restored = true;
            Unsubscribe();

            if (controller != null)
            {
                controller.RestoreTo(snapshot, restoreDuration);
            }

            if (active == this)
            {
                active = null;
            }

            Destroy(gameObject);
        }

        private void Cancel()
        {
            if (restored) return;

            restored = true;
            Unsubscribe();

            if (active == this)
            {
                active = null;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (active == this)
            {
                active = null;
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;

            if (DialogueManager.hasInstance)
            {
                DialogueManager.instance.conversationLinePrepared -= OnLinePrepared;
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
            }

            subscribed = false;
        }
    }
}
