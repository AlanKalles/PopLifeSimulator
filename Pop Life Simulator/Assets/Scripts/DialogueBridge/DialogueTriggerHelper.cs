using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// Static helper class for triggering dialogues and tutorials from anywhere in code
    ///
    /// Examples:
    ///   DialogueTriggerHelper.StartConversation("Tutorial/Welcome");
    ///   DialogueTriggerHelper.StartConversationIf("Tutorial/Day2", "Variable['CurrentDay'] >= 2");
    ///   DialogueTriggerHelper.ShowSpotlightAndTalk("Tutorial/Build", buildButtonRectTransform);
    /// </summary>
    public static class DialogueTriggerHelper
    {
        #region Conversation Triggers

        /// <summary>
        /// Start a conversation by title
        /// </summary>
        public static bool StartConversation(string conversationTitle)
        {
            if (string.IsNullOrEmpty(conversationTitle))
            {
                Debug.LogWarning("[DialogueTriggerHelper] StartConversation called with empty title");
                return false;
            }

            if (DialogueManager.isConversationActive)
            {
                Debug.LogWarning($"[DialogueTriggerHelper] Cannot start '{conversationTitle}' - conversation already active");
                return false;
            }

            if (!DialogueManager.ConversationHasValidEntry(conversationTitle))
            {
                Debug.LogWarning($"[DialogueTriggerHelper] Conversation not found: {conversationTitle}");
                return false;
            }

            DialogueManager.StartConversation(conversationTitle);
            return true;
        }

        /// <summary>
        /// Start a conversation with actor and conversant
        /// </summary>
        public static bool StartConversation(string conversationTitle, Transform actor, Transform conversant)
        {
            if (string.IsNullOrEmpty(conversationTitle))
            {
                Debug.LogWarning("[DialogueTriggerHelper] StartConversation called with empty title");
                return false;
            }

            if (DialogueManager.isConversationActive)
            {
                Debug.LogWarning($"[DialogueTriggerHelper] Cannot start '{conversationTitle}' - conversation already active");
                return false;
            }

            if (!DialogueManager.ConversationHasValidEntry(conversationTitle))
            {
                Debug.LogWarning($"[DialogueTriggerHelper] Conversation not found: {conversationTitle}");
                return false;
            }

            DialogueManager.StartConversation(conversationTitle, actor, conversant);
            return true;
        }

        /// <summary>
        /// Start a conversation only if a Lua condition is true
        /// </summary>
        public static bool StartConversationIf(string conversationTitle, string luaCondition)
        {
            if (string.IsNullOrEmpty(luaCondition))
            {
                return StartConversation(conversationTitle);
            }

            if (Lua.IsTrue(luaCondition))
            {
                return StartConversation(conversationTitle);
            }

            return false;
        }

        /// <summary>
        /// Queue a conversation to start after current one ends
        /// </summary>
        public static void QueueConversation(string conversationTitle)
        {
            if (string.IsNullOrEmpty(conversationTitle)) return;

            if (DialogueManager.isConversationActive)
            {
                // Use coroutine to wait
                if (DialogueManager.instance != null)
                {
                    DialogueManager.instance.StartCoroutine(WaitAndStartConversation(conversationTitle));
                }
            }
            else
            {
                StartConversation(conversationTitle);
            }
        }

        private static System.Collections.IEnumerator WaitAndStartConversation(string conversationTitle)
        {
            while (DialogueManager.isConversationActive)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.1f);

            StartConversation(conversationTitle);
        }

        #endregion

        #region Spotlight + Dialogue

        /// <summary>
        /// [DEPRECATED] 旧 helper：spotlight 自动隐藏 DialogueUI 的状态机已移除。
        /// 新系统中 spotlight 与对话 UI 解耦，无需此种"配套调用"。
        ///
        /// 推荐做法：在对话节点 Sequence 字段写
        /// <c>required ShowSpotlight(targetId, "提示文字", Right); Continue()</c>
        /// 让 spotlight 与对话节点同步推进。
        /// </summary>
        [System.Obsolete("使用对话节点 Sequence 字段中的 ShowSpotlight(...) 命令替代")]
        public static void ShowSpotlightAndTalk(string conversationTitle, RectTransform spotlightTarget,
            SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (SpotlightManager.Instance != null && spotlightTarget != null)
            {
                SpotlightManager.Instance.Show(spotlightTarget, SpotlightRequest.NullTextSentinel,
                    TooltipPosition.Auto, shape, InteractionMode.Passthrough);
            }
            StartConversation(conversationTitle);
        }

        [System.Obsolete("使用对话节点 Sequence 字段中的 ShowSpotlight(...) 命令替代")]
        public static void ShowSpotlightWorldAndTalk(string conversationTitle, GameObject spotlightTarget,
            SpotlightShape shape = SpotlightShape.RoundedRectangle)
        {
            if (SpotlightManager.Instance != null && spotlightTarget != null)
            {
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ForWorld(spotlightTarget),
                    text = SpotlightRequest.NullTextSentinel, shape = shape,
                    position = TooltipPosition.Auto, mode = InteractionMode.Passthrough,
                });
            }
            StartConversation(conversationTitle);
        }

        [System.Obsolete("使用对话节点 Sequence 字段中的 ShowSpotlight(...) 命令替代")]
        public static void ShowSpotlightByNameAndTalk(string conversationTitle, string targetName,
            SpotlightShape shape = SpotlightShape.RoundedRectangle, bool isWorldObject = false)
        {
            if (SpotlightManager.Instance != null && !string.IsNullOrEmpty(targetName))
            {
                if (isWorldObject)
                {
                    var go = GameObject.Find(targetName);
                    if (go != null)
                    {
                        SpotlightManager.Instance.Show(new SpotlightRequest
                        {
                            target = SpotlightTargetSpec.ForWorld(go),
                            text = SpotlightRequest.NullTextSentinel, shape = shape,
                            position = TooltipPosition.Auto, mode = InteractionMode.Passthrough,
                        });
                    }
                }
                else
                {
                    SpotlightManager.Instance.Show(targetName, SpotlightRequest.NullTextSentinel,
                        TooltipPosition.Auto, shape, InteractionMode.Passthrough);
                }
            }
            StartConversation(conversationTitle);
        }

        public static void HideSpotlight()
        {
            SpotlightManager.Instance?.Hide(CloseReason.Manual);
        }

        #endregion

        #region Lua Helpers

        /// <summary>
        /// Set a Lua variable
        /// </summary>
        public static void SetVariable(string variableName, object value)
        {
            if (value is bool boolValue)
            {
                DialogueLua.SetVariable(variableName, boolValue);
            }
            else if (value is int intValue)
            {
                DialogueLua.SetVariable(variableName, intValue);
            }
            else if (value is float floatValue)
            {
                DialogueLua.SetVariable(variableName, floatValue);
            }
            else if (value is double doubleValue)
            {
                DialogueLua.SetVariable(variableName, doubleValue);
            }
            else
            {
                DialogueLua.SetVariable(variableName, value?.ToString() ?? "");
            }
        }

        /// <summary>
        /// Get a Lua variable as string
        /// </summary>
        public static string GetVariableString(string variableName)
        {
            return DialogueLua.GetVariable(variableName).asString;
        }

        /// <summary>
        /// Get a Lua variable as int
        /// </summary>
        public static int GetVariableInt(string variableName)
        {
            return DialogueLua.GetVariable(variableName).asInt;
        }

        /// <summary>
        /// Get a Lua variable as bool
        /// </summary>
        public static bool GetVariableBool(string variableName)
        {
            return DialogueLua.GetVariable(variableName).asBool;
        }

        /// <summary>
        /// Evaluate a Lua condition
        /// </summary>
        public static bool EvaluateCondition(string luaCondition)
        {
            return Lua.IsTrue(luaCondition);
        }

        /// <summary>
        /// Run arbitrary Lua code
        /// </summary>
        public static void RunLua(string luaCode)
        {
            Lua.Run(luaCode);
        }

        #endregion

        #region Quest Helpers

        /// <summary>
        /// Set quest state
        /// </summary>
        public static void SetQuestState(string questName, string state)
        {
            QuestLog.SetQuestState(questName, state);
        }

        /// <summary>
        /// Set quest entry state
        /// </summary>
        public static void SetQuestEntryState(string questName, int entryNumber, string state)
        {
            QuestLog.SetQuestEntryState(questName, entryNumber, state);
        }

        /// <summary>
        /// Get quest state
        /// </summary>
        public static string GetQuestState(string questName)
        {
            return QuestLog.GetQuestState(questName).ToString();
        }

        /// <summary>
        /// Check if quest is active
        /// </summary>
        public static bool IsQuestActive(string questName)
        {
            return QuestLog.IsQuestActive(questName);
        }

        /// <summary>
        /// Check if quest is successful
        /// </summary>
        public static bool IsQuestSuccessful(string questName)
        {
            return QuestLog.IsQuestSuccessful(questName);
        }

        #endregion
    }
}
