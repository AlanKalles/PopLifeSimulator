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
    /// Bridges TutorialMarker events with Dialogue System Quest system
    /// When a TutorialMarker is triggered, this component can:
    /// - Update a corresponding Quest entry state
    /// - Start a conversation
    /// - Set Lua variables
    /// </summary>
    public class TutorialMarkerBridge : MonoBehaviour
    {
        #region Singleton

        public static TutorialMarkerBridge Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildMappingDictionary();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Serialized Fields

        [Title("Marker to Quest Mappings")]
        [InfoBox("Configure how TutorialMarkers map to Dialogue System Quests and Conversations")]
        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true)]
        private List<MarkerQuestMapping> mappings = new List<MarkerQuestMapping>();

        [Title("Debug")]
        [SerializeField]
        private bool debugMode = true;

        #endregion

        #region Private Fields

        private Dictionary<TutorialMarker, MarkerQuestMapping> mappingDictionary;

        #endregion

        #region Data Classes

        /// <summary>
        /// Mapping between TutorialMarker and Dialogue System Quest/Conversation
        /// </summary>
        [Serializable]
        public class MarkerQuestMapping
        {
            [Title("Trigger")]
            [EnumToggleButtons]
            public TutorialMarker marker;

            [Title("Quest Actions")]
            [Tooltip("Quest name in Dialogue Database (leave empty to skip quest update)")]
            public string questName;

            [Tooltip("Quest entry number (0 = main quest state, 1+ = entry state)")]
            [ShowIf("@!string.IsNullOrEmpty(questName)")]
            public int questEntryNumber = 0;

            [Tooltip("Target state for the quest/entry")]
            [ShowIf("@!string.IsNullOrEmpty(questName)")]
            [ValueDropdown("GetQuestStates")]
            public string targetState = "success";

            [Title("Conversation Actions")]
            [Tooltip("Conversation to start (leave empty to skip)")]
            public string conversationToStart;

            [Tooltip("Delay before starting conversation (seconds)")]
            [ShowIf("@!string.IsNullOrEmpty(conversationToStart)")]
            [Range(0f, 5f)]
            public float conversationDelay = 0f;

            [Title("Lua Actions")]
            [Tooltip("Custom Lua code to execute (optional)")]
            [TextArea(2, 4)]
            public string luaScript;

            [Title("Operation Guide")]
            [Tooltip("触发此标记时显示的操作指南（可选）")]
            public OperationGuideData operationGuide;

            [Title("Metadata")]
            [TextArea(1, 2)]
            public string description;

            private static IEnumerable<string> GetQuestStates()
            {
                return new[] { "unassigned", "active", "success", "failure", "abandoned" };
            }

            /// <summary>
            /// Editor display label
            /// </summary>
            public string GetLabel()
            {
                string label = marker.ToString();
                if (!string.IsNullOrEmpty(questName))
                    label += $" -> {questName}";
                if (!string.IsNullOrEmpty(conversationToStart))
                    label += $" + Conv";
                return label;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            TutorialEventBus.OnMarkerTriggered += HandleMarkerTriggered;
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= HandleMarkerTriggered;
        }

        #endregion

        #region Core Logic

        /// <summary>
        /// Build dictionary for fast lookup
        /// </summary>
        private void BuildMappingDictionary()
        {
            mappingDictionary = new Dictionary<TutorialMarker, MarkerQuestMapping>();

            foreach (var mapping in mappings)
            {
                if (!mappingDictionary.ContainsKey(mapping.marker))
                {
                    mappingDictionary[mapping.marker] = mapping;
                }
                else
                {
                    Debug.LogWarning($"[TutorialMarkerBridge] Duplicate mapping for marker: {mapping.marker}");
                }
            }

            if (debugMode)
            {
                Debug.Log($"[TutorialMarkerBridge] Built mapping dictionary with {mappingDictionary.Count} entries");
            }
        }

        /// <summary>
        /// Handle TutorialMarker triggered event
        /// </summary>
        private void HandleMarkerTriggered(TutorialMarker marker)
        {
            if (!mappingDictionary.TryGetValue(marker, out var mapping))
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

            // Execute all configured actions
            ExecuteQuestAction(mapping);
            ExecuteLuaAction(mapping);
            ExecuteConversationAction(mapping);
            ExecuteGuideAction(mapping);
        }

        /// <summary>
        /// Update Quest state based on mapping
        /// </summary>
        private void ExecuteQuestAction(MarkerQuestMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.questName))
                return;

            try
            {
                if (mapping.questEntryNumber > 0)
                {
                    // Update specific entry state
                    QuestLog.SetQuestEntryState(mapping.questName, mapping.questEntryNumber, mapping.targetState);
                    if (debugMode)
                    {
                        Debug.Log($"[TutorialMarkerBridge] Set quest entry state: {mapping.questName}[{mapping.questEntryNumber}] = {mapping.targetState}");
                    }
                }
                else
                {
                    // Update main quest state
                    QuestLog.SetQuestState(mapping.questName, mapping.targetState);
                    if (debugMode)
                    {
                        Debug.Log($"[TutorialMarkerBridge] Set quest state: {mapping.questName} = {mapping.targetState}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TutorialMarkerBridge] Failed to update quest state: {e.Message}");
            }
        }

        /// <summary>
        /// Execute custom Lua script
        /// </summary>
        private void ExecuteLuaAction(MarkerQuestMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.luaScript))
                return;

            try
            {
                Lua.Run(mapping.luaScript);
                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] Executed Lua: {mapping.luaScript}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TutorialMarkerBridge] Failed to execute Lua: {e.Message}");
            }
        }

        /// <summary>
        /// Start conversation if configured
        /// </summary>
        private void ExecuteConversationAction(MarkerQuestMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.conversationToStart))
                return;

            // Check if a conversation is already active
            if (DialogueManager.isConversationActive)
            {
                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] Conversation already active, queuing: {mapping.conversationToStart}");
                }
                // Queue the conversation to start after current one ends
                DialogueManager.instance.StartCoroutine(StartConversationWhenReady(mapping.conversationToStart));
                return;
            }

            if (mapping.conversationDelay > 0)
            {
                DialogueManager.instance.StartCoroutine(StartConversationDelayed(mapping.conversationToStart, mapping.conversationDelay));
            }
            else
            {
                StartConversation(mapping.conversationToStart);
            }
        }

        /// <summary>
        /// 显示操作指南（如果配置了的话）
        /// </summary>
        private void ExecuteGuideAction(MarkerQuestMapping mapping)
        {
            if (mapping.operationGuide == null) return;

            if (OperationGuideManager.Instance != null)
            {
                OperationGuideManager.Instance.ShowGuide(mapping.operationGuide);

                if (debugMode)
                {
                    Debug.Log($"[TutorialMarkerBridge] Showing operation guide: {mapping.operationGuide.guideId}");
                }
            }
        }

        /// <summary>
        /// Start conversation with delay
        /// </summary>
        private System.Collections.IEnumerator StartConversationDelayed(string conversationTitle, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (!DialogueManager.isConversationActive)
            {
                StartConversation(conversationTitle);
            }
            else
            {
                // Wait for current conversation to end
                yield return StartConversationWhenReady(conversationTitle);
            }
        }

        /// <summary>
        /// Wait for any active conversation to end, then start new one
        /// </summary>
        private System.Collections.IEnumerator StartConversationWhenReady(string conversationTitle)
        {
            while (DialogueManager.isConversationActive)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.1f); // Small delay to ensure clean transition

            StartConversation(conversationTitle);
        }

        /// <summary>
        /// Actually start the conversation
        /// </summary>
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

        #endregion

        #region Public API

        /// <summary>
        /// Add or update a mapping at runtime
        /// </summary>
        public void AddMapping(TutorialMarker marker, string questName, int entryNumber = 0,
            string targetState = "success", string conversation = null)
        {
            var mapping = new MarkerQuestMapping
            {
                marker = marker,
                questName = questName,
                questEntryNumber = entryNumber,
                targetState = targetState,
                conversationToStart = conversation
            };

            mappingDictionary[marker] = mapping;

            // Also add to serialized list if not present
            var existing = mappings.Find(m => m.marker == marker);
            if (existing != null)
            {
                mappings.Remove(existing);
            }
            mappings.Add(mapping);

            if (debugMode)
            {
                Debug.Log($"[TutorialMarkerBridge] Added/updated mapping for: {marker}");
            }
        }

        /// <summary>
        /// Check if a mapping exists for a marker
        /// </summary>
        public bool HasMapping(TutorialMarker marker)
        {
            return mappingDictionary != null && mappingDictionary.ContainsKey(marker);
        }

        /// <summary>
        /// Get mapping for a marker (returns null if not found)
        /// </summary>
        public MarkerQuestMapping GetMapping(TutorialMarker marker)
        {
            if (mappingDictionary != null && mappingDictionary.TryGetValue(marker, out var mapping))
            {
                return mapping;
            }
            return null;
        }

        /// <summary>
        /// Force rebuild mapping dictionary (call after modifying mappings list)
        /// </summary>
        [Button("Rebuild Mapping Dictionary")]
        public void RebuildMappings()
        {
            BuildMappingDictionary();
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Print All Mappings")]
        private void PrintAllMappings()
        {
            Debug.Log("=== Tutorial Marker Mappings ===");
            foreach (var mapping in mappings)
            {
                Debug.Log($"{mapping.marker} -> Quest: {mapping.questName}[{mapping.questEntryNumber}]={mapping.targetState}, Conv: {mapping.conversationToStart}");
            }
        }
#endif

        #endregion
    }
}
