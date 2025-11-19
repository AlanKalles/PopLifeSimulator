using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Manager;
using PopLife.NarrativeSystem;
using PopLife.GuidanceSystem;

namespace PopLife.Integration
{
    /// <summary>
    /// 教程内容分发器，根据标记决定触发叙事还是指引
    /// Tutorial content dispatcher, determines whether to trigger narrative or guidance based on markers
    /// </summary>
    public class TutorialContentDispatcher : MonoBehaviour
    {
        public static TutorialContentDispatcher Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private bool enableDispatcher = true;
        [SerializeField] private bool debugMode = false;

        [Header("Content Mapping")]
        [SerializeField] private List<TutorialContentMapping> contentMappings;

        // Runtime state
        private Dictionary<TutorialMarker, TutorialContentMapping> mappingDictionary;
        private Queue<TutorialContent> contentQueue;
        private bool isProcessingContent;

        // Events
        public event Action<TutorialContent> OnContentTriggered;
        public event Action<TutorialContent> OnContentCompleted;

        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            contentQueue = new Queue<TutorialContent>();
            BuildMappingDictionary();
        }

        private void OnEnable()
        {
            // Subscribe to tutorial marker events
            TutorialEventBus.OnMarkerTriggered += HandleMarkerTriggered;

            // Subscribe to system completion events
            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnNarrativeCompleted += HandleNarrativeCompleted;
                Debug.Log("[TutorialDispatcher] Successfully subscribed to NarrativeManager.OnNarrativeCompleted");
            }
            else
            {
                Debug.LogWarning("[TutorialDispatcher] NarrativeManager.Instance is null in OnEnable, will try again in Start");
            }

