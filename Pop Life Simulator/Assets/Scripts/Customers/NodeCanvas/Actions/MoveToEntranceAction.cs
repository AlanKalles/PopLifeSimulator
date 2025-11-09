using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("移动到商店入口并进入（外部街道 → 商店内部）")]
    public class MoveToEntranceAction : ActionTask
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
        private SpriteRenderer spriteRenderer;

        protected override string info
        {
            get { return "移动到商店入口"; }
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
                Debug.LogError("[MoveToEntranceAction] 找不到 FollowerEntity 组件");
                EndAction(false);
                return;
            }

            if (blackboard == null)
            {
                Debug.LogError("[MoveToEntranceAction] 找不到 CustomerBlackboardAdapter 组件");
                EndAction(false);
                return;
            }

            if (spriteRenderer == null)
            {
                Debug.LogError("[MoveToEntranceAction] 找不到 SpriteRenderer 组件");
                EndAction(false);
                return;
            }

            // 验证入口配置
            if (blackboard.entranceOutsideAnchor == null)
            {
                Debug.LogError($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 没有设置 entranceOutsideAnchor");
                EndAction(false);
                return;
            }

            if (blackboard.entranceInsideAnchor == null)
            {
                Debug.LogError($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 没有设置 entranceInsideAnchor");
                EndAction(false);
                return;
            }

            // 设置目标为外部锚点
            targetTransform = blackboard.entranceOutsideAnchor;

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

            Debug.Log($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 开始移动到入口外侧 {targetTransform.position}");
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
                Debug.LogWarning($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 移动到入口超时");
                followerEntity.isStopped = true;
                EndAction(false);
                return;
            }

            // 首选：FollowerEntity 内置到达判断
            if (followerEntity.reachedDestination)
            {
                OnReachedEntrance();
                return;
            }

            // 备选：距离判断（兼容 RVO）
            if (targetTransform != null)
            {
                float dist = Vector3.Distance(agent.transform.position, targetTransform.position);
                if (dist <= stoppingDistance)
                {
                    OnReachedEntrance();
                    return;
                }
            }
        }

        /// <summary>
        /// 到达入口外侧后的处理
        /// </summary>
        private void OnReachedEntrance()
        {
            Debug.Log($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 到达入口外侧，准备进入商店");

            // 停止移动
            followerEntity.isStopped = true;

            // 切换目标到内部锚点（A* 会自动通过 NodeLink2）
            targetTransform = blackboard.entranceInsideAnchor;

            if (destinationSetter != null)
            {
                destinationSetter.target = targetTransform;
            }
            else
            {
                followerEntity.destination = targetTransform.position;
            }

            // 保持允许所有图形（已经进入商店，后续可以在任何楼层移动）
            // graphMask 保持为 -1，无需改变

            // 恢复移动（A* 会自动通过 NodeLink2）
            followerEntity.isStopped = false;

            // 标记已进入商店
            blackboard.hasEnteredStore = true;

            // 更新黑板的队列位置（用于后续的 MoveToTargetAction）
            blackboard.assignedQueueSlot = targetTransform;

            // 进入商店：切换到 InsideStoreLayer（sorting order 不变）
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingLayerName = "InsideStoreLayer";

                // 同步emoji子对象的sorting layer
                SpriteRenderer emojiRenderer = agent.transform.Find("emoji")?.GetComponent<SpriteRenderer>();
                if (emojiRenderer != null)
                {
                    emojiRenderer.sortingLayerName = "InsideStoreLayer";
                }

                Debug.Log($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 进入商店，sortingLayer 切换到 InsideStoreLayer");
            }

            Debug.Log($"[MoveToEntranceAction] 顾客 {blackboard.customerId} 已进入商店，graphMask 切换到内部图形");

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
