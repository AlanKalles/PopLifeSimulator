using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Data;
using PopLife.Quest;
using PopLife.Runtime;
using PopLife.UI;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("顾客交互事件：在货架前掷骰 → 播放交互动画 → 等待玩家点击 → 对话 → 发放奖励")]
    public class TryCustomerInteractionAction : ActionTask
    {
        // ── 内部状态机 ──
        private enum Phase
        {
            WaitForClick,     // 播放 emoji 动画，等待玩家点击或超时
            WaitForDialogue,  // 等待对话完成
            HandleResult      // 处理结果，发放奖励
        }

        [UnityEngine.Header("黑板绑定")]
        public BBParameter<string> targetShelfId;

        // ── 运行时缓存 ──
        private Phase currentPhase;
        private CustomerBlackboardAdapter adapter;
        private CustomerAnimationController animController;
        private InteractionEventData currentEvent;
        private float phaseTimer;

        protected override string info => "Try Customer Interaction";

        // ─────────────────────────────────────────────
        //  OnExecute：掷骰检查 + 开始等待点击
        // ─────────────────────────────────────────────
        protected override void OnExecute()
        {
            adapter = agent.GetComponent<CustomerBlackboardAdapter>();

            // 今日已触发过 → 立即通过
            if (adapter == null || adapter.interactionUsedToday)
            {
                EndAction(true);
                return;
            }

            // 查找当前货架
            var shelf = FindShelfById(targetShelfId.value);
            if (shelf == null)
            {
                EndAction(true);
                return;
            }

            // 掷骰 + 匹配事件
            var rolledEvent = InteractionEventService.Instance?.TryRollInteraction(shelf);
            if (rolledEvent == null)
            {
                EndAction(true);
                return;
            }

            currentEvent = rolledEvent;
            adapter.assignedInteraction = rolledEvent;

            // 获取动画控制器
            animController = agent.GetComponent<CustomerAnimationController>();
            if (animController == null)
            {
                adapter.assignedInteraction = null;
                EndAction(true);
                return;
            }

            // ── 进入 Phase: WaitForClick ──
            currentPhase = Phase.WaitForClick;
            phaseTimer = 0f;
            adapter.interactionBubbleActive = true;

            // 播放交互 emoji 循环动画（多帧精灵循环）
            animController.PlayInteraction();

            Debug.Log($"[TryCustomerInteraction] 顾客 {adapter.customerId} " +
                      $"在货架 {targetShelfId.value} 触发交互事件 {currentEvent.eventId}");
        }

        // ─────────────────────────────────────────────
        //  OnUpdate：状态机逐帧推进
        // ─────────────────────────────────────────────
        protected override void OnUpdate()
        {
            switch (currentPhase)
            {
                case Phase.WaitForClick:    UpdateWaitForClick();    break;
                case Phase.WaitForDialogue: UpdateWaitForDialogue(); break;
                case Phase.HandleResult:    HandleResult();          break;
            }
        }

        // ── Phase: WaitForClick ──
        private void UpdateWaitForClick()
        {
            phaseTimer += Time.deltaTime;

            // 检查玩家是否点击了（由 CustomerDialogueTrigger 设置此标志）
            if (adapter.interactionDialogueStarted)
            {
                // 立即隐藏气泡 + 阻止重复点击
                animController.StopLoop();
                adapter.interactionBubbleActive = false;

                // 初始化对话结果变量（兜底：对话被 ESC 关闭时默认 false）
                DialogueLua.SetVariable("InteractionCorrect", false);

                // ── 进入 Phase: WaitForDialogue ──
                currentPhase = Phase.WaitForDialogue;

                Debug.Log($"[TryCustomerInteraction] 顾客 {adapter.customerId} 对话已开始");
                return;
            }

            // 超时检查
            if (phaseTimer >= currentEvent.BubbleTimeout)
            {
                Debug.Log($"[TryCustomerInteraction] 顾客 {adapter.customerId} " +
                          $"交互气泡超时（{currentEvent.BubbleTimeout}s）");
                animController.StopLoop();
                CleanupAndEnd();
            }
        }

        // ── Phase: WaitForDialogue ──
        private void UpdateWaitForDialogue()
        {
            // 等待 Dialogue System 对话结束
            if (!DialogueManager.isConversationActive)
            {
                currentPhase = Phase.HandleResult;
            }
        }

        // ── Phase: HandleResult ──
        private void HandleResult()
        {
            // 读取 Lua 变量判断是否回答正确
            bool correct = DialogueLua.GetVariable("InteractionCorrect").asBool;

            if (correct && currentEvent.Rewards != null && currentEvent.Rewards.Length > 0)
            {
                // 发放奖励
                foreach (var reward in currentEvent.Rewards)
                {
                    QuestRewardDistributor.DistributeSingleReward(reward);

                    // 奖励视觉反馈
                    if (FloatingTextSpawner.Instance != null)
                    {
                        switch (reward.type)
                        {
                            case RewardType.Money:
                                FloatingTextSpawner.Instance.SpawnIncomeText(
                                    agent.transform.position, reward.amount);
                                break;
                            case RewardType.Fame:
                                FloatingTextSpawner.Instance.SpawnFameText(
                                    agent.transform.position, reward.amount);
                                break;
                        }
                    }
                }

                // 播放奖励音效
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound(AudioKeys.CUSTOMER_CHECKOUT);
                }

                Debug.Log($"[TryCustomerInteraction] 顾客 {adapter.customerId} " +
                          $"交互正确，发放 {currentEvent.Rewards.Length} 项奖励");
            }
            else
            {
                Debug.Log($"[TryCustomerInteraction] 顾客 {adapter.customerId} " +
                          $"交互未正确回答 (correct={correct})");
            }

            CleanupAndEnd();
        }

        // ─────────────────────────────────────────────
        //  CleanupAndEnd：正常完成时的清理
        //  标记 interactionUsedToday = true（消耗今日机会）
        // ─────────────────────────────────────────────
        private void CleanupAndEnd()
        {
            if (adapter != null)
            {
                adapter.assignedInteraction = null;
                adapter.interactionBubbleActive = false;
                adapter.interactionDialogueStarted = false;
                adapter.interactionUsedToday = true; // 正常完成：消耗今日机会
            }

            if (animController != null)
                animController.StopLoop();

            EndAction(true); // 始终返回 true，不阻断 StepIterator
        }

        // ─────────────────────────────────────────────
        //  OnStop：行为树中断时的兜底清理
        //  不标记 interactionUsedToday（中断不算消耗机会）
        // ─────────────────────────────────────────────
        protected override void OnStop()
        {
            // 停止 emoji 循环动画
            if (animController != null)
                animController.StopLoop();

            // 重置标志位（但不标记 interactionUsedToday）
            if (adapter != null)
            {
                adapter.interactionBubbleActive = false;
                adapter.interactionDialogueStarted = false;
                // 注意：不设 interactionUsedToday = true
                // 行为树中断（商店关门/顾客愤怒离开）不消耗交互机会
            }
        }

        // ─────────────────────────────────────────────
        //  根据ID查找货架（同 ExecutePurchaseAction 模式）
        // ─────────────────────────────────────────────
        private ShelfInstance FindShelfById(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId))
                return null;

            var allShelves = Object.FindObjectsByType<ShelfInstance>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var shelf in allShelves)
            {
                if (shelf.instanceId == shelfId)
                    return shelf;
            }

            return null;
        }
    }
}
