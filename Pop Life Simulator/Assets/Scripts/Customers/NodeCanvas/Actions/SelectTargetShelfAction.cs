using System.Collections.Generic;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("使用策略选择目标货架并设置到黑板")]
    public class SelectTargetShelfAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<BehaviorPolicySet> policies;

        [BlackboardOnly]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        public BBParameter<Vector2Int> goalCell;

        protected override string info
        {
            get { return "选择目标货架"; }
        }

        protected override void OnExecute()
        {
            // 诊断：unconditional log 确认 OnExecute 是否真的被 BT 调用
            // 如果开店期间从未看到此行，说明 NodeCanvas 根本没触发这个 action
            Debug.Log($"[SelectTargetShelfAction.entry] OnExecute fired (agent={agent?.name})");

            // 获取 CustomerBlackboardAdapter
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[SelectTargetShelfAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            // 【闭店检查】如果商店闭店，停止寻找新货架，退出购物循环
            if (adapter.isClosingTime)
            {
                Debug.Log($"[SelectTargetShelfAction] 商店已闭店，顾客 {adapter.customerId} 停止购物，准备结账 (待结账: ${adapter.pendingPayment})");

                // 清空目标货架ID，确保行为树退出购物循环
                targetShelfId.value = string.Empty;
                adapter.targetShelfId = string.Empty;

                EndAction(false); // 返回 Failure 触发 Repeater 退出循环
                return;
            }

            // 获取策略集
            var policySet = policies.value;
            if (policySet == null || policySet.targetSelector == null)
            {
                Debug.LogError("[SelectTargetShelfAction] 策略集或目标选择策略为空");
                EndAction(false);
                return;
            }

            // 构建顾客上下文
            var customerContext = CustomerContextBuilder.BuildCustomerContext(adapter);

            // 构建货架快照列表
            var shelfSnapshots = CustomerContextBuilder.BuildAllShelfSnapshots(adapter.destinationStoreId);

            if (shelfSnapshots.Count == 0)
            {
                // 商店里完全没有货架，直接标记 upset
                adapter.isUpset = true;
                Debug.LogWarning($"[SelectTargetShelfAction] 顾客 {adapter.customerId} 找不到任何货架，进入 upset 状态！");

                EndAction(false);
                return;
            }

            // 使用策略选择目标
            int selectedIndex = policySet.targetSelector.SelectTargetShelf(customerContext, shelfSnapshots);

            if (selectedIndex < 0 || selectedIndex >= shelfSnapshots.Count)
            {
                // 当前漏斗阶段未找到匹配货架，返回 Failure 让行为树推进到下一阶段
                // 不设置 isUpset —— 漏斗系统中单个阶段失败是正常的
                Debug.Log($"[SelectTargetShelfAction] 顾客 {adapter.customerId} 阶段 {adapter.currentFunnelPhase} 未找到匹配货架，跳过当前阶段。");

                // 清空目标货架ID
                targetShelfId.value = string.Empty;
                adapter.targetShelfId = string.Empty;

                EndAction(false);
                return;
            }

            // 设置选中的目标
            var selectedShelf = shelfSnapshots[selectedIndex];
            targetShelfId.value = selectedShelf.shelfId;
            goalCell.value = selectedShelf.gridCell;

            // 更新 adapter 中的目标信息
            adapter.targetShelfId = selectedShelf.shelfId;
            adapter.goalCell = selectedShelf.gridCell;

            // 记录命中的漏斗层级
            adapter.lastSelectionTier = adapter.currentFunnelPhase;

            Debug.Log($"[SelectTargetShelfAction] 顾客 {adapter.customerId} 选择了货架 {selectedShelf.shelfId} (阶段: {adapter.currentFunnelPhase})");

            EndAction(true);
        }
    }
}
