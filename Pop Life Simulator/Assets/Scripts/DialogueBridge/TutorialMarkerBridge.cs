using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Manager;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Bridges TutorialMarker events with Dialogue System conversations and Lua scripts.
    /// Guide 触发由 OperationGuideManager 自处理，Quest 激活由 QuestLogicManager 自处理。
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

        [Title("Marker to Conversation/Lua Mappings")]
        [InfoBox("配置 Marker → Conversation / Lua 映射。Guide 和 Quest 触发请直接在各自的 SO 中配置 activationMarker。")]
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
        /// 简化后的映射：仅处理 marker → conversation / lua
        /// Guide 触发已由 OperationGuideManager 自处理
        /// Quest 激活已由 QuestLogicManager 自处理
        /// </summary>
        [Serializable]
        public class MarkerQuestMapping
        {
            [Title("Trigger")]
            [EnumToggleButtons]
            public TutorialMarker marker;

            [Title("Conversation")]
            [Tooltip("Conversation to start (leave empty to skip)")]
            public string conversationToStart;

            [Tooltip("Delay before starting conversation (seconds)")]
            [ShowIf("@!string.IsNullOrEmpty(conversationToStart)")]
            [Range(0f, 5f)]
            public float conversationDelay = 0f;

            [Title("Lua")]
            [Tooltip("Custom Lua code to execute (optional)")]
            [TextArea(2, 4)]
            public string luaScript;

            [Title("Metadata")]
            [TextArea(1, 2)]
            public string description;
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

            // Execute conversation and lua actions
            // Guide 触发由 OperationGuideManager 自处理
            // Quest 激活由 QuestLogicManager 自处理
            ExecuteLuaAction(mapping);
            ExecuteConversationAction(mapping);
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
        public void AddMapping(TutorialMarker marker, string conversation = null, string lua = null)
        {
            var mapping = new MarkerQuestMapping
            {
                marker = marker,
                conversationToStart = conversation,
                luaScript = lua
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
                Debug.Log($"{mapping.marker} -> Conv: {mapping.conversationToStart}, Lua: {mapping.luaScript}");
            }
        }
#endif

        #endregion
    }
}
