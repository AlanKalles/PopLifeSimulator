using System;
using System.Collections.Generic;
using NodeCanvas.DialogueTrees;
using UnityEngine;

namespace Poplife.Dialogue
{
    public class DialogueManager : MonoBehaviour
    {
        [SerializeField] private GameObject dialogueUIPrefab;

        private List<DialogueEvent> dialogueEvents = new List<DialogueEvent>();
        private List<DialogueEvent> archivedEvents = new List<DialogueEvent>();

        private static Camera mainCamera;

        public void Start()
        {
            if (mainCamera == null) 
                mainCamera = Camera.main;

            AddEvent("D001", "Meet Midori", "V001", () => true, new List<string>());
            /*
            AddEvent("D002", "Build Tutorial", "V001", () => buildModeEntered, new List<string> { "B001", "B002" });
            AddEvent("D003", "Open Store", "V001", () => shelfCount >= 2, new List<string> { "C001", "C002", "C003" });
            AddEvent("D004", "First Customer", "C001", () => storeOpened, new List<string> { "fame +500" });
            AddEvent("D005", "Fame System", "V001", () => famePoints > 0, new List<string> { "R004", "R005" });
            AddEvent("D006", "First Request", "C001", () => dialogueCompleted.Contains("D005"), new List<string> { "R006" });
            AddEvent("D007", "Expand Store", "V002", () => shelfCount >= 4, new List<string> { "Expansion unlocked" });
            */
        }

        private void AddEvent(string code, string name, string npc, Func<bool> trigger, List<string> rewards)
        {
            var tree = Resources.Load<DialogueTree>($"DialogueTreeControllers/{code}");
            if (tree == null)
            {
                Debug.LogError($"Dialogue tree {code} not found in Resources!");
                return;
            }

            dialogueEvents.Add(new DialogueEvent(tree, dialogueUIPrefab, trigger, code, name, npc, rewards));
        }

        public void Update()
        {
            for (int i = dialogueEvents.Count - 1; i >= 0; i--)
            {
                var dialogueEvent = dialogueEvents[i];
                dialogueEvent.Update();

                if (!dialogueEvent.IsReadyToTrigger)
                {
                    archivedEvents.Add(dialogueEvent);
                    dialogueEvents.RemoveAt(i);
                }
            }
        }
    }
}