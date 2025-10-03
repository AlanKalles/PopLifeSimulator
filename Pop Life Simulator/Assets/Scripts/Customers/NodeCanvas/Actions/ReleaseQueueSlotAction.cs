using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer/Queue")]
    [Description("释放队列位置（购买完成或离开时调用）")]
    public class ReleaseQueueSlotAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("目标货架ID")]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        [Tooltip("目标收银台ID")]
        public BBParameter<string> targetCashierId;

        private CustomerBlackboardAdapter blackboard;

        protected override string info
        {
            get { return "释放队列位置"; }
        }

        protected override void OnExecute()
        {
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[ReleaseQueueSlotAction] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            bool released = false;

            // 优先释放货架队列
            if (!string.IsNullOrEmpty(targetShelfId.value))
            {
                released = ReleaseShelfQueueSlot(targetShelfId.value);
            }
            // 其次释放收银台队列
            else if (!string.IsNullOrEmpty(targetCashierId.value))
            {
                released = ReleaseCashierQueueSlot(targetCashierId.value);
            }
            else
            {
                Debug.LogWarning($"[ReleaseQueueSlotAction] 顾客 {blackboard.customerId} 没有指定目标");
                EndAction(true); // 即便没有目标也算成功（可能是系统状态）
                return;
            }

            if (released)
            {
                // 清空黑板中的队位引用
                blackboard.assignedQueueSlot = null;
                Debug.Log($"[ReleaseQueueSlotAction] 顾客 {blackboard.customerId} 成功释放队位");
            }

            EndAction(true); // 释放操作总是返回成功
        }

        /// <summary>
        /// 释放货架队列位置
        /// </summary>
        private bool ReleaseShelfQueueSlot(string shelfId)
        {
            var shelf = FindShelfById(shelfId);
            if (shelf == null)
            {
                Debug.LogWarning($"[ReleaseQueueSlotAction] 找不到货架 {shelfId}");
                return false;
            }

            var queueController = shelf.GetComponent<ShelfQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[ReleaseQueueSlotAction] 货架 {shelfId} 没有 ShelfQueueController");
                return false;
            }

            queueController.ReleaseSlot(blackboard.customerId);
            return true;
        }

        /// <summary>
        /// 释放收银台队列位置
        /// </summary>
        private bool ReleaseCashierQueueSlot(string cashierId)
        {
            var cashier = FindFacilityById(cashierId);
            if (cashier == null)
            {
                Debug.LogWarning($"[ReleaseQueueSlotAction] 找不到收银台 {cashierId}");
                return false;
            }

            var queueController = cashier.GetComponent<CashierQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[ReleaseQueueSlotAction] 收银台 {cashierId} 没有 CashierQueueController");
                return false;
            }

            queueController.ReleaseSlot(blackboard.customerId);
            return true;
        }

        /// <summary>
        /// 根据ID查找货架
        /// </summary>
        private ShelfInstance FindShelfById(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId)) return null;

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
            if (string.IsNullOrEmpty(facilityId)) return null;

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
