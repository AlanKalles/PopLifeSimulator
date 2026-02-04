using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// 调试 Dialogue System UI 状态
    /// </summary>
    public class DialogueUIDebugger : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[DialogueUIDebugger] === 启动诊断 ===");

            // 检查 DialogueManager
            if (DialogueManager.instance == null)
            {
                Debug.LogError("[DialogueUIDebugger] DialogueManager.instance 为 null!");
                return;
            }

            Debug.Log($"[DialogueUIDebugger] DialogueManager 存在: {DialogueManager.instance.name}");

            // 检查 DialogueUI
            var dialogueUI = DialogueManager.dialogueUI;
            if (dialogueUI == null)
            {
                Debug.LogError("[DialogueUIDebugger] DialogueManager.dialogueUI 为 null!");
                Debug.LogError("[DialogueUIDebugger] 请检查 Dialogue Manager 的 Display Settings -> Dialogue UI 字段");
                return;
            }

            Debug.Log($"[DialogueUIDebugger] DialogueUI 存在: {dialogueUI}");

            // 检查是否是 StandardDialogueUI
            var standardUI = dialogueUI as StandardDialogueUI;
            if (standardUI == null)
            {
                Debug.LogWarning($"[DialogueUIDebugger] DialogueUI 不是 StandardDialogueUI 类型: {dialogueUI.GetType()}");
            }
            else
            {
                Debug.Log($"[DialogueUIDebugger] StandardDialogueUI 组件所在对象: {standardUI.gameObject.name}");
                Debug.Log($"[DialogueUIDebugger] StandardDialogueUI.isOpen: {standardUI.isOpen}");

                // 检查 conversationUIElements
                var conversationUI = standardUI.conversationUIElements;
                if (conversationUI.mainPanel == null)
                {
                    Debug.LogError("[DialogueUIDebugger] conversationUIElements.mainPanel 为 null!");
                }
                else
                {
                    Debug.Log($"[DialogueUIDebugger] mainPanel: {conversationUI.mainPanel.name}, active: {conversationUI.mainPanel.gameObject.activeInHierarchy}");

                    // 检查 CanvasGroup
                    var canvasGroup = conversationUI.mainPanel.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        Debug.Log($"[DialogueUIDebugger] mainPanel CanvasGroup alpha: {canvasGroup.alpha}");
                    }

                    // 检查 Animator
                    var animator = conversationUI.mainPanel.GetComponent<Animator>();
                    if (animator != null)
                    {
                        Debug.Log($"[DialogueUIDebugger] mainPanel Animator 存在, enabled: {animator.enabled}");
                        Debug.Log($"[DialogueUIDebugger] Animator controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}");

                        if (animator.runtimeAnimatorController == null)
                        {
                            Debug.LogError("[DialogueUIDebugger] Animator 没有 Controller!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[DialogueUIDebugger] mainPanel 没有 Animator 组件");
                    }
                }

                // 检查 subtitle panels
                if (conversationUI.defaultNPCSubtitlePanel == null)
                {
                    Debug.LogError("[DialogueUIDebugger] defaultNPCSubtitlePanel 为 null!");
                }
                else
                {
                    var panel = conversationUI.defaultNPCSubtitlePanel;
                    Debug.Log($"[DialogueUIDebugger] NPC Subtitle Panel: {panel.name}");
                    Debug.Log($"[DialogueUIDebugger] NPC Subtitle Panel GameObject active: {panel.gameObject.activeInHierarchy}");
                    Debug.Log($"[DialogueUIDebugger] NPC Subtitle Panel 组件 enabled: {panel.enabled}");

                    // 检查父对象链
                    Transform t = panel.transform;
                    string hierarchy = panel.name;
                    while (t.parent != null)
                    {
                        t = t.parent;
                        hierarchy = t.name + " -> " + hierarchy;
                        if (!t.gameObject.activeSelf)
                        {
                            Debug.LogWarning($"[DialogueUIDebugger] 父对象 {t.name} 是 inactive 的!");
                        }
                    }
                    Debug.Log($"[DialogueUIDebugger] 层级: {hierarchy}");
                }
            }

            // 检查数据库
            var database = DialogueManager.masterDatabase;
            if (database == null)
            {
                Debug.LogError("[DialogueUIDebugger] DialogueManager.masterDatabase 为 null!");
            }
            else
            {
                Debug.Log($"[DialogueUIDebugger] Database: {database.name}");

                // 检查对话是否存在
                var conversation = database.GetConversation("Midori/1");
                if (conversation == null)
                {
                    Debug.LogError("[DialogueUIDebugger] 找不到对话 'Midori/1'!");
                }
                else
                {
                    Debug.Log($"[DialogueUIDebugger] 对话 'Midori/1' 存在, 有 {conversation.dialogueEntries.Count} 个条目");
                }
            }

            Debug.Log("[DialogueUIDebugger] === 诊断完成 ===");
        }

        private void Update()
        {
            // 按 F5 手动测试启动对话
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[DialogueUIDebugger] 手动触发对话测试...");

                if (DialogueManager.isConversationActive)
                {
                    Debug.Log("[DialogueUIDebugger] 对话已在进行中，先停止...");
                    DialogueManager.StopConversation();
                }

                Debug.Log("[DialogueUIDebugger] 启动对话 'Midori/1'...");
                DialogueManager.StartConversation("Midori/1");

                // 延迟检查状态
                Invoke(nameof(CheckAfterStart), 0.5f);
            }
        }

        private void CheckAfterStart()
        {
            Debug.Log($"[DialogueUIDebugger] === 对话启动后状态 ===");
            Debug.Log($"[DialogueUIDebugger] isConversationActive: {DialogueManager.isConversationActive}");

            // 检查当前对话状态
            var currentConversationState = DialogueManager.currentConversationState;
            if (currentConversationState != null)
            {
                Debug.Log($"[DialogueUIDebugger] 当前对话状态存在");
                Debug.Log($"[DialogueUIDebugger] subtitle: {currentConversationState.subtitle?.formattedText?.text ?? "null"}");
                Debug.Log($"[DialogueUIDebugger] hasNPCResponse: {currentConversationState.hasNPCResponse}");
            }
            else
            {
                Debug.LogError("[DialogueUIDebugger] currentConversationState 为 null!");
            }

            var standardUI = DialogueManager.dialogueUI as StandardDialogueUI;
            if (standardUI != null)
            {
                Debug.Log($"[DialogueUIDebugger] StandardDialogueUI.isOpen: {standardUI.isOpen}");

                var mainPanel = standardUI.conversationUIElements.mainPanel;
                if (mainPanel != null)
                {
                    Debug.Log($"[DialogueUIDebugger] mainPanel active: {mainPanel.gameObject.activeInHierarchy}");

                    var canvasGroup = mainPanel.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        Debug.Log($"[DialogueUIDebugger] mainPanel CanvasGroup alpha: {canvasGroup.alpha}");
                    }

                    var animator = mainPanel.GetComponent<Animator>();
                    if (animator != null)
                    {
                        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                        Debug.Log($"[DialogueUIDebugger] Animator 当前状态 hash: {stateInfo.shortNameHash}");
                        Debug.Log($"[DialogueUIDebugger] Animator 状态名检查: Start={Animator.StringToHash("Start")}, Show={Animator.StringToHash("Show")}, Hide={Animator.StringToHash("Hide")}");
                    }
                }

                var npcPanel = standardUI.conversationUIElements.defaultNPCSubtitlePanel;
                if (npcPanel != null)
                {
                    Debug.Log($"[DialogueUIDebugger] NPC Panel active: {npcPanel.gameObject.activeInHierarchy}");
                    Debug.Log($"[DialogueUIDebugger] NPC Panel 自身 activeSelf: {npcPanel.gameObject.activeSelf}");

                    // 检查 NPC Panel 的 CanvasGroup
                    var npcCanvasGroup = npcPanel.GetComponent<CanvasGroup>();
                    if (npcCanvasGroup != null)
                    {
                        Debug.Log($"[DialogueUIDebugger] NPC Panel CanvasGroup alpha: {npcCanvasGroup.alpha}");
                    }

                    // 检查 NPC Panel 的 Animator
                    var npcAnimator = npcPanel.GetComponent<Animator>();
                    if (npcAnimator != null)
                    {
                        var npcStateInfo = npcAnimator.GetCurrentAnimatorStateInfo(0);
                        Debug.Log($"[DialogueUIDebugger] NPC Panel Animator 状态 hash: {npcStateInfo.shortNameHash}");
                    }

                    // 检查 visibility 设置
                    Debug.Log($"[DialogueUIDebugger] NPC Panel visibility: {npcPanel.visibility}");

                    // 检查父对象
                    var parent = npcPanel.transform.parent;
                    if (parent != null)
                    {
                        Debug.Log($"[DialogueUIDebugger] NPC Panel 父对象: {parent.name}, active: {parent.gameObject.activeInHierarchy}");
                    }
                }

                // 检查 subtitlePanels 列表
                var subtitlePanels = standardUI.conversationUIElements.subtitlePanels;
                Debug.Log($"[DialogueUIDebugger] subtitlePanels 数量: {subtitlePanels.Length}");
                for (int i = 0; i < subtitlePanels.Length; i++)
                {
                    var panel = subtitlePanels[i];
                    if (panel != null)
                    {
                        Debug.Log($"[DialogueUIDebugger] subtitlePanels[{i}]: {panel.name}, active: {panel.gameObject.activeInHierarchy}");
                    }
                }
            }
        }
    }
}