using System;
using System.Collections.Generic;
using NodeCanvas.DialogueTrees;
using PopLife.Customers.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Poplife.Dialogue
{
    public class DialogueEvent
    {
        private DialogueTree dialogueAsset;
        private bool isTriggered = false;

        private Func<bool> triggerCondition;
        private string dialogueCode;
        private string dialogueName;
        private string npcCode;
        private List<string> rewards;

        private GameObject uiButtonInstance;
        private GameObject dialogueUIPrefab;
        private Transform uiParent;

        private DialogueTreeController controllerInstance; // runtime controller

        public DialogueEvent(
            DialogueTree dialogueAsset,
            GameObject dialogueUIPrefab,
            Func<bool> triggerCondition,
            string dialogueCode,
            string dialogueName,
            string npcCode,
            List<string> rewards)
        {
            this.dialogueAsset = dialogueAsset;
            this.dialogueUIPrefab = dialogueUIPrefab;
            this.triggerCondition = triggerCondition;
            this.dialogueCode = dialogueCode;
            this.dialogueName = dialogueName;
            this.npcCode = npcCode;
            this.rewards = rewards;
        }

        public void Update()
        {
            if (!isTriggered && triggerCondition.Invoke() && uiButtonInstance == null)
            {
                CreateDialogueUI();
            }
        }

        private void CreateDialogueUI()
        {
            var npc = FindNPCByCode(npcCode);
            if (npc == null)
            {
                Debug.LogError($"NPC with code {npcCode} not found!");
            }

            var worldPos = npc.transform.position + Vector3.up * 2f;
            uiButtonInstance = GameObject.Instantiate(dialogueUIPrefab);
            GameObject tmpUI = uiButtonInstance.GetComponentInChildren<DialogueTriggerClickable>().gameObject;
            tmpUI.transform.position = worldPos;
            

            // ✅ Canvas 的 World Camera
            var canvas = uiButtonInstance.GetComponentInChildren<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.worldCamera = Camera.main;
            }

            /*
            // ✅ Add Collider if missing
            if (uiButtonInstance.GetComponent<Collider>() == null)
            {
                uiButtonInstance.AddComponent<BoxCollider>();
            }
            */

            // ✅ Add Button Listener
            Button buttonComponent = uiButtonInstance.GetComponentInChildren<Button>();

            if (buttonComponent != null)
            {
                buttonComponent.onClick.AddListener(TryTrigger);
                Debug.Log($"Button found and listener added for {dialogueCode}");
            }
            else
            {
                Debug.LogError("Button component not found!");
            }
        }


        public void TryTrigger()
        {
            if (isTriggered)
                return;

            Debug.Log("TryTrigger called!");

            if (controllerInstance == null)
            {
                // Create runtime GameObject to host DialogueTreeController
                GameObject dialogueObj = new GameObject($"Dialogue_{dialogueCode}");

                // Add the controller
                controllerInstance = dialogueObj.AddComponent<DialogueTreeController>();

                // Assign the DialogueTree asset
                controllerInstance.behaviour = dialogueAsset;

                // Optionally: Assign blackboard, actor parameters etc.
                // controllerInstance.blackboard = ...
            }

            // Start the dialogue
            controllerInstance.StartDialogue();
            isTriggered = true;

            // Clean up the UI
            if (uiButtonInstance != null)
                GameObject.Destroy(uiButtonInstance);

            uiButtonInstance = null;
        }


        public bool IsReadyToTrigger => uiButtonInstance != null && !isTriggered;
        public string GetNPCCode() => npcCode;

        private GameObject FindNPCByCode(string code)//这个method有点狗屎，之后可以改成spawn一个对应的npc在门口，而不是找到对应的npc
        {
            var allAdapters = GameObject.FindObjectsOfType<CustomerBlackboardAdapter>();
            foreach (var adapter in allAdapters)
            {
                if (adapter.customerId == code)
                    return adapter.gameObject;
            }
            return null;
        }
    }
}
