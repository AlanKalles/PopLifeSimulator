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
                Debug.Log($"[TutorialMarkerBridge] Processing marker: {marker} ({actions.Count} action(s))");
            }

            // actions 已在 BuildLookupTables() 按 executionOrder 升序排好。
            // 串行执行：前一个 action 启动的对话结束后，才启动下一个 action。
            // ⚠️ 串行范围限于"同一 marker 内"——不同 marker 同时被 raise 时，它们各自的协程仍并发，
            //    顺序由 DialogueManager.isConversationActive 抢占决定，不可控。当前游戏无此场景。
            DialogueManager.instance.StartCoroutine(ExecuteActionsSerially(actions));
        }

        private System.Collections.IEnumerator ExecuteActionsSerially(List<DialogueAction> actions)
        {
            foreach (var action in actions)
            {
                if (action == null) continue;

                // 1. 先等任何进行中的对话结束（可能是上一 action 启动的，也可能是无关的）。
                //    这样 Lua、delay、StartConversation 都和"该 action 自身的 conversation"绑定时序，
                //    避免未来出现"Lua 改变量，紧接的 conversation 用到"的场景因 Lua 提前执行而踩坑。
                while (DialogueManager.isConversationActive)
                    yield return null;

                // 2. 执行 Lua（与本 action 的 conversation 时序绑定，紧贴启动前）
                ExecuteLuaAction(action.LuaScript);

                // 3. 跳过空 conversation
                if (string.IsNullOrEmpty(action.ConversationToStart))
                    continue;

                // 4. 启动前缓冲（delay 回归本意：UI/动画过渡时间，仅作用于本 action）
                if (action.ConversationDelay > 0f)
                    yield return new WaitForSecondsRealtime(action.ConversationDelay);

                // 5. 启动对话
                StartConversation(action.ConversationToStart);

                // 6. 等下一帧让 isConversationActive 翻成 true（StartConversation 同步调用但状态需 1 帧生效）
                yield return null;

                // 7. 等本对话结束才走下一个 action
                while (DialogueManager.isConversationActive)
                    yield return null;
            }
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
