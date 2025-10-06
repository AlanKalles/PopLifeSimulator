using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("强制退出购物循环并清理状态")]
    public class ForceExitShoppingLoopAction : ActionTask
    {
        protected override string info
        {
            get { return "Force Exit Shopping Loop"; }
        }

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[ForceExitShoppingLoop] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            Debug.Log($"[ForceExitShoppingLoop] Customer {blackboard.customerId} 开始清理购物状态");

            // 1. 停止移动
            var follower = agent.GetComponent<FollowerEntity>();
            if (follower != null)
            {
                follower.isStopped = true;
                Debug.Log($"[ForceExitShoppingLoop] 停止移动");
            }

            // 2. 释放货架队列位置
            if (!string.IsNullOrEmpty(blackboard.targetShelfId) && blackboard.assignedQueueSlot != null)
            {
                bool released = ReleaseShelfQueue(blackboard.targetShelfId, blackboard.customerId);
                if (released)
                {
                    Debug.Log($"[ForceExitShoppingLoop] 释放货架 {blackboard.targetShelfId} 的队列位置");
                }
            }

            // 3. 清空货架相关变量
            blackboard.targetShelfId = string.Empty;
            blackboard.assignedQueueSlot = null;
            blackboard.purchaseQuantity = 0;
            blackboard.goalCell = Vector2Int.zero;

            // 4. 同步到 NodeCanvas 黑板
#if NODECANVAS
            if (blackboard.ncBlackboard != null)
            {
                blackboard.ncBlackboard.SetVariableValue("targetShelfId", string.Empty);
                blackboard.ncBlackboard.SetVariableValue("assignedQueueSlot", null);
                blackboard.ncBlackboard.SetVariableValue("purchaseQuantity", 0);
                blackboard.ncBlackboard.SetVariableValue("goalCell", Vector2Int.zero);
            }
#endif

            Debug.Log($"[ForceExitShoppingLoop] Customer {blackboard.customerId} 购物状态清理完成");
            EndAction(true);
        }

        /// <summary>
        /// 释放货架队列
        /// </summary>
        private bool ReleaseShelfQueue(string shelfId, string customerId)
        {
            if (string.IsNullOrEmpty(shelfId))
                return false;

            var shelf = FindShelfById(shelfId);
            if (shelf == null)
            {
                Debug.LogWarning($"[ForceExitShoppingLoop] 找不到货架 {shelfId}");
                return false;
            }

            var queueController = shelf.GetComponent<ShelfQueueController>();
            if (queueController == null)
            {
                Debug.LogWarning($"[ForceExitShoppingLoop] 货架 {shelfId} 没有 ShelfQueueController");
                return false;
            }

            queueController.ReleaseSlot(customerId);
            return true;
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
    }
}
