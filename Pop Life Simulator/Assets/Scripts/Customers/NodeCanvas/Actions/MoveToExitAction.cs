using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("移动到商店出口并离开（商店内部 → 外部街道）")]
    public class MoveToExitAction : ActionTask
    {
        [Tooltip("到达距离（兼容 RVO 局部避障）")]
        public float stoppingDistance = 0.8f;

        [Tooltip("超时时间（秒），超时则失败")]
        public float timeoutSeconds = 30f;

        private AILerp aiLerp;
        private Seeker seeker;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter customerBlackboard;
        private Transform targetTransform;
        private float startTime;
        private bool reachedInside = false;
        private CustomerAnimationController animController;

        protected override string info
        {
            get { return "移动到商店出口"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            aiLerp = agent.GetComponent<AILerp>();
            seeker = agent.GetComponent<Seeker>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            customerBlackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            animController = agent.GetComponent<CustomerAnimationController>();

            if (aiLerp == null)
            {
                Debug.LogError("[MoveToExitAction] 找不到 AILerp 组件");
                EndAction(false);
                return;
            }

            if (customerBlackboard == null)
            {
                Debug.LogError("[MoveToExitAction] 找不到 CustomerBlackboardAdapter 组件");
                EndAction(false);
                return;
            }

            if (animController == null)
            {
                Debug.LogError("[MoveToExitAction] 找不到 CustomerAnimationController 组件");
                EndAction(false);
                return;
            }

            // 验证出口配置
            if (customerBlackboard.entranceInsideAnchor == null)
            {
                Debug.LogError($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 没有设置 entranceInsideAnchor");
                EndAction(false);
                return;
            }

            if (customerBlackboard.entranceOutsideAnchor == null)
            {
                Debug.LogError($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 没有设置 entranceOutsideAnchor");
                EndAction(false);
                return;
            }

            // 设置目标为内部锚点（出口内侧）
            targetTransform = customerBlackboard.entranceInsideAnchor;

            // 设置移动速度
            if (customerBlackboard.moveSpeed > 0)
            {
                aiLerp.speed = customerBlackboard.moveSpeed;
            }

            // 第一阶段：保持当前 graphMask（排除外部图），在店内正常寻路到 entrance 内侧
            // graphMask 由 MoveToEntranceAction 设置为 ~outsideGraph，无需修改

            // 设置 A* 寻路目标
            if (destinationSetter != null)
            {
                destinationSetter.target = targetTransform;
            }
            else
            {
                aiLerp.destination = targetTransform.position;
            }

            // 开始移动
            aiLerp.isStopped = false;

            // 记录开始时间
            startTime = Time.time;
            reachedInside = false;

            Debug.Log($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 开始移动到出口内侧 {targetTransform.position}");
        }

        protected override void OnUpdate()
        {
            if (aiLerp == null || customerBlackboard == null)
            {
                EndAction(false);
                return;
            }

            // 检查超时
            if (Time.time - startTime > timeoutSeconds)
            {
                Debug.LogWarning($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 移动到出口超时");
                aiLerp.isStopped = true;
                EndAction(false);
                return;
            }

            // 第一阶段：到达出口内侧
            if (!reachedInside)
            {
                // 首选：AILerp 内置到达判断
                if (aiLerp.reachedDestination)
                {
                    OnReachedInsideAnchor();
                    return;
                }

                // 备选：距离判断（兼容 RVO）
                if (targetTransform != null)
                {
                    float dist = Vector3.Distance(agent.transform.position, targetTransform.position);
                    if (dist <= stoppingDistance)
                    {
                        OnReachedInsideAnchor();
                        return;
                    }
                }
            }
            // 第二阶段：穿过 NodeLink2 到达外侧
            else
            {
                // 检查是否到达外侧锚点
                if (customerBlackboard.entranceOutsideAnchor != null)
                {
                    float dist = Vector3.Distance(agent.transform.position, customerBlackboard.entranceOutsideAnchor.position);
                    if (dist <= stoppingDistance * 1.5f) // 稍微宽松一些
                    {
                        OnReachedOutsideAnchor();
                        return;
                    }
                }

                // 或者使用 AILerp 的到达判断
                if (aiLerp.reachedDestination)
                {
                    OnReachedOutsideAnchor();
                    return;
                }
            }
        }

        /// <summary>
        /// 到达出口内侧锚点后的处理
        /// </summary>
        private void OnReachedInsideAnchor()
        {
            Debug.Log($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 到达出口内侧，准备离开商店");

            // 停止移动
            aiLerp.isStopped = true;

            // 切换目标到外部锚点（A* 会自动通过 NodeLink2）
            targetTransform = customerBlackboard.entranceOutsideAnchor;

            if (destinationSetter != null)
            {
                destinationSetter.target = targetTransform;
            }
            else
            {
                aiLerp.destination = targetTransform.position;
            }

            // 第二阶段：切换到 everything，允许通过 NodeLink2 离开商店
            if (seeker != null)
            {
                seeker.graphMask = GraphMask.everything;
            }

            // 恢复移动（A* 会自动通过 NodeLink2）
            aiLerp.isStopped = false;

            // 标记已到达内侧
            reachedInside = true;

            Debug.Log($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 开始穿过出口到外部，graphMask 切换到外部图形");
        }

        /// <summary>
        /// 到达出口外侧锚点后的处理
        /// </summary>
        private void OnReachedOutsideAnchor()
        {
            Debug.Log($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 已离开商店到达外部街道");

            // 停止移动
            aiLerp.isStopped = true;

            // 标记已离开商店
            customerBlackboard.hasEnteredStore = false;

            // 更新黑板的队列位置（用于后续的 MoveToTargetAction）
            customerBlackboard.assignedQueueSlot = targetTransform;

            // 离开商店：切换所有部件 + emoji 到 OutsideStoreLayer
            if (animController != null)
            {
                animController.SetAllSortingLayer("OutsideStoreLayer");
                Debug.Log($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 离开商店，sortingLayer 切换到 OutsideStoreLayer");
            }

            Debug.Log($"[MoveToExitAction] 顾客 {customerBlackboard.customerId} 离店完成，准备前往最终撤离点");

            EndAction(true);
        }

        protected override void OnStop()
        {
            if (aiLerp != null)
            {
                aiLerp.isStopped = true;
            }
        }
    }
}
