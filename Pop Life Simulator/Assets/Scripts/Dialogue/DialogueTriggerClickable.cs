using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poplife.Dialogue
{
    public class DialogueTriggerClickable : MonoBehaviour
    {
        private DialogueEvent linkedEvent;

        public void Init(DialogueEvent dialogueEvent)
        {
            linkedEvent = dialogueEvent;
            /*
            var img = GetComponent<Image>();
            if (img != null)
            {
                img.alphaHitTestMinimumThreshold = 0.1f;
                Debug.Log("Alpha hit test threshold set.");
            }
            else
            {
                Debug.LogWarning("No Image found on DialogueTriggerClickable.");
            }
            */
        }
        /*
        public void OnPointerClick(PointerEventData pointerEventData)
        {
            //Output to console the clicked GameObject's name and the following message. You can replace this with your own actions for when clicking the GameObject.
            Debug.Log(name + " Dialogue UI Clicked!");
            if (linkedEvent != null)
            {
                linkedEvent.TryTrigger();
            }
        }
        */
    }
}