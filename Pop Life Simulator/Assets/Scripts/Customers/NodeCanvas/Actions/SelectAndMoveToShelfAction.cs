using System.Collections.Generic;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using PopLife.Customers.Runtime;
using PopLife.Runtime;
using PopLife.UI;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("使用策略选择货架并移动到该位置（集成版本）")]
    public class SelectAndMoveToShelfAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<BehaviorPolicySet> policies;

        [BlackboardOnly]
        public BBParameter<string> targetShelfId;

        [BlackboardOnly]
        public BBParameter<Vector2Int> goalCell;

        [BlackboardOnly]
        public BBParameter<float> moveSpeed;

        public float stoppingDistance = 0.5f;

        private CustomerBlackboardAdapter blackboard;
        private FollowerEntity followerEntity;
        private AIDestinationSetter destinationSetter;
        private CustomerInteraction interaction;
        private FloorManager floorManager;

        private Vector3 targetPosition;
        private bool isMoving = false;

        protected override string info
        {
            get { return "选择并移动到货架"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            followerEntity = agent.GetComponent<FollowerEntity>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            interaction = agent.GetComponent<CustomerInteraction>();

            // 获取 FloorManager
            if (floorManager == null)
            {
                floorManager = Object.FindFirstObjectByType<FloorManager>(FindObjectsInactive.Exclude);
            }

            if (blackboard == null || followerEntity == null)
            {
                Debug.LogError("[SelectAndMoveToShelfAction] 缺少必要组件");
                EndAction(false);
                return;
            }

            // 步骤1：选择目标货架
            if (!SelectTargetShelf())
            {
                string msg = "Failed to select target shelf";
                Debug.LogWarning($"[SelectAndMoveToShelfAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogWarning(blackboard.customerId, msg);
                EndAction(false);
                return;
            }

            // 步骤2：开始移动
            StartMovement();
        }

        private bool SelectTargetShelf()
        {
            var policySet = policies.value;
            if (policySet == null || policySet.targetSelector == null)
            {
                Debug.LogError("[SelectAndMoveToShelfAction] 策略集或目标选择策略为空");
                return false;
            }

            // 构建上下文和货架列表
            var customerContext = CustomerContextBuilder.BuildCustomerContext(blackboard);
            var shelfSnapshots = CustomerContextBuilder.BuildAllShelfSnapshots();

            if (shelfSnapshots.Count == 0)
            {
                Debug.LogWarning("[SelectAndMoveToShelfAction] 没有可用货架");
                return false;
            }

            // 使用策略选择
            int selectedIndex = policySet.targetSelector.SelectTargetShelf(customerContext, shelfSnapshots);

            if (selectedIndex < 0 || selectedIndex >= shelfSnapshots.Count)
            {
                return false;
            }

            // 设置目标
            var selectedShelf = shelfSnapshots[selectedIndex];
            targetShelfId.value = selectedShelf.shelfId;
            goalCell.value = selectedShelf.gridCell;

            // 同步到黑板
            blackboard.targetShelfId = selectedShelf.shelfId;
            blackboard.goalCell = selectedShelf.gridCell;

            string msg = $"Selected shelf {selectedShelf.shelfId} at position {selectedShelf.gridCell}";
            Debug.Log($"[SelectAndMoveToShelfAction] Customer {blackboard.customerId} {msg}");
            ScreenLogger.LogCustomerAction(blackboard.customerId, msg);

            return true;
        }

        private void StartMovement()
        {
            // 设置移动速度
            if (moveSpeed.value > 0)
            {
                followerEntity.maxSpeed = moveSpeed.value;
            }
            else if (blackboard.moveSpeed > 0)
            {
                followerEntity.maxSpeed = blackboard.moveSpeed;
            }

            // 转换目标位置（使用目标货架所在楼层的坐标系统）
            targetPosition = GridToWorldPosition(goalCell.value, targetShelfId.value);

            // 设置 A* 目标
            if (destinationSetter != null)
            {
                GameObject targetGO = GetOrCreateTargetObject();
                targetGO.transform.position = targetPosition;
                destinationSetter.target = targetGO.transform;
            }
            else
            {
                followerEntity.destination = targetPosition;
            }

            followerEntity.isStopped = false;
            isMoving = true;

            string msg = $"Started moving to {goalCell.value}";
            Debug.Log($"[SelectAndMoveToShelfAction] Customer {blackboard.customerId} {msg}");
            ScreenLogger.LogCustomerAction(blackboard.customerId, msg);
        }

        protected override void OnUpdate()
        {
            if (!isMoving || followerEntity == null)
            {
                EndAction(false);
                return;
            }

            // 检查是否已经通过碰撞到达目标
            if (interaction != null && interaction.IsInteracting)
            {
                var currentShelf = interaction.GetCurrentShelf();
                if (currentShelf != null && currentShelf.instanceId == targetShelfId.value)
                {
                    string msg = "Arrived at shelf via collision";
                    Debug.Log($"[SelectAndMoveToShelfAction] Customer {blackboard.customerId} {msg}");
                    ScreenLogger.LogCustomerAction(blackboard.customerId, msg);
                    followerEntity.isStopped = true;
                    EndAction(true);
                    return;
                }
            }

            // 检查是否到达目标（使用 FollowerEntity 内置属性）
            if (followerEntity.reachedDestination)
            {
                string msg = "Arrived at target position";
                Debug.Log($"[SelectAndMoveToShelfAction] Customer {blackboard.customerId} {msg}");
                ScreenLogger.LogCustomerAction(blackboard.customerId, msg);
                followerEntity.isStopped = true;

                // 手动检查交互（以防碰撞器没有触发）
                if (interaction != null)
                {
                    interaction.CheckForInteractables();
                }

                EndAction(true);
                return;
            }

            // FollowerEntity 会自动管理路径重计算
            // 可选：根据策略重新考虑目标（保留原有逻辑）
            if (ShouldReconsiderTarget())
            {
                Debug.Log($"[SelectAndMoveToShelfAction] 顾客 {blackboard.customerId} 重新考虑目标");
                if (SelectTargetShelf())
                {
                    StartMovement();
                }
            }

            // 可选：检查是否速度异常
            float distance = Vector3.Distance(agent.transform.position, targetPosition);
            if (followerEntity.velocity.magnitude < 0.1f && distance > stoppingDistance * 2)
            {
                Debug.LogWarning($"[SelectAndMoveToShelfAction] 顾客 {blackboard.customerId} 移动速度异常慢");
            }
        }

        protected override void OnStop()
        {
            if (followerEntity != null)
            {
                followerEntity.isStopped = true;
            }
            isMoving = false;
        }

        private bool ShouldReconsiderTarget()
        {
            // 使用 PathPolicy 决定是否重新考虑目标
            var policySet = policies.value;
            if (policySet != null && policySet.path != null)
            {
                var customerContext = CustomerContextBuilder.BuildCustomerContext(blackboard);
                float distLeft = Vector3.Distance(agent.transform.position, targetPosition);
                // 注意：FollowerEntity 自动管理 repath，这里的时间检查仅用于决策重选目标
                return policySet.path.ShouldRepath(customerContext, Time.time, distLeft);
            }

            return false;
        }

        private Vector3 GridToWorldPosition(Vector2Int gridPos, string targetId = null)
        {
            // 尝试通过目标ID找到对应的楼层
            if (!string.IsNullOrEmpty(targetId))
            {
                // 查找目标货架
                var shelf = FindShelfById(targetId);
                if (shelf != null && floorManager != null)
                {
                    var floorGrid = floorManager.GetFloor(shelf.floorId);
                    if (floorGrid != null)
                    {
                        return floorGrid.GridToWorld(gridPos);
                    }
                }
            }

            // 备用：使用当前活跃楼层
            if (floorManager != null)
            {
                var activeFloor = floorManager.GetActiveFloor();
                if (activeFloor != null)
                {
                    return activeFloor.GridToWorld(gridPos);
                }
            }

            // 最后备用：直接转换
            return new Vector3(gridPos.x, gridPos.y, 0);
        }

        private GameObject GetOrCreateTargetObject()
        {
            string targetName = $"Target_{blackboard.customerId}";
            GameObject targetGO = GameObject.Find(targetName);

            if (targetGO == null)
            {
                targetGO = new GameObject(targetName);
                targetGO.tag = "CustomerTarget";
            }

            return targetGO;
        }

        private ShelfInstance FindShelfById(string shelfId)
        {
            if (string.IsNullOrEmpty(shelfId)) return null;

            var allShelves = Object.FindObjectsByType<ShelfInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var shelf in allShelves)
            {
                if (shelf.instanceId == shelfId)
                    return shelf;
            }

            return null;
        }
    }
}
