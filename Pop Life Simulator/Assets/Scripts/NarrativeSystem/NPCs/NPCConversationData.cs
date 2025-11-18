using System;
using UnityEngine;

namespace PopLife.NarrativeSystem.NPCs
{
    /// <summary>
    /// NPC对话数据组件，挂载在NPC GameObject上
    /// NPC conversation data component attached to NPC GameObjects
    /// </summary>
    public class NPCConversationData : MonoBehaviour
    {
        [Header("NPC Identity")]
        [SerializeField] private string npcID;
        [SerializeField] private string npcName;
        [SerializeField] private NPCType npcType;

        [Header("Conversation Data")]
        [SerializeField] private NarrativeSO conversationData;
        [SerializeField] private NarrativeSO[] additionalConversations;  // Multiple conversations

        [Header("Trigger Settings")]
        [SerializeField] private TriggerType triggerType = TriggerType.Interaction;
        [SerializeField] private float triggerDistance = 2f;
        [SerializeField] private bool requiresPlayerFacing = true;
        [SerializeField] private LayerMask playerLayer;

        [Header("Conversation State")]
        [SerializeField] private bool hasBeenTriggered;
        [SerializeField] private int conversationCount;
        [SerializeField] private float lastConversationTime;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject interactionIndicator;  // UI indicator above NPC
        [SerializeField] private Color indicatorColor = Color.yellow;

        // Events
        public event Action<NarrativeSO> OnConversationTriggered;
        public event Action OnConversationCompleted;

        // Properties
        public string NPCID => npcID;
        public string NPCName => npcName;
        public NPCType Type => npcType;
        public bool HasConversation => conversationData != null;
        public int ConversationCount => conversationCount;

        private bool playerInRange;
        private Transform playerTransform;

        /// <summary>
        /// NPC类型枚举
        /// </summary>
        public enum NPCType
        {
            Regular,      // 普通NPC
            VIP,         // VIP顾客
            Customer,    // 普通顾客
            Special,     // 特殊NPC（不进店）
            Staff        // 员工
        }

        /// <summary>
        /// 触发类型枚举
        /// </summary>
        public enum TriggerType
        {
            Interaction,  // 玩家交互触发
            Proximity,    // 接近自动触发
            Automatic,    // 自动触发（条件满足时）
            Scripted      // 脚本控制触发
        }

        private void Awake()
        {
            InitializeNPC();
        }

        private void InitializeNPC()
        {
            // Auto-generate ID if not set
            if (string.IsNullOrEmpty(npcID))
            {
                npcID = $"NPC_{gameObject.name}_{GetInstanceID()}";
            }

            // Set name from GameObject if not specified
            if (string.IsNullOrEmpty(npcName))
            {
                npcName = gameObject.name;
            }

            // Hide interaction indicator initially
            if (interactionIndicator != null)
            {
                interactionIndicator.SetActive(false);
            }
        }

        private void Update()
        {
            if (triggerType == TriggerType.Proximity)
            {
                CheckProximityTrigger();
            }
            else if (triggerType == TriggerType.Interaction)
            {
                CheckInteractionInput();
            }

            UpdateIndicator();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                playerInRange = true;
                playerTransform = other.transform;
                ShowIndicator();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
            {
                playerInRange = false;
                playerTransform = null;
                HideIndicator();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayer(other))
            {
                playerInRange = true;
                playerTransform = other.transform;
                ShowIndicator();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other))
            {
                playerInRange = false;
                playerTransform = null;
                HideIndicator();
            }
        }

        /// <summary>
        /// 触发对话
        /// </summary>
        public bool TriggerConversation()
        {
            if (!CanTriggerConversation())
            {
                Debug.Log($"[NPCConversation] Cannot trigger conversation for {npcName}");
                return false;
            }

            // Select conversation to use
            NarrativeSO conversationToUse = SelectConversation();
            if (conversationToUse == null)
            {
                Debug.LogWarning($"[NPCConversation] No valid conversation for {npcName}");
                return false;
            }

            // Start narrative
            bool started = NarrativeManager.Instance?.StartNarrative(conversationToUse) ?? false;

            if (started)
            {
                hasBeenTriggered = true;
                conversationCount++;
                lastConversationTime = Time.time;

                OnConversationTriggered?.Invoke(conversationToUse);

                // Subscribe to completion
                if (NarrativeManager.Instance != null)
                {
                    NarrativeManager.Instance.OnNarrativeCompleted += HandleConversationCompleted;
                }

                Debug.Log($"[NPCConversation] Started conversation with {npcName}");
            }

            return started;
        }

