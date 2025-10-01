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
        public BBParameter<Vector2Int> goalCell;

        [BlackboardOnly]
        public BBParameter<float> moveSpeed;

        [BlackboardOnly]
        public BBParameter<bool> hasReachedTarget;

        public float stoppingDistance = 0.5f;

        private FollowerEntity followerEntity;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter blackboard;
        private FloorManager floorManager;

        private Vector3 targetPosition;

        protected override string info
        {
            get { return $"移动到 {goalCell}"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            followerEntity = agent.GetComponent<FollowerEntity>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            // 获取 FloorManager
            if (floorManager == null)
            {
                floorManager = Object.FindFirstObjectByType<FloorManager>(FindObjectsInactive.Exclude);
            }

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

            // 转换网格坐标到世界坐标
            targetPosition = GridToWorldPosition(goalCell.value, blackboard.targetShelfId);

            // 如果有 AIDestinationSetter，使用它（FollowerEntity 也支持）
            if (destinationSetter != null)
            {
                GameObject targetGO = GetOrCreateTargetObject();
                targetGO.transform.position = targetPosition;
                destinationSetter.target = targetGO.transform;
            }
            else
            {
                // 直接设置 FollowerEntity 的目标（不需要手动 SearchPath）
                followerEntity.destination = targetPosition;
            }

            hasReachedTarget.value = false;

            Debug.Log($"[MoveToTargetAction] 顾客 {blackboard.customerId} 开始移动到 {goalCell.value}");
        }

        protected override void OnUpdate()
        {
            if (followerEntity == null)
            {
                EndAction(false);
                return;
            }

            // 检查是否到达目标（使用 FollowerEntity 的内置属性）
            if (followerEntity.reachedDestination)
            {
                hasReachedTarget.value = true;
                Debug.Log($"[MoveToTargetAction] 顾客 {blackboard.customerId} 到达目标");

                // 停止移动
                followerEntity.isStopped = true;

                EndAction(true);
                return;
            }

            // FollowerEntity 会自动重新计算路径，不需要手动干预
            // 它有内置的自动 repath 机制

            // 可选：检查是否卡住（FollowerEntity 通常能自行处理）
            float distance = Vector3.Distance(agent.transform.position, targetPosition);
            if (followerEntity.velocity.magnitude < 0.1f && distance > stoppingDistance * 2)
            {
                Debug.LogWarning($"[MoveToTargetAction] 顾客 {blackboard.customerId} 移动速度异常慢");
            }
        }

        protected override void OnStop()
        {
            if (followerEntity != null)
            {
                followerEntity.isStopped = true;
            }
        }

        /// <summary>
        /// 将网格坐标转换为世界坐标
        /// </summary>
        private Vector3 GridToWorldPosition(Vector2Int gridPos, string targetId = null)
        {
            // 尝试通过目标ID找到对应的楼层
            if (!string.IsNullOrEmpty(targetId))
            {
                // 如果是货架ID
                var shelf = FindShelfById(targetId);
                if (shelf != null && floorManager != null)
                {
                    var floorGrid = floorManager.GetFloor(shelf.floorId);
                    if (floorGrid != null)
                    {
                        return floorGrid.GridToWorld(gridPos);
                    }
                }

                // 如果是收银台ID
                var facility = FindFacilityById(targetId);
                if (facility != null && floorManager != null)
                {
                    var floorGrid = floorManager.GetFloor(facility.floorId);
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

            // 最后备用：直接转换（假设网格单位为1）
            return new Vector3(gridPos.x, gridPos.y, 0);
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
                if (shelf.instanceId == shelfId) return shelf;
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
                if (facility.instanceId == facilityId) return facility;
            }
            return null;
        }

        /// <summary>
        /// 获取或创建目标对象（供 AIDestinationSetter 使用）
        /// </summary>
        private GameObject GetOrCreateTargetObject()
        {
            // 查找或创建一个目标对象
            string targetName = $"Target_{blackboard.customerId}";
            GameObject targetGO = GameObject.Find(targetName);

            if (targetGO == null)
            {
                targetGO = new GameObject(targetName);
                targetGO.tag = "CustomerTarget";
            }

            return targetGO;
        }
    }
}
