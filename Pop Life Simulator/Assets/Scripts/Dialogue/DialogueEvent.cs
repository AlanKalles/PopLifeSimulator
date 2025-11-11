using System;
using System.Collections.Generic;
using NodeCanvas.DialogueTrees;
using NodeCanvas.Framework;
using UnityEngine;

namespace Poplife.Dialogue
{
    /// <summary>
    /// UI configuration for dialogue display
    /// 对话显示的UI配置
    /// </summary>
    [System.Serializable]
    public class DialogueUIConfig
    {
        public string uiType = "Default";              // UI类型（用于筛选UI）
        public Vector2 panelPosition = Vector2.zero;   // 对话框位置（屏幕坐标偏移）
        public Vector2 panelAnchor = new Vector2(0.5f, 0.5f); // 锚点（0-1）
        public float panelScale = 1.0f;                // 对话框缩放

        public DialogueUIConfig(string uiType = "Default")
        {
            this.uiType = uiType;
        }

        public DialogueUIConfig(string uiType, Vector2 position, Vector2 anchor, float scale = 1.0f)
        {
            this.uiType = uiType;
            this.panelPosition = position;
            this.panelAnchor = anchor;
            this.panelScale = scale;
        }
    }

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
        private DialogueUIConfig uiConfig; // UI configuration for this dialogue
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
            List<string> rewards,
            DialogueUIConfig uiConfig = null)
        {
            this.dialogueAsset = dialogueAsset;
            this.dialogueCode = dialogueCode;
            this.dialogueName = dialogueName;
            this.rewards = rewards;
            this.uiConfig = uiConfig ?? new DialogueUIConfig();
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

            Debug.Log($"[DialogueEvent] Triggering dialogue: {dialogueCode} with UI type: {uiConfig.uiType}");

            if (controllerInstance == null)
            {
                // Create runtime GameObject to host DialogueTreeController
                GameObject dialogueObj = new GameObject($"Dialogue_{dialogueCode}");

                // Add the controller
                controllerInstance = dialogueObj.AddComponent<DialogueTreeController>();

                // Assign the DialogueTree asset
                controllerInstance.behaviour = dialogueAsset;

                // Create Blackboard and set UI configuration
                var blackboard = dialogueObj.AddComponent<Blackboard>();
                blackboard.SetVariableValue("uiType", uiConfig.uiType);
                blackboard.SetVariableValue("dialogueCode", dialogueCode);
                blackboard.SetVariableValue("panelPosition", uiConfig.panelPosition);
                blackboard.SetVariableValue("panelAnchor", uiConfig.panelAnchor);
                blackboard.SetVariableValue("panelScale", uiConfig.panelScale);
                controllerInstance.blackboard = blackboard;

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
        public DialogueUIConfig GetUIConfig() => uiConfig;
        public List<string> GetRewards() => rewards;
    }
}
