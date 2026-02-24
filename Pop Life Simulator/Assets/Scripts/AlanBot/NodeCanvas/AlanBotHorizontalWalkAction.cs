using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot行为树节点：水平随机移动
    /// 使用ConstantPath获取范围内节点，过滤同Y高度节点，随机选择目标
    /// </summary>
    [Category("PopLife/AlanBot")]
    [Description("AlanBot水平随机移动")]
    public class AlanBotHorizontalWalkAction : ActionTask
    {
        [Tooltip("ConstantPath最大水平搜索距离（格数）")]
        public int maxHorizontalDistance = 8;
        [Tooltip("到达目标的判定距离")]
        public float stoppingDistance = 0.5f;
        [Tooltip("移动超时时间（秒）")]
        public float timeout = 10f;
        [Tooltip("过滤同高度节点的Y容差")]
        public float yTolerance = 0.3f;

        private AlanBotController controller;
        private AlanBotAnimator animator;
        private AILerp aiLerp;

        private GameObject tempTarget;
        private float timer;
        private bool reachedDestination;
        private bool waitingForFinishWalk;

        protected override string info => "AlanBot水平移动";

        protected override void OnExecute()
        {
            controller = agent.GetComponent<AlanBotController>();
            animator = agent.GetComponent<AlanBotAnimator>();
            aiLerp = agent.GetComponent<AILerp>();

            if (controller == null || animator == null || aiLerp == null)
            {
                EndAction(false);
                return;
            }

            timer = 0f;
            reachedDestination = false;
            waitingForFinishWalk = false;

            // 使用ConstantPath获取范围内所有可达节点
            Vector3 currentPos = agent.transform.position;
            float currentY = currentPos.y;

            var path = ConstantPath.Construct(currentPos, maxHorizontalDistance * 1000);
            AstarPath.StartPath(path);
            path.BlockUntilCalculated();

            if (path.allNodes == null || path.allNodes.Count == 0)
            {
                EndAction(true); // 无可达节点，跳过
                return;
            }

            // 过滤：只保留同图形 + 同Y高度的节点
            var candidates = new System.Collections.Generic.List<GraphNode>();
            foreach (var node in path.allNodes)
            {
                if (node.GraphIndex != controller.boundGraphIndex) continue;
                if (!node.Walkable) continue;

                Vector3 nodePos = (Vector3)node.position;
                if (Mathf.Abs(nodePos.y - currentY) > yTolerance) continue;

                // 排除距离太近的节点
                float dist = Mathf.Abs(nodePos.x - currentPos.x);
                if (dist < stoppingDistance) continue;

                candidates.Add(node);
            }

            if (candidates.Count == 0)
            {
                EndAction(true); // 无合适目标，跳过
                return;
            }

            // 随机选择目标
            GraphNode targetNode = candidates[Random.Range(0, candidates.Count)];
            Vector3 targetPos = (Vector3)targetNode.position;

            // 创建临时目标GameObject
            tempTarget = new GameObject("AlanBotWalkTarget");
            tempTarget.transform.position = targetPos;

            // 设置AILerp目标
            aiLerp.destination = targetPos;
            aiLerp.isStopped = false;
            aiLerp.SearchPath();

            // 开始行走动画
            animator.SetWalking(true);
        }

        protected override void OnUpdate()
        {
            if (controller == null || aiLerp == null)
            {
                EndAction(false);
                return;
            }

            // 如果正在交互，暂停计时（BT已暂停，但以防万一）
            if (controller.isInteracting) return;

            // 等待移动动画回到第一帧
            if (waitingForFinishWalk)
            {
                if (!animator.IsFinishingWalk)
                {
                    CleanupTempTarget();
                    EndAction(true);
                }
                return;
            }

            timer += Time.deltaTime;

            // 检查到达
            if (tempTarget != null)
            {
                float dist = Vector3.Distance(agent.transform.position, tempTarget.transform.position);
                if (dist <= stoppingDistance)
                {
                    reachedDestination = true;
                }
            }

            // 检查到达或超时
            if (reachedDestination || timer >= timeout)
            {
                aiLerp.isStopped = true;
                animator.SetWalking(false); // 进入FinishingWalk
                waitingForFinishWalk = true;
            }
        }

        protected override void OnStop()
        {
            // 清理
            if (aiLerp != null)
                aiLerp.isStopped = true;

            CleanupTempTarget();
        }

        private void CleanupTempTarget()
        {
            if (tempTarget != null)
            {
                Object.Destroy(tempTarget);
                tempTarget = null;
            }
        }
    }
}
