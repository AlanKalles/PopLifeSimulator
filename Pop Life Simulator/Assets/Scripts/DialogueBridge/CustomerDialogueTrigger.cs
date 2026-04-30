using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Customers.Runtime;
using Sirenix.OdinInspector;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Customer dialogue trigger component
    /// Attach to customer prefab to enable dialogue when player clicks on the customer
    ///
    /// Features:
    /// - Syncs customer data to Lua variables before dialogue
    /// - Selects appropriate conversation based on customer traits
    /// - Cooldown to prevent spam clicking
    /// - Optional dialogue probability
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CustomerDialogueTrigger : MonoBehaviour
    {
        #region Serialized Fields

        [Title("Dialogue Settings")]
        [Tooltip("Can this customer trigger dialogue?")]
        [SerializeField] private bool canTriggerDialogue = true;

        [Tooltip("Cooldown between dialogues (seconds)")]
        [SerializeField] private float dialogueCooldown = 10f;

        [Title("Conversation Selection")]
        [Tooltip("Default conversation if no specific one is found")]
        [SerializeField] private string defaultConversation = "Customer/Generic";

        [Title("Debug")]
        [SerializeField] private bool debugMode = false;

        #endregion

        #region Private Fields

        private CustomerAgent customerAgent;
        private CustomerRecord customerRecord;
        private float lastDialogueTime = -999f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            customerAgent = GetComponent<CustomerAgent>();

            // Ensure collider is set up for clicks
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true; // Make sure it's a trigger for raycasting
            }
        }

        private void Start()
        {
            // Get customer record from agent
            if (customerAgent != null)
            {
                customerRecord = customerAgent.GetCustomerRecord();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempt to trigger dialogue with this customer
        /// Called by CustomerClickHandler when player clicks on the customer
        /// </summary>
        public bool TryTriggerDialogue()
        {
            if (!canTriggerDialogue)
            {
                if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: Dialogue disabled");
                return false;
            }

            // Check cooldown
            if (Time.time - lastDialogueTime < dialogueCooldown)
            {
                if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: On cooldown");
                return false;
            }

            // ── 交互事件优先级最高 ──
            var adapter = GetComponent<CustomerBlackboardAdapter>();
            if (adapter != null && adapter.interactionBubbleActive
                && adapter.interactionClickAllowed
                && adapter.assignedInteraction != null
                && !DialogueManager.isConversationActive)
            {
                string conv = adapter.assignedInteraction.ConversationTitle;
                if (!string.IsNullOrEmpty(conv)
                    && DialogueManager.ConversationHasValidEntry(conv))
                {
                    SyncCustomerDataToLua();
                    DialogueLua.SetVariable("InteractionCorrect", false);
                    lastDialogueTime = Time.time;
                    adapter.interactionDialogueStarted = true;
                    DialogueManager.StartConversation(conv, null, transform);
                    if (debugMode)
                        Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: " +
                                  $"开始交互事件对话: {conv}");
                    return true;
                }
            }

            // Check if a conversation is already active
            if (DialogueManager.isConversationActive)
            {
                if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: Conversation already active");
                return false;
            }

            // Sync customer data to Lua
            SyncCustomerDataToLua();

            // Select and start conversation
            string conversationTitle = SelectConversation();

            if (string.IsNullOrEmpty(conversationTitle))
            {
                if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: No conversation found");
                return false;
            }

            // Check if conversation exists
            if (!DialogueManager.ConversationHasValidEntry(conversationTitle))
            {
                if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: Conversation not found: {conversationTitle}");

                // Try default conversation
                if (!DialogueManager.ConversationHasValidEntry(defaultConversation))
                {
                    if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: Default conversation also not found");
                    return false;
                }

                conversationTitle = defaultConversation;
            }

            // Update cooldown
            lastDialogueTime = Time.time;

            // Start conversation with this customer as the conversant
            DialogueManager.StartConversation(conversationTitle, null, transform);

            if (debugMode) Debug.Log($"[CustomerDialogueTrigger] {gameObject.name}: Started conversation: {conversationTitle}");

            return true;
        }

        /// <summary>
        /// Enable or disable dialogue for this customer
        /// </summary>
        public void SetDialogueEnabled(bool enabled)
        {
            canTriggerDialogue = enabled;
        }

        #endregion

        #region Dialogue System Messages

        /// <summary>
        /// Pixel Crushers 在对话启动时自动发送此 Unity message 到 conversant 的 GameObject 及其 children。
        /// 此时 conversation 已 active，可以安全调用 SetActorPortraitSprite。
        /// Display Name 也在这里覆盖，保证第一个 subtitle 显示时已生效。
        /// </summary>
        private void OnConversationStart(Transform actor)
        {
            ApplyCustomerIdentityToDialogue();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 把当前顾客的 Display Name 和 portrait 动态覆盖到 "Customer" actor 上。
        /// 所有顾客共享 "Customer" actor，每次对话开始时覆盖一次。
        /// Portrait 按 Resources/CustomerPortraits/{customerId}_{name} 加载；
        /// 找不到时传 null，由 Standard Dialogue UI 按配置隐藏 portrait 格。
        /// </summary>
        private void ApplyCustomerIdentityToDialogue()
        {
            if (customerRecord == null) return;

            // 1. Display Name — Pixel Crushers UI 优先读 Display Name，fallback 到 actor 主键
            string displayName = string.IsNullOrEmpty(customerRecord.name)
                ? "Customer"
                : customerRecord.name;
            DialogueLua.SetActorField("Customer", "Display Name", displayName);

            // 2. Portrait — 按 {customerId}_{name} 约定从 Resources 加载
            Sprite portrait = null;
            if (!string.IsNullOrEmpty(customerRecord.customerId) && !string.IsNullOrEmpty(customerRecord.name))
            {
                string portraitPath = $"CustomerPortraits/{customerRecord.customerId}_{customerRecord.name}";
                portrait = Resources.Load<Sprite>(portraitPath);

                if (portrait == null && debugMode)
                {
                    Debug.Log($"[CustomerDialogueTrigger] Portrait 未找到: {portraitPath}");
                }
            }

            // 传 null 时由 Standard Dialogue UI 的 portrait Image 处理（通常配置成自动隐藏）
            if (DialogueManager.instance != null)
            {
                DialogueManager.instance.SetActorPortraitSprite("Customer", portrait);
            }
        }

        /// <summary>
        /// Sync customer data to Lua variables for use in dialogue conditions
        /// </summary>
        private void SyncCustomerDataToLua()
        {
            if (customerRecord == null)
            {
                // Set defaults if no record
                DialogueLua.SetVariable("CurrentCustomer_ID", "Unknown");
                DialogueLua.SetVariable("CurrentCustomer_Name", "Customer");
                DialogueLua.SetVariable("CurrentCustomer_LoyaltyLevel", 0);
                DialogueLua.SetVariable("CurrentCustomer_VisitCount", 0);
                DialogueLua.SetVariable("CurrentCustomer_LifetimeSpent", 0);
                DialogueLua.SetVariable("CurrentCustomer_Traits", "");
                return;
            }

            DialogueLua.SetVariable("CurrentCustomer_ID", customerRecord.customerId ?? "Unknown");
            DialogueLua.SetVariable("CurrentCustomer_Name", customerRecord.name ?? "Customer");
            DialogueLua.SetVariable("CurrentCustomer_LoyaltyLevel", customerRecord.loyaltyLevel);
            DialogueLua.SetVariable("CurrentCustomer_VisitCount", customerRecord.visitCount);
            DialogueLua.SetVariable("CurrentCustomer_LifetimeSpent", customerRecord.lifetimeSpent);

            // Traits 已移除，清空 Lua 变量
            DialogueLua.SetVariable("CurrentCustomer_Traits", "");

            if (debugMode)
            {
                Debug.Log($"[CustomerDialogueTrigger] Synced customer data: {customerRecord.customerId}");
            }
        }

        /// <summary>
        /// Select the most appropriate conversation for this customer
        /// </summary>
        private string SelectConversation()
        {
            if (customerRecord == null)
            {
                return defaultConversation;
            }

            // Priority 1: Customer-specific conversation
            string customerConversation = $"Customer/{customerRecord.customerId}";
            if (DialogueManager.ConversationHasValidEntry(customerConversation))
            {
                return customerConversation;
            }

            // Priority 2: Trait-based conversations（Trait 已移除，跳过此优先级）

            // Priority 3: Loyalty level based conversations
            if (customerRecord.loyaltyLevel > 0)
            {
                string loyaltyConversation = $"Customer/Loyalty_{customerRecord.loyaltyLevel}";
                if (DialogueManager.ConversationHasValidEntry(loyaltyConversation))
                {
                    return loyaltyConversation;
                }
            }

            // Fallback: Default conversation
            return defaultConversation;
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [Button("Test Trigger Dialogue")]
        private void TestTriggerDialogue()
        {
            if (Application.isPlaying)
            {
                TryTriggerDialogue();
            }
            else
            {
                Debug.Log("[CustomerDialogueTrigger] Test only works in Play mode");
            }
        }
#endif

        #endregion
    }
}
