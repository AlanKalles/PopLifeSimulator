using UnityEngine;
using Sirenix.OdinInspector;
using PopLife.Manager;

namespace PopLife.Data
{
    [CreateAssetMenu(menuName = "PopLife/DialogueBridge/DialogueAction")]
    public class DialogueAction : ScriptableObject
    {
        [Header("Marker Trigger")]
        [Tooltip("Execute when this TutorialMarker is raised. None means this asset will not be triggered.")]
        [SerializeField]
        private TutorialMarker activationMarker = TutorialMarker.None;

        [Header("Execution Order")]
        [Tooltip("Lower values execute first when multiple actions share the same marker.")]
        [SerializeField]
        private int executionOrder = 100;

        [Header("Conversation")]
        [Tooltip("Conversation to start. Leave empty to skip.")]
        [SerializeField]
        private string conversationToStart;

        [Tooltip("Delay before starting the conversation.")]
        [ShowIf("@!string.IsNullOrEmpty(conversationToStart)")]
        [Range(0f, 5f)]
        [SerializeField]
        private float conversationDelay;

        [Header("Lua")]
        [Tooltip("Optional Lua script to execute.")]
        [TextArea(2, 4)]
        [SerializeField]
        private string luaScript;

        [Header("Metadata")]
        [TextArea(1, 2)]
        [SerializeField]
        private string description;

        public TutorialMarker ActivationMarker => activationMarker;
        public int ExecutionOrder => executionOrder;
        public string ConversationToStart => conversationToStart;
        public float ConversationDelay => conversationDelay;
        public string LuaScript => luaScript;
        public string Description => description;

        private void OnValidate()
        {
            if (activationMarker == TutorialMarker.None)
            {
                Debug.LogWarning($"[DialogueAction] '{name}' does not have a valid activation marker and will never trigger.", this);
            }
        }
    }
}