            if (GuidanceManager.Instance != null)
            {
                GuidanceManager.Instance.OnGuidanceCompleted += HandleGuidanceCompleted;
            }
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= HandleMarkerTriggered;

            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnNarrativeCompleted -= HandleNarrativeCompleted;
            }

            if (GuidanceManager.Instance != null)
            {
                GuidanceManager.Instance.OnGuidanceCompleted -= HandleGuidanceCompleted;
            }
        }

        private void Start()
        {
            
            if (contentMappings == null || contentMappings.Count == 0)
            {
                Debug.LogWarning("[TutorialDispatcher] No content mappings configured in Inspector! Please configure mappings in the TutorialContentDispatcher component.");
            }

            // 再次尝试订阅事件，以防在OnEnable时管理器还未初始化
            // Try subscribing again in case managers weren't initialized during OnEnable
            if (NarrativeManager.Instance != null)
            {
                // 先取消订阅以避免重复
                NarrativeManager.Instance.OnNarrativeCompleted -= HandleNarrativeCompleted;
                NarrativeManager.Instance.OnNarrativeCompleted += HandleNarrativeCompleted;
                Debug.Log("[TutorialDispatcher] Resubscribed to NarrativeManager.OnNarrativeCompleted in Start");
            }
            else
            {
                Debug.LogError("[TutorialDispatcher] NarrativeManager.Instance is still null in Start!");
            }

            if (GuidanceManager.Instance != null)
            {
                GuidanceManager.Instance.OnGuidanceCompleted -= HandleGuidanceCompleted;
                GuidanceManager.Instance.OnGuidanceCompleted += HandleGuidanceCompleted;
            }
        }

        /// <summary>
        /// 构建映射字典
        /// </summary>
        private void BuildMappingDictionary()
        {
            mappingDictionary = new Dictionary<TutorialMarker, TutorialContentMapping>();

            if (contentMappings != null)
            {
                foreach (var mapping in contentMappings)
                {
                    if (mapping != null && !mappingDictionary.ContainsKey(mapping.Marker))
                    {
                        mappingDictionary[mapping.Marker] = mapping;
                    }
                }
            }
        }

        /// <summary>
        /// 处理标记触发
        /// </summary>
        private void HandleMarkerTriggered(TutorialMarker marker)
        {
            if (!enableDispatcher)
            {
                Debug.Log("[TutorialDispatcher] Dispatcher is disabled");
                return;
            }

            if (!mappingDictionary.TryGetValue(marker, out TutorialContentMapping mapping))
            {
                if (debugMode)
                {
                    Debug.Log($"[TutorialDispatcher] No mapping found for marker: {marker}");
                }
                return;
            }

            Debug.Log($"[TutorialDispatcher] Processing marker: {marker} -> {mapping.ContentType}");

            // Create content object
            var content = new TutorialContent
            {
                Marker = marker,
                Type = mapping.ContentType,
                ContentID = mapping.ContentID,
                Priority = mapping.Priority
            };

            // Queue or trigger immediately
            if (isProcessingContent)
            {
                EnqueueContent(content);
            }
            else
            {
                TriggerContent(content);
            }
        }

        /// <summary>
        /// 触发内容
        /// </summary>
        private void TriggerContent(TutorialContent content)
        {
            if (content == null) return;

            isProcessingContent = true;
            OnContentTriggered?.Invoke(content);

            switch (content.Type)
            {
                case ContentType.Narrative:
                    TriggerNarrative(content.ContentID);
                    break;

                case ContentType.Guidance:
                    TriggerGuidance(content.ContentID);
                    break;

                case ContentType.Mixed:
                    TriggerMixedContent(content.ContentID);
                    break;

                case ContentType.Skip:
                    // Skip this marker
                    isProcessingContent = false;
                    ProcessQueue();
                    break;
            }
        }

        /// <summary>
        /// 触发叙事
        /// </summary>
        private void TriggerNarrative(string narrativeID)
        {
            if (NarrativeManager.Instance != null)
            {
                bool started = NarrativeManager.Instance.StartNarrative(narrativeID);

                if (!started)
                {
                    Debug.LogWarning($"[TutorialDispatcher] Failed to start narrative: {narrativeID}");
                    isProcessingContent = false;
                    ProcessQueue();
                }
            }
            else
            {
                Debug.LogError("[TutorialDispatcher] NarrativeManager not found");
                isProcessingContent = false;
                ProcessQueue();
            }
        }

        /// <summary>
        /// 触发指引
        /// </summary>
        private void TriggerGuidance(string guidanceID)
        {
            if (GuidanceManager.Instance != null)
            {
                // 使用新的 StartGuidanceByID 方法，支持从工厂创建
                // Use new StartGuidanceByID method that supports factory creation
                bool started = GuidanceManager.Instance.StartGuidanceByID(guidanceID);

                if (!started)
                {
                    Debug.LogWarning($"[TutorialDispatcher] Failed to start guidance: {guidanceID}");
                    isProcessingContent = false;
                    ProcessQueue();
                }
            }
            else
            {
                Debug.LogError("[TutorialDispatcher] GuidanceManager not found");
                isProcessingContent = false;
                ProcessQueue();
            }
        }

        /// <summary>
        /// 触发混合内容（先指引后叙事或反之）
        /// </summary>
        private void TriggerMixedContent(string contentID)
        {
            // For mixed content, determine the order based on content ID
            // This is a simplified approach - in production would have more sophisticated logic

            if (contentID.StartsWith("GUIDE_"))
            {
                // Guidance first, then narrative
                TriggerGuidance(contentID);
            }
            else if (contentID.StartsWith("NARR_"))
            {
                // Narrative first, then guidance
                TriggerNarrative(contentID);
            }
            else
            {
                // Default: try guidance first
                TriggerGuidance(contentID);
            }
        }

        /// <summary>
        /// 将内容加入队列
        /// </summary>
        private void EnqueueContent(TutorialContent content)
        {
            contentQueue.Enqueue(content);
            Debug.Log($"[TutorialDispatcher] Queued content: {content.ContentID}");
        }

        /// <summary>
        /// 处理内容队列
        /// </summary>
        private void ProcessQueue()
        {
            if (contentQueue.Count > 0 && !isProcessingContent)
            {
                var nextContent = contentQueue.Dequeue();
                TriggerContent(nextContent);
            }
        }

        /// <summary>
        /// 处理叙事完成
        /// </summary>
        private void HandleNarrativeCompleted(NarrativeSO narrative)
        {
            Debug.Log($"[TutorialDispatcher] HandleNarrativeCompleted called for: {narrative.NarrativeID}");
            Debug.Log($"[TutorialDispatcher] isProcessingContent was: {isProcessingContent}, setting to false");
            isProcessingContent = false;

            var content = new TutorialContent
            {
                Type = ContentType.Narrative,
                ContentID = narrative.NarrativeID
            };

            OnContentCompleted?.Invoke(content);

            Debug.Log($"[TutorialDispatcher] About to process queue. Queue count: {contentQueue.Count}");
            ProcessQueue();
        }

        /// <summary>
        /// 处理指引完成
        /// </summary>
        private void HandleGuidanceCompleted(GuidanceSequence sequence)
        {
            isProcessingContent = false;

            var content = new TutorialContent
            {
                Type = ContentType.Guidance,
                ContentID = sequence.SequenceID
            };

            OnContentCompleted?.Invoke(content);
            ProcessQueue();
        }

        /// <summary>
        /// 获取标记的内容类型
        /// </summary>
        public ContentType GetContentTypeForMarker(TutorialMarker marker)
        {
            if (mappingDictionary.TryGetValue(marker, out TutorialContentMapping mapping))
            {
                return mapping.ContentType;
            }

            return ContentType.None;
        }

        /// <summary>
        /// 添加或更新映射
        /// </summary>
        public void AddOrUpdateMapping(TutorialMarker marker, ContentType type, string contentID)
        {
            var mapping = new TutorialContentMapping
            {
                Marker = marker,
                ContentType = type,
                ContentID = contentID
            };

            // Update in list
            var existing = contentMappings.Find(m => m.Marker == marker);
            if (existing != null)
            {
                contentMappings.Remove(existing);
            }
            contentMappings.Add(mapping);

            // Update dictionary
            mappingDictionary[marker] = mapping;
        }

        /// <summary>
        /// 清除所有队列
        /// </summary>
        public void ClearQueues()
        {
            contentQueue.Clear();
            isProcessingContent = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Print All Mappings")]
        private void PrintAllMappings()
        {
            Debug.Log("=== Tutorial Content Mappings ===");
            foreach (var mapping in contentMappings)
            {
                Debug.Log($"{mapping.Marker} -> {mapping.ContentType}: {mapping.ContentID}");
            }
        }
#endif
    }

    /// <summary>
    /// 内容类型枚举
    /// </summary>
    public enum ContentType
    {
        None,           // 无内容
        Narrative,      // 叙事（对话）
        Guidance,       // 指引（教程）
        Mixed,          // 混合（两者都有）
        Skip            // 跳过
    }

    /// <summary>
    /// 教程内容映射
    /// </summary>
    [Serializable]
    public class TutorialContentMapping
    {
        public TutorialMarker Marker;
        public ContentType ContentType;
        public string ContentID;
        public string Description;
        public int Priority = 0;  // Higher priority content interrupts lower
    }

    /// <summary>
    /// 教程内容
    /// </summary>
    public class TutorialContent
    {
        public TutorialMarker Marker;
        public ContentType Type;
        public string ContentID;
        public int Priority;
    }
}