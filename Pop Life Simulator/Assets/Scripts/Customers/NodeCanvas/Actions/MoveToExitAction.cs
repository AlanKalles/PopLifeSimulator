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

        private FollowerEntity followerEntity;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter blackboard;
        private Transform targetTransform;
        private float startTime;
        private bool reachedInside = false;
        private SpriteRenderer spriteRenderer;

        protected override string info
        {
            get { return "移动到商店出口"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            followerEntity = agent.GetComponent<FollowerEntity>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            spriteRenderer = agent.GetComponent<SpriteRenderer>();

            if (followerEntity == null)
            {
                Debug.LogError("[MoveToExitAction] 找不到 FollowerEntity 组件");
                EndAction(false);
                return;
            }

            if (blackboard == null)
            {
                Debug.LogError("[MoveToExitAction] 找不到 CustomerBlackboardAdapter 组件");
                EndAction(false);
                return;
            }

            if (spriteRenderer == null)
            {
                Debug.LogError("[MoveToExitAction] 找不到 SpriteRenderer 组件");
                EndAction(false);
                return;
            }

            // 验证出口配置
            if (blackboard.entranceInsideAnchor == null)
            {
                Debug.LogError($"[MoveToExitAction] 顾客 {blackboard.customerId} 没有设置 entranceInsideAnchor");
                EndAction(false);
                return;
            }

            if (blackboard.entranceOutsideAnchor == null)
            {
                Debug.LogError($"[MoveToExitAction] 顾客 {blackboard.customerId} 没有设置 entranceOutsideAnchor");
                EndAction(false);
                return;
            }

            // 设置目标为内部锚点（出口内侧）
            targetTransform = blackboard.entranceInsideAnchor;

            // 设置移动速度
            if (blackboard.moveSpeed > 0)
            {
                followerEntity.maxSpeed = blackboard.moveSpeed;
            }

            // 允许使用所有图形，让 A* 通过 NodeLink2 自动选择路径
            followerEntity.pathfindingSettings.graphMask = GraphMask.everything;

            // 设置 A* 寻路目标
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

            // 记录开始时间
            startTime = Time.time;
            reachedInside = false;

            Debug.Log($"[MoveToExitAction] 顾客 {blackboard.customerId} 开始移动到出口内侧 {targetTransform.position}");
        }

        protected override void OnUpdate()
        {
            if (followerEntity == null || blackboard == null)
            {
                EndAction(false);
                return;
            }

            // 检查超时
            if (Time.time - startTime > timeoutSeconds)
            {
                Debug.LogWarning($"[MoveToExitAction] 顾客 {blackboard.customerId} 移动到出口超时");
                followerEntity.isStopped = true;
                EndAction(false);
                return;
            }

            // 第一阶段：到达出口内侧
            if (!reachedInside)
            {
                // 首选：FollowerEntity 内置到达判断
                if (followerEntity.reachedDestination)
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
                if (blackboard.entranceOutsideAnchor != null)
                {
                    float dist = Vector3.Distance(agent.transform.position, blackboard.entranceOutsideAnchor.position);
                    if (dist <= stoppingDistance * 1.5f) // 稍微宽松一些
                    {
                        OnReachedOutsideAnchor();
                        return;
                    }
                }

                // 或者使用 FollowerEntity 的到达判断
                if (followerEntity.reachedDestination)
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
            Debug.Log($"[MoveToExitAction] 顾客 {blackboard.customerId} 到达出口内侧，准备离开商店");

            // 停止移动
            followerEntity.isStopped = true;

            // 切换目标到外部锚点（A* 会自动通过 NodeLink2）
            targetTransform = blackboard.entranceOutsideAnchor;

            if (destinationSetter != null)
            {
                destinationSetter.target = targetTransform;
            }
            else
            {
                followerEntity.destination = targetTransform.position;
            }

            // 保持允许所有图形（已经离开商店，graphMask 保持为 -1）
            // graphMask 保持为 -1，无需改变
            // 恢复移动（A* 会自动通过 NodeLink2）
            followerEntity.isStopped = false;

            // 标记已到达内侧
            reachedInside = true;

            Debug.Log($"[MoveToExitAction] 顾客 {blackboard.customerId} 开始穿过出口到外部，graphMask 切换到外部图形");
        }

        /// <summary>
        /// 到达出口外侧锚点后的处理
        /// </summary>
        private void OnReachedOutsideAnchor()
        {
            Debug.Log($"[MoveToExitAction] 顾客 {blackboard.customerId} 已离开商店到达外部街道");

            // 停止移动
            followerEntity.isStopped = true;

            // 标记已离开商店
            blackboard.hasEnteredStore = false;

            // 更新黑板的队列位置（用于后续的 MoveToTargetAction）
            blackboard.assignedQueueSlot = targetTransform;

            // 离开商店：切换到 OutsideStoreLayer（sorting order 不变）
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = "OutsideStoreLayer";
                Debug.Log($"[MoveToExitAction] 顾客 {blackboard.customerId} 离开商店，sortingLayer 切换到 OutsideStoreLayer");
            }

            Debug.Log($"[MoveToExitAction] 顾客 {blackboard.customerId} 离店完成，准备前往最终撤离点");

            EndAction(true);
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
