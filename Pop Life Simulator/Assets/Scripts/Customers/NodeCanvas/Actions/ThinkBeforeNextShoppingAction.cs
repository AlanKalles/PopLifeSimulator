using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("思考下一个购买目标：随机移动→播放思考动画→等待随机时间")]
    public class ThinkBeforeNextShoppingAction : ActionTask
    {
        [Tooltip("思考时间范围（秒）")]
        public Vector2 thinkDurationRange = new Vector2(0.25f, 0.5f);

        [Tooltip("随机移动的最大距离（格子数）")]
        public int maxMoveDistance = 5;

        [Tooltip("到达目标的停止距离")]
        public float stoppingDistance = 0.5f;

        // 组件缓存
        private FollowerEntity followerEntity;
        private AIDestinationSetter destinationSetter;
        private CustomerAnimationController animController;

        // 状态变量
        private enum ThinkState
        {
            MovingToThinkSpot,   // 移动到思考位置
            Thinking             // 正在思考（播放动画+等待）
        }

        private ThinkState currentState;
        private Vector3 thinkPosition;
        private float thinkDuration;
        private float thinkTimer;

        protected override string info
        {
            get { return "思考下一个购买目标"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            followerEntity = agent.GetComponent<FollowerEntity>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            animController = agent.GetComponent<CustomerAnimationController>();

            if (followerEntity == null)
            {
                Debug.LogError($"[ThinkBeforeNextShoppingAction] {agent.name} 缺少 FollowerEntity 组件！");
                EndAction(false);
                return;
            }

            if (animController == null)
            {
                Debug.LogError($"[ThinkBeforeNextShoppingAction] {agent.name} 缺少 CustomerAnimationController 组件！");
                EndAction(false);
                return;
            }

            // 选择随机思考位置
            thinkPosition = GetRandomThinkPosition();

            // 设置目标
            if (destinationSetter != null)
            {
                // 创建临时目标点（因为需要 Transform）
                GameObject tempTarget = new GameObject($"{agent.name}_ThinkTarget");
                tempTarget.transform.position = thinkPosition;
                destinationSetter.target = tempTarget.transform;

                // 标记为临时对象，稍后销毁
                tempTarget.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                followerEntity.destination = thinkPosition;
            }

            // 开始移动
            followerEntity.isStopped = false;
            currentState = ThinkState.MovingToThinkSpot;

            Debug.Log($"[ThinkBeforeNextShoppingAction] {agent.name} 开始移动到思考位置：{thinkPosition}");
        }

        protected override void OnUpdate()
        {
            if (followerEntity == null || animController == null)
            {
                EndAction(false);
                return;
            }

            switch (currentState)
            {
                case ThinkState.MovingToThinkSpot:
                    UpdateMoving();
                    break;

                case ThinkState.Thinking:
                    UpdateThinking();
                    break;
            }
        }

        private void UpdateMoving()
        {
            // 检查是否到达思考位置
            float distance = Vector3.Distance(agent.transform.position, thinkPosition);

            if (distance <= stoppingDistance || followerEntity.reachedDestination)
            {
                // 到达，开始思考
                followerEntity.isStopped = true;

                // 播放思考动画
                animController.PlayThink();

                // 随机思考时长
                thinkDuration = Random.Range(thinkDurationRange.x, thinkDurationRange.y);
                thinkTimer = 0f;

                currentState = ThinkState.Thinking;

                Debug.Log($"[ThinkBeforeNextShoppingAction] {agent.name} 到达思考位置，开始思考 {thinkDuration:F2} 秒");
            }
        }

        private void UpdateThinking()
        {
            // 计时
            thinkTimer += Time.deltaTime;

            if (thinkTimer >= thinkDuration)
            {
                // 思考结束，停止动画
                animController.StopLoop();

                Debug.Log($"[ThinkBeforeNextShoppingAction] {agent.name} 思考完成");

                // 完成
                EndAction(true);
            }
        }

        protected override void OnStop()
        {
            // 清理：停止动画、恢复移动
            if (animController != null)
            {
                animController.StopLoop();
            }

            if (followerEntity != null)
            {
                followerEntity.isStopped = false;
            }

            // 清理临时目标对象
            if (destinationSetter != null && destinationSetter.target != null)
            {
                if (destinationSetter.target.name.Contains("ThinkTarget"))
                {
                    GameObject.Destroy(destinationSetter.target.gameObject);
                }
            }
        }

        /// <summary>
        /// 获取随机思考位置（在当前 Graph 中）
        /// </summary>
        private Vector3 GetRandomThinkPosition()
        {
            // 获取当前位置的节点
            NNInfo currentNodeInfo = AstarPath.active.GetNearest(agent.transform.position);
            GraphNode currentNode = currentNodeInfo.node;

            if (currentNode == null)
            {
                Debug.LogWarning($"[ThinkBeforeNextShoppingAction] {agent.name} 无法获取当前节点，使用当前位置");
                return agent.transform.position;
            }

            // 尝试获取随机邻近节点
            GraphNode randomNode = GetRandomWalkableNodeNearby(currentNode, maxMoveDistance);

            if (randomNode != null)
            {
                return (Vector3)randomNode.position;
            }
            else
            {
                // 失败则原地思考
                Debug.LogWarning($"[ThinkBeforeNextShoppingAction] {agent.name} 无法找到随机位置，原地思考");
                return agent.transform.position;
            }
        }

        /// <summary>
        /// 获取附近的随机可行走节点
        /// </summary>
        private GraphNode GetRandomWalkableNodeNearby(GraphNode startNode, int maxDistance)
        {
            // 简单实现：使用 ConstantPath 获取范围内所有节点
            var path = ConstantPath.Construct(agent.transform.position, maxDistance * 1000); // 距离转换为成本
            AstarPath.StartPath(path);
            path.BlockUntilCalculated();

            if (path.allNodes != null && path.allNodes.Count > 0)
            {
                // 随机选择一个节点
                int randomIndex = Random.Range(0, path.allNodes.Count);
                GraphNode randomNode = path.allNodes[randomIndex];

                if (randomNode.Walkable)
                {
                    return randomNode;
                }
            }

            // 失败，尝试简单的邻近节点查找
            return GetRandomNeighborNode(startNode);
        }

        /// <summary>
        /// 获取随机邻近节点（简单方法）
        /// </summary>
        private GraphNode GetRandomNeighborNode(GraphNode node)
        {
            if (node == null) return null;

            // 使用 GetConnections 方法获取连接列表
            System.Collections.Generic.List<GraphNode> neighbors = new System.Collections.Generic.List<GraphNode>();
            node.GetConnections((GraphNode neighbor) => {
                if (neighbor != null && neighbor.Walkable)
                {
                    neighbors.Add(neighbor);
                }
            });

            if (neighbors.Count == 0)
            {
                return null;
            }

            // 随机选择一个可行走的邻居
            int randomIndex = Random.Range(0, neighbors.Count);
            return neighbors[randomIndex];
        }
    }
}
