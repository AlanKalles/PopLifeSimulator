using System.Collections.Generic;
using NodeCanvas.DialogueTrees;
using PopLife;
using PopLife.Manager;
using UnityEngine;

namespace Poplife.Dialogue
{
    /// <summary>
    /// Tutorial dialogue manager using marker-based triggering system
    /// 基于标记触发的教程对话管理器
    /// </summary>
    public class TutorialDialogueManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool enableTutorial = true;

        // 标记 → 对话列表的映射
        private Dictionary<TutorialMarker, List<DialogueEvent>> markerDialogueMap
            = new Dictionary<TutorialMarker, List<DialogueEvent>>();

        private List<DialogueEvent> completedEvents = new List<DialogueEvent>();

        private void Start()
        {
            if (!enableTutorial)
            {
                Debug.Log("[Tutorial] Tutorial disabled");
                return;
            }

            // 注册教程对话
            RegisterTutorialDialogues();

            // 订阅标记事件
            TutorialEventBus.OnMarkerTriggered += OnMarkerTriggered;

            Debug.Log("[Tutorial] Tutorial dialogue manager initialized");
        }

        private void OnDestroy()
        {
            // 取消订阅
            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;
        }

        private void RegisterTutorialDialogues()
        {
            // D001: 游戏开始时触发 - 屏幕中央
            AddEvent("D001", "Meet Midori",
                TutorialMarker.GameStarted,
                new List<string>(),
                new DialogueUIConfig("Tutorial", Vector2.zero, new Vector2(0.5f, 0.5f)));

            // D002: 首次进入建造模式时触发 - 左下角
            AddEvent("D002", "Build Tutorial",
                TutorialMarker.FirstBuildPhaseEntered,
                new List<string> { "Blueprint:B001", "Blueprint:B002" },
                new DialogueUIConfig("Tutorial", new Vector2(100, 100), new Vector2(0f, 0f)));

            // D003: 放置2个货架时触发 - 右下角
            AddEvent("D003", "Open Store",
                TutorialMarker.TwoShelvesPlaced,
                new List<string> { "Customer:C001", "Customer:C002", "Customer:C003" },
                new DialogueUIConfig("Tutorial", new Vector2(-100, 100), new Vector2(1f, 0f)));

            // D004: 商店开张时触发 - 屏幕顶部
            AddEvent("D004", "First Customer",
                TutorialMarker.StoreOpened,
                new List<string> { "Fame:500" },
                new DialogueUIConfig("Tutorial", new Vector2(0, -100), new Vector2(0.5f, 1f)));

            // D005: 首次获得声望时触发 - 屏幕中央（稍大）
            AddEvent("D005", "Fame System",
                TutorialMarker.FirstFameEarned,
                new List<string> { "Blueprint:R004", "Blueprint:R005" },
                new DialogueUIConfig("Tutorial", Vector2.zero, new Vector2(0.5f, 0.5f), 1.2f));

            Debug.Log($"[Tutorial] Registered {markerDialogueMap.Count} tutorial dialogues");
        }

        private void AddEvent(string code, string name, TutorialMarker marker, List<string> rewards, DialogueUIConfig uiConfig = null)
        {
            var tree = Resources.Load<DialogueTree>($"DialogueTreeControllers/{code}");
            if (tree == null)
            {
                Debug.LogWarning($"[Tutorial] Dialogue tree {code} not found in Resources/DialogueTreeControllers/");
                return;
            }

            var dialogueEvent = new DialogueEvent(tree, code, name, rewards, uiConfig);
            dialogueEvent.OnDialogueCompleted += OnDialogueCompleted;

            // 添加到标记映射
            if (!markerDialogueMap.ContainsKey(marker))
            {
                markerDialogueMap[marker] = new List<DialogueEvent>();
            }
            markerDialogueMap[marker].Add(dialogueEvent);

            Debug.Log($"[Tutorial] Registered dialogue '{name}' for marker '{marker}' with UI config");
        }

        private void OnMarkerTriggered(TutorialMarker marker)
        {
            Debug.Log($"[Tutorial] Marker triggered: {marker}");

            // 查找并触发对应的对话
            if (markerDialogueMap.ContainsKey(marker))
            {
                foreach (var dialogue in markerDialogueMap[marker])
                {
                    if (!dialogue.IsTriggered)
                    {
                        dialogue.ForceTrigger();
                        Debug.Log($"[Tutorial] Triggered dialogue: {dialogue.GetDialogueCode()}");
                    }
                }
            }
        }

        private void OnDialogueCompleted(DialogueEvent evt)
        {
            Debug.Log($"[Tutorial] Dialogue completed: {evt.GetDialogueCode()}");

            // 添加到已完成列表
            if (!completedEvents.Contains(evt))
            {
                completedEvents.Add(evt);
            }

            // 发放奖励
            GrantRewards(evt.GetRewards());
        }

        private void GrantRewards(List<string> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return;

            foreach (var reward in rewards)
            {
                string[] parts = reward.Split(':');
                string rewardType = parts[0];
                string rewardValue = parts.Length > 1 ? parts[1] : "";

                switch (rewardType)
                {
                    case "Blueprint":
                        Debug.Log($"[Tutorial] Granted blueprint: {rewardValue}");
                        // TODO: Integrate with BlueprintManager when implemented
                        // BlueprintManager.Instance.UnlockBlueprint(rewardValue);
                        break;

                    case "Customer":
                        Debug.Log($"[Tutorial] Unlocked customer: {rewardValue}");
                        // TODO: Integrate with SpawnerProfile
                        // SpawnerProfile.UnlockCustomer(rewardValue);
                        break;

                    case "Fame":
                        if (int.TryParse(rewardValue, out int fameAmount))
                        {
                            ResourceManager.Instance.AddFame(fameAmount);
                            Debug.Log($"[Tutorial] Granted fame: {fameAmount}");
                        }
                        break;

                    case "Money":
                        if (int.TryParse(rewardValue, out int moneyAmount))
                        {
                            ResourceManager.Instance.AddMoney(moneyAmount);
                            Debug.Log($"[Tutorial] Granted money: {moneyAmount}");
                        }
                        break;

                    default:
                        Debug.LogWarning($"[Tutorial] Unknown reward type: {rewardType}");
                        break;
                }
            }
        }

        /// <summary>
        /// Manually trigger a dialogue by code (for testing)
        /// </summary>
        [ContextMenu("Trigger D001")]
        public void TriggerD001()
        {
            TriggerDialogueByCode("D001");
        }

        public void TriggerDialogueByCode(string code)
        {
            foreach (var dialogues in markerDialogueMap.Values)
            {
                foreach (var dialogue in dialogues)
                {
                    if (dialogue.GetDialogueCode() == code && !dialogue.IsTriggered)
                    {
                        dialogue.ForceTrigger();
                        Debug.Log($"[Tutorial] Manually triggered dialogue: {code}");
                        return;
                    }
                }
            }

            Debug.LogWarning($"[Tutorial] Dialogue {code} not found or already completed");
        }

        /// <summary>
        /// Reset tutorial progress (for testing)
        /// </summary>
        [ContextMenu("Reset Tutorial")]
        public void ResetTutorial()
        {
            // Clear all dialogues
            markerDialogueMap.Clear();
            completedEvents.Clear();

            // Reset marker system
            TutorialEventBus.ResetAllMarkers();

            // Reset game state
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.ResetTutorialStates();
            }

            // Re-register dialogues
            RegisterTutorialDialogues();

            Debug.Log("[Tutorial] Tutorial reset complete");
        }
    }
}