        /// <summary>
        /// 检查是否可以触发对话
        /// </summary>
        private bool CanTriggerConversation()
        {
            // Check if narrative system is available
            if (NarrativeManager.Instance == null || NarrativeManager.Instance.IsNarrativeActive())
            {
                return false;
            }

            // Check if has conversation data
            if (conversationData == null && (additionalConversations == null || additionalConversations.Length == 0))
            {
                return false;
            }

            // Check cooldown (prevent spam)
            if (Time.time - lastConversationTime < 1f)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 选择要使用的对话
        /// </summary>
        private NarrativeSO SelectConversation()
        {
            // Logic to select which conversation to use
            // Could be based on conversation count, game state, etc.

            if (conversationCount == 0 && conversationData != null)
            {
                return conversationData; // First conversation
            }

            if (additionalConversations != null && additionalConversations.Length > 0)
            {
                // Cycle through additional conversations
                int index = (conversationCount - 1) % additionalConversations.Length;
                if (index >= 0 && index < additionalConversations.Length)
                {
                    return additionalConversations[index];
                }
            }

            // Fall back to main conversation
            return conversationData;
        }

        /// <summary>
        /// 检查接近触发
        /// </summary>
        private void CheckProximityTrigger()
        {
            if (!playerInRange) return;

            if (playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, playerTransform.position);
                if (distance <= triggerDistance)
                {
                    TriggerConversation();
                }
            }
        }

        /// <summary>
        /// 检查交互输入
        /// </summary>
        private void CheckInteractionInput()
        {
            if (!playerInRange) return;

            // Check for interaction input (E key by default)
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (requiresPlayerFacing && !IsPlayerFacingNPC())
                {
                    return;
                }

                TriggerConversation();
            }
        }

        /// <summary>
        /// 检查玩家是否面向NPC
        /// </summary>
        private bool IsPlayerFacingNPC()
        {
            if (playerTransform == null) return false;

            Vector3 directionToNPC = (transform.position - playerTransform.position).normalized;
            float angle = Vector3.Angle(playerTransform.forward, directionToNPC);

            return angle < 45f; // Within 45 degrees
        }

        /// <summary>
        /// 更新指示器
        /// </summary>
        private void UpdateIndicator()
        {
            if (interactionIndicator != null && playerInRange && HasConversation)
            {
                // Make indicator face camera
                if (Camera.main != null)
                {
                    interactionIndicator.transform.LookAt(Camera.main.transform);
                    interactionIndicator.transform.Rotate(0, 180, 0);
                }

                // Pulse effect
                float pulse = Mathf.Sin(Time.time * 2f) * 0.1f + 1f;
                interactionIndicator.transform.localScale = Vector3.one * pulse;
            }
        }

        /// <summary>
        /// 显示交互指示器
        /// </summary>
        private void ShowIndicator()
        {
            if (interactionIndicator != null && HasConversation)
            {
                interactionIndicator.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏交互指示器
        /// </summary>
        private void HideIndicator()
        {
            if (interactionIndicator != null)
            {
                interactionIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// 处理对话完成
        /// </summary>
        private void HandleConversationCompleted(NarrativeSO narrative)
        {
            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnNarrativeCompleted -= HandleConversationCompleted;
            }

            OnConversationCompleted?.Invoke();
        }

        /// <summary>
        /// 检查是否是玩家
        /// </summary>
        private bool IsPlayer(Component other)
        {
            if (playerLayer == 0)
            {
                // Check by tag if layer not set
                return other.CompareTag("Player");
            }

            return ((1 << other.gameObject.layer) & playerLayer) != 0;
        }

        /// <summary>
        /// 设置对话数据（运行时）
        /// </summary>
        public void SetConversationData(NarrativeSO narrative)
        {
            conversationData = narrative;
        }

        /// <summary>
        /// 重置对话状态
        /// </summary>
        public void ResetConversationState()
        {
            hasBeenTriggered = false;
            conversationCount = 0;
            lastConversationTime = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw trigger distance
            if (triggerType == TriggerType.Proximity)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, triggerDistance);
            }
        }
#endif
    }
}