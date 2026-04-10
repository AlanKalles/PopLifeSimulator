using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("使用 A* Pathfinding (AILerp) 移动到目标位置")]
    public class MoveToTargetAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("分配的队列位置信息 Transform（由 AcquireQueueSlotAction 分配）")]
        public BBParameter<Transform> assignedQueueSlot;

        [BlackboardOnly]
        public BBParameter<float> moveSpeed;

        [BlackboardOnly]
        public BBParameter<bool> hasReachedTarget;

        [Tooltip("到达距离（兼容 RVO 局部避障）")]
        public float stoppingDistance = 0.8f;

        private AILerp aiLerp;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter customerBlackboard;
        private bool waitingForNewPath; // 等待新路径计算完成，防止 reachedDestination 误判

        protected override string info
        {
            get { return "移动到队列位置"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            aiLerp = agent.GetComponent<AILerp>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            customerBlackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (aiLerp == null)
            {
                Debug.LogError("[MoveToTargetAction] 找不到 AILerp 组件");
                EndAction(false);
                return;
            }

            // 设置移动速度
            if (moveSpeed.value > 0)
            {
                aiLerp.speed = moveSpeed.value;
            }
            else if (customerBlackboard != null && customerBlackboard.moveSpeed > 0)
            {
                aiLerp.speed = customerBlackboard.moveSpeed;
            }

            // 获取目标 Transform（真实坐标法）
            Transform targetTransform = assignedQueueSlot.value;

            if (targetTransform == null)
            {
                Debug.LogError($"[MoveToTargetAction] 顾客 {customerBlackboard?.customerId} 没有分配队列位置");
                EndAction(false);
                return;
            }

            // 设置 A* 寻路目标（直接使用 Transform 引用）
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
            hasReachedTarget.value = false;

            // 强制请求新路径，并标记等待状态
            // 防止 OnUpdate 第一帧读到上一次寻路的 reachedDestination=true 而瞬间完成
            aiLerp.SearchPath();
            waitingForNewPath = true;

            Debug.Log($"[MoveToTargetAction] 顾客 {customerBlackboard?.customerId} 开始移动到 {targetTransform.position}");
        }

        protected override void OnUpdate()
        {
            if (aiLerp == null)
            {
                EndAction(false);
                return;
            }

            // 等待新路径计算完成，防止 reachedDestination 误判
            if (waitingForNewPath)
            {
                if (aiLerp.pathPending)
                    return; // 路径仍在计算中，继续等待
                waitingForNewPath = false;
            }

            // 电梯穿越中不做到达判断
            var detector = agent.GetComponent<LinkTraversalDetector>();
            if (detector != null && detector.IsTraversingElevator) return;

            // 兼容 RVO 的距离容忍度检查（优先于 reachedDestination，更可靠）
            var target = assignedQueueSlot.value;
            if (target != null)
            {
                float dist = Vector3.Distance(agent.transform.position, target.position);
                if (dist <= stoppingDistance)
                {
                    hasReachedTarget.value = true;
                    aiLerp.isStopped = true;
                    Debug.Log($"[MoveToTargetAction] 顾客 {customerBlackboard?.customerId} 到达目标 (distance {dist:F2}m <= {stoppingDistance}m)");
                    EndAction(true);
                    return;
                }
            }

            // AILerp 内置到达判断
            if (aiLerp.reachedDestination)
            {
                hasReachedTarget.value = true;
                Debug.Log($"[MoveToTargetAction] 顾客 {customerBlackboard?.customerId} 到达目标 (reachedDestination = true)");

                // 停止移动
                aiLerp.isStopped = true;

                EndAction(true);
                return;
            }
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

