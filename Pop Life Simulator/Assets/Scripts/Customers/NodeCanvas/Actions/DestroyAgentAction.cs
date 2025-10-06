using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("销毁顾客GameObject - 用于离店")]
    public class DestroyAgentAction : ActionTask
    {
        [Tooltip("延迟销毁时间（秒）")]
        public float delay = 0f;

        protected override string info
        {
            get { return "销毁顾客"; }
        }

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            var customerAgent = agent.GetComponent<CustomerAgent>();

            if (blackboard != null)
            {
                Debug.Log($"[DestroyAgentAction] 顾客 {blackboard.customerId} 离店");

                // 强制释放所有队列位置（防止队列泄漏）
                if (!string.IsNullOrEmpty(blackboard.targetShelfId))
                {
                    ReleaseShelfQueue(blackboard.targetShelfId, blackboard.customerId);
                }

                if (!string.IsNullOrEmpty(blackboard.targetCashierId))
                {
                    ReleaseCashierQueue(blackboard.targetCashierId, blackboard.customerId);
                }
            }

            // 触发销毁事件
            if (customerAgent != null)
            {
                CustomerEventBus.RaiseCustomerDestroyed(customerAgent);
            }

            if (delay > 0)
            {
                GameObject.Destroy(agent.gameObject, delay);
            }
            else
            {
                GameObject.Destroy(agent.gameObject);
            }

            EndAction(true);
        }

        /// <summary>
        /// 释放货架队列
        /// </summary>
        private void ReleaseShelfQueue(string shelfId, string customerId)
        {
            if (string.IsNullOrEmpty(shelfId))
                return;

            var shelf = FindShelfById(shelfId);
            if (shelf == null)
                return;

            var queueController = shelf.GetComponent<ShelfQueueController>();
            if (queueController != null)
            {
                queueController.ReleaseSlot(customerId);
                Debug.Log($"[DestroyAgentAction] 释放货架 {shelfId} 的队列位置");
            }
        }

        /// <summary>
        /// 释放收银台队列
        /// </summary>
        private void ReleaseCashierQueue(string cashierId, string customerId)
        {
            if (string.IsNullOrEmpty(cashierId))
                return;

            var cashier = FindFacilityById(cashierId);
            if (cashier == null)
                return;

            var queueController = cashier.GetComponent<CashierQueueController>();
            if (queueController != null)
            {
                queueController.ReleaseSlot(customerId);
                Debug.Log($"[DestroyAgentAction] 释放收银台 {cashierId} 的队列位置");
            }
        }

        /// <summary>
        /// 根据ID查找货架
        /// </summary>
        private ShelfInstance FindShelfById(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId))
                return null;

            var shelves = Object.FindObjectsByType<ShelfInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var shelf in shelves)
            {
                if (shelf.instanceId == shelfId)
                    return shelf;
            }

            return null;
        }

        /// <summary>
        /// 根据ID查找设施
        /// </summary>
        private FacilityInstance FindFacilityById(string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId))
                return null;

            var facilities = Object.FindObjectsByType<FacilityInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var facility in facilities)
            {
                if (facility.instanceId == facilityId)
                    return facility;
            }

            return null;
        }
    }
}
