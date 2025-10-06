using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("使用策略选择收银台")]
    public class SelectCashierAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<BehaviorPolicySet> policies;

        [BlackboardOnly]
        public BBParameter<string> targetCashierId;

        [BlackboardOnly]
        public BBParameter<Vector2Int> goalCell;

        protected override string info
        {
            get { return "选择收银台"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter == null)
            {
                Debug.LogError("[SelectCashierAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            // 获取策略
            var policySet = policies.value;
            if (policySet == null || policySet.checkout == null)
            {
                Debug.LogError("[SelectCashierAction] 策略集或结账策略为空");
                EndAction(false);
                return;
            }

            // 构建上下文
            var customerContext = CustomerContextBuilder.BuildCustomerContext(adapter);

            // 构建收银台快照列表
            var cashierSnapshots = CustomerContextBuilder.BuildAllCashierSnapshots();

            if (cashierSnapshots.Count == 0)
            {
                Debug.LogError($"[SelectCashierAction] 没有可用的收银台");
                EndAction(false);
                return;
            }

            int selectedIndex;

            // 【闭店逻辑】如果商店闭店，选择最近的收银台（忽略队列长度）
            if (adapter.isClosingTime)
            {
                Debug.Log($"[SelectCashierAction] 商店闭店，顾客 {adapter.customerId} 选择最近的收银台（忽略队列）");
                selectedIndex = FindNearestCashierIndex(cashierSnapshots, agent.transform.position);
            }
            else
            {
                // 正常营业：使用策略选择收银台
                selectedIndex = policySet.checkout.ChooseCashier(customerContext, cashierSnapshots);
            }

            if (selectedIndex < 0 || selectedIndex >= cashierSnapshots.Count)
            {
                // 如果策略返回无效索引，默认选择第一个
                selectedIndex = 0;
                Debug.LogWarning($"[SelectCashierAction] 策略返回无效索引，使用默认收银台");
            }

            // 设置选中的收银台
            var selectedCashier = cashierSnapshots[selectedIndex];
            targetCashierId.value = selectedCashier.cashierId;
            goalCell.value = selectedCashier.gridCell;

            // 更新 adapter
            adapter.targetCashierId = selectedCashier.cashierId;
            adapter.goalCell = selectedCashier.gridCell;

            Debug.Log($"[SelectCashierAction] 顾客 {adapter.customerId} 选择了收银台 {selectedCashier.cashierId}");

            EndAction(true);
        }

        /// <summary>
        /// 查找最近的收银台索引（用于闭店时忽略队列）
        /// </summary>
        private int FindNearestCashierIndex(System.Collections.Generic.List<CashierSnapshot> cashiers, Vector3 customerPosition)
        {
            if (cashiers == null || cashiers.Count == 0)
                return -1;

            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < cashiers.Count; i++)
            {
                // 将网格坐标转换为世界坐标（简化计算，假设每格1单位）
                Vector2 cashierPos = new Vector2(cashiers[i].gridCell.x, cashiers[i].gridCell.y);
                Vector2 customerPos2D = new Vector2(customerPosition.x, customerPosition.y);
                float distance = Vector2.Distance(customerPos2D, cashierPos);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }
    }
}