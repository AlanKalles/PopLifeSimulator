using System;
using System.Collections.Generic;
using NodeCanvas.DialogueTrees;
using UnityEngine;

namespace Poplife.Dialogue
{
    /// <summary>
    /// Represents a single dialogue event that can be triggered
    /// 代表一个可触发的对话事件
    /// </summary>
    public class DialogueEvent
    {
        private DialogueTree dialogueAsset;
        private bool isTriggered = false;
        private bool isCompleted = false;

        private string dialogueCode;
        private string dialogueName;
        private List<string> rewards;

        private DialogueTreeController controllerInstance; // runtime controller

        // Events
        public event Action<DialogueEvent> OnDialogueCompleted;

        /// <summary>
        /// Constructor for dialogue event (simplified for marker-based triggering)
        /// </summary>
        public DialogueEvent(
            DialogueTree dialogueAsset,
            string dialogueCode,
            string dialogueName,
            List<string> rewards)
        {
            this.dialogueAsset = dialogueAsset;
            this.dialogueCode = dialogueCode;
            this.dialogueName = dialogueName;
            this.rewards = rewards;
        }

        /// <summary>
        /// Trigger the dialogue (called by marker system)
        /// 触发对话（由标记系统调用）
        /// </summary>
        public void ForceTrigger()
        {
            if (isTriggered)
            {
                Debug.LogWarning($"[DialogueEvent] Dialogue {dialogueCode} already triggered");
                return;
            }

            Debug.Log($"[DialogueEvent] Triggering dialogue: {dialogueCode}");

            if (controllerInstance == null)
            {
                // Create runtime GameObject to host DialogueTreeController
                GameObject dialogueObj = new GameObject($"Dialogue_{dialogueCode}");

                // Add the controller
                controllerInstance = dialogueObj.AddComponent<DialogueTreeController>();

                // Assign the DialogueTree asset
                controllerInstance.behaviour = dialogueAsset;

                // TODO: Optionally load and assign Actor parameters here
                // var midoriActor = Resources.Load("DialogueActors/Midori") as IDialogueActor;
                // if (midoriActor != null) {
                //     controllerInstance.SetActorReference("Midori", midoriActor);
                // }
            }

            // Start the dialogue with callback
            controllerInstance.StartDialogue((success) => OnDialogueFinished(success));
            isTriggered = true;
        }

        private void OnDialogueFinished(bool success)
        {
            Debug.Log($"[DialogueEvent] Dialogue {dialogueCode} finished! Success: {success}");
            isCompleted = true;

            // Notify listeners
            OnDialogueCompleted?.Invoke(this);

            // Clean up controller
            if (controllerInstance != null)
            {
                GameObject.Destroy(controllerInstance.gameObject);
                controllerInstance = null;
            }
        }

        // Public getters
        public bool IsTriggered => isTriggered;
        public bool IsCompleted => isCompleted;
        public string GetDialogueCode() => dialogueCode;
        public string GetDialogueName() => dialogueName;
        public List<string> GetRewards() => rewards;
    }
}
