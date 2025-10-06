using UnityEngine;

namespace Poplife.Dialogue
{
    public class DialogueTriggerClickable : MonoBehaviour
    {
        private DialogueEvent linkedEvent;

        public void Init(DialogueEvent dialogueEvent)
        {
            linkedEvent = dialogueEvent;
            
        }

        private void OnMouseDown()
        {
            if (linkedEvent != null)
            {
                linkedEvent.TryTrigger();
            }
        }
    }
}