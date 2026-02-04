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

        [Tooltip("Conversation prefix for trait-based conversations")]
        [SerializeField] private string traitConversationPrefix = "Customer/Trait_";

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

        #region Private Methods

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

            // Sync traits as comma-separated string
            string traitsStr = "";
            if (customerRecord.traitIds != null && customerRecord.traitIds.Length > 0)
            {
                traitsStr = string.Join(",", customerRecord.traitIds);
            }
            DialogueLua.SetVariable("CurrentCustomer_Traits", traitsStr);

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

            // Priority 2: Trait-based conversations
            if (customerRecord.traitIds != null)
            {
                foreach (var traitId in customerRecord.traitIds)
                {
                    string traitConversation = $"{traitConversationPrefix}{traitId}";
                    if (DialogueManager.ConversationHasValidEntry(traitConversation))
                    {
                        return traitConversation;
                    }
                }
            }

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
