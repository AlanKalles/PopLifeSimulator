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
            // 获取 CustomerBlackboardAdapter
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[SelectTargetShelfAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
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
            var shelfSnapshots = CustomerContextBuilder.BuildAllShelfSnapshots();

            if (shelfSnapshots.Count == 0)
            {
                Debug.LogWarning($"[SelectTargetShelfAction] 顾客 {adapter.customerId} 找不到可用货架");
                EndAction(false);
                return;
            }

            // 使用策略选择目标
            int selectedIndex = policySet.targetSelector.SelectTargetShelf(customerContext, shelfSnapshots);

            if (selectedIndex < 0 || selectedIndex >= shelfSnapshots.Count)
            {
                Debug.LogWarning($"[SelectTargetShelfAction] 顾客 {adapter.customerId} 策略返回无效索引 (索引: {selectedIndex}, 货架数量: {shelfSnapshots.Count})。" +
                                 "可能原因: 所有货架已购买/库存不足/兴趣过滤。顾客将跳过购物环节,直接前往收银台。");

                // 清空目标货架ID,确保行为树跳过购物环节
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

            Debug.Log($"[SelectTargetShelfAction] 顾客 {adapter.customerId} 选择了货架 {selectedShelf.shelfId}");

            EndAction(true);
        }
    }
}