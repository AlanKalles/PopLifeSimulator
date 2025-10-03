using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("使用 A* Pathfinding (FollowerEntity) 移动到目标位置")]
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

        private FollowerEntity followerEntity;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter blackboard;

        protected override string info
        {
            get { return "移动到队列位置"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            followerEntity = agent.GetComponent<FollowerEntity>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (followerEntity == null)
            {
                Debug.LogError("[MoveToTargetAction] 找不到 FollowerEntity 组件");
                EndAction(false);
                return;
            }

            // 设置移动速度
            if (moveSpeed.value > 0)
            {
                followerEntity.maxSpeed = moveSpeed.value;
            }
            else if (blackboard != null && blackboard.moveSpeed > 0)
            {
                followerEntity.maxSpeed = blackboard.moveSpeed;
            }

            // 获取目标 Transform（真实坐标法）
            Transform targetTransform = assignedQueueSlot.value;

            if (targetTransform == null)
            {
                Debug.LogError($"[MoveToTargetAction] 顾客 {blackboard?.customerId} 没有分配队列位置");
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
                followerEntity.destination = targetTransform.position;
            }

            // 开始移动
            followerEntity.isStopped = false;
            hasReachedTarget.value = false;

            Debug.Log($"[MoveToTargetAction] 顾客 {blackboard?.customerId} 开始移动到 {targetTransform.position}");
        }

        protected override void OnUpdate()
        {
            if (followerEntity == null)
            {
                EndAction(false);
                return;
            }

            // 【调试】每帧输出状态
            var target = assignedQueueSlot.value;
            float dist = target != null ? Vector3.Distance(agent.transform.position, target.position) : -1f;
            Debug.Log($"[MoveToTargetAction] OnUpdate - Customer {blackboard?.customerId}:\n" +
                      $"  - Distance to target: {dist:F2}m\n" +
                      $"  - followerEntity.reachedDestination: {followerEntity.reachedDestination}\n" +
                      $"  - followerEntity.isStopped: {followerEntity.isStopped}\n" +
                      $"  - stoppingDistance: {stoppingDistance}");

            // 首选：FollowerEntity 内置到达判断
            if (followerEntity.reachedDestination)
            {
                hasReachedTarget.value = true;
                Debug.Log($"[MoveToTargetAction] 顾客 {blackboard?.customerId} 到达目标 (reachedDestination = true)");

                // 停止移动
                followerEntity.isStopped = true;

                EndAction(true);
                return;
            }

            // 兼容 RVO 的距离容忍度检查
            if (target != null)
            {
                if (dist <= stoppingDistance)
                {
                    hasReachedTarget.value = true;
                    followerEntity.isStopped = true;
                    Debug.Log($"[MoveToTargetAction] 顾客 {blackboard?.customerId} 到达目标 (distance {dist:F2}m <= {stoppingDistance}m)");
                    EndAction(true);
                    return;
                }
            }
        }

        protected override void OnStop()
        {
            if (followerEntity != null)
            {
                followerEntity.isStopped = true;
            }
        }
    }
}

