using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Data;
using PopLife.Manager;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Bridges TutorialMarker events with Dialogue System conversations and Lua scripts.
    /// Guide triggers are handled by OperationGuideManager.
    /// Quest activation is handled by QuestLogicManager.
    /// </summary>
    public class TutorialMarkerBridge : MonoBehaviour
    {
        private const string DialogueActionResourcesPath = "ScriptableObjects/DialogueActions";

        public static TutorialMarkerBridge Instance { get; private set; }

        [Title("Dialogue Actions")]
        [InfoBox("Configure DialogueAction assets under Resources/ScriptableObjects/DialogueActions. TutorialMarkerBridge will auto-load them at runtime.")]
        [SerializeField]
        [ReadOnly]
        private int loadedActionCount;

        [Title("Debug")]
        [SerializeField]
        private bool debugMode = true;

        private DialogueAction[] allActions = Array.Empty<DialogueAction>();
        private Dictionary<TutorialMarker, List<DialogueAction>> markerLookup = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadDialogueActions();
                BuildLookupTables();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            TutorialEventBus.OnMarkerTriggered += HandleMarkerTriggered;
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= HandleMarkerTriggered;
        }

        private void LoadDialogueActions()
        {
            allActions = Resources.LoadAll<DialogueAction>(DialogueActionResourcesPath) ?? Array.Empty<DialogueAction>();
            loadedActionCount = allActions.Length;
        }

        private void BuildLookupTables()
        {
            markerLookup.Clear();

            foreach (var action in allActions)
            {
                if (action == null || action.ActivationMarker == TutorialMarker.None)
                    continue;

                if (!markerLookup.TryGetValue(action.ActivationMarker, out var actions))
                {
                    actions = new List<DialogueAction>();
                    markerLookup[action.ActivationMarker] = actions;
                }

                actions.Add(action);
            }

            foreach (var pair in markerLookup)
            {
                pair.Value.Sort(CompareActions);

                for (int i = 1; i < pair.Value.Count; i++)
                {
                    var previous = pair.Value[i - 1];
                    var current = pair.Value[i];
                    if (previous.ExecutionOrder == current.ExecutionOrder)
                    {
                        Debug.LogWarning(
                            $"[TutorialMarkerBridge] Duplicate executionOrder {current.ExecutionOrder} on marker {pair.Key}: '{previous.name}' and '{current.name}'.");
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log(
                    $"[TutorialMarkerBridge] Loaded {allActions.Length} DialogueAction assets. " +
                    $"SO markers: {markerLookup.Count}.");
            }
        }

        private static int CompareActions(DialogueAction left, DialogueAction right)
        {
            int orderCompare = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            if (orderCompare != 0)
                return orderCompare;

            return string.Compare(left.name, right.name, StringComparison.Ordinal);
        }

        private void HandleMarkerTriggered(TutorialMarker marker)
        {
            if (!markerLookup.TryGetValue(marker, out var actions) || actions.Count == 0)
            {
                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] No mapping found for marker: {marker}");
                }
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[TutorialMarkerBridge] Processing marker: {marker}");
            }

            foreach (var action in actions)
            {
                ExecuteAction(action);
            }
        }

        private void ExecuteAction(DialogueAction action)
        {
            ExecuteLuaAction(action.LuaScript);
            ExecuteConversationAction(action.ConversationToStart, action.ConversationDelay);
        }

        private void ExecuteLuaAction(string luaScript)
        {
            if (string.IsNullOrEmpty(luaScript))
                return;

            try
            {
                Lua.Run(luaScript);
                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] Executed Lua: {luaScript}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TutorialMarkerBridge] Failed to execute Lua: {e.Message}");
            }
        }

        private void ExecuteConversationAction(string conversationToStart, float conversationDelay)
        {
            if (string.IsNullOrEmpty(conversationToStart))
                return;

            if (DialogueManager.isConversationActive)
            {
                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] Conversation already active, queuing: {conversationToStart}");
                }

                DialogueManager.instance.StartCoroutine(StartConversationWhenReady(conversationToStart));
                return;
            }

            if (conversationDelay > 0f)
            {
                DialogueManager.instance.StartCoroutine(StartConversationDelayed(conversationToStart, conversationDelay));
            }
            else
            {
                StartConversation(conversationToStart);
            }
        }

        private System.Collections.IEnumerator StartConversationDelayed(string conversationTitle, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (!DialogueManager.isConversationActive)
            {
                StartConversation(conversationTitle);
            }
            else
            {
                yield return StartConversationWhenReady(conversationTitle);
            }
        }

        private System.Collections.IEnumerator StartConversationWhenReady(string conversationTitle)
        {
            while (DialogueManager.isConversationActive)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.1f);
            StartConversation(conversationTitle);
        }

        private void StartConversation(string conversationTitle)
        {
            if (DialogueManager.ConversationHasValidEntry(conversationTitle))
            {
                DialogueManager.StartConversation(conversationTitle);
                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] Started conversation: {conversationTitle}");
                }
            }
            else
            {
                Debug.LogWarning($"[TutorialMarkerBridge] Conversation not found or has no valid entry: {conversationTitle}");
            }
        }
    }
}
