using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

namespace PopLife.Runtime
{
    /// <summary>
    /// 挂载在 NodeLink2 物体上。
    /// 当 AILerp agent 穿越此链接时，可选择隐藏其 sprite，
    /// 到达链接终点后恢复可见。
    ///
    /// 配合 LinkTraversalDetector（挂载在 agent 上）使用。
    /// </summary>
    [RequireComponent(typeof(NodeLink2))]
    public class AILerpLinkTeleporter : MonoBehaviour
    {
        [Tooltip("穿越此链接时是否隐藏 agent 的 sprite")]
        [SerializeField] private bool hideOnTraversal = true;

        [Tooltip("判定到达起点/终点的距离阈值")]
        [SerializeField] private float arrivalThreshold = 0.5f;

        [Header("调试")]
        [SerializeField] private bool debugMode;

        private NodeLink2 nodeLink;

        // 电梯门引用（方向无关，由 ResolveDoors 根据行进方向判断入口/出口）
        private ElevatorDoorInstance doorA;
        private ElevatorDoorInstance doorB;

        /// <summary> 设置电梯门引用，由 ElevatorLinkManager 在创建连接时调用 </summary>
        public void SetDoors(ElevatorDoorInstance a, ElevatorDoorInstance b)
        {
            doorA = a;
            doorB = b;
        }

        // 全局注册表：通过 NodeLink2 查找对应的 teleporter
        private static readonly Dictionary<NodeLink2, AILerpLinkTeleporter> registry = new();

        private enum Phase { Approaching, Hidden }

        private struct AgentState
        {
            public SpriteRenderer[] sprites;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public Phase phase;
            public ElevatorDoorInstance entryDoor;
            public ElevatorDoorInstance exitDoor;
        }

        private readonly Dictionary<Transform, AgentState> trackedAgents = new();
        private readonly List<Transform> toRemove = new();
        private readonly List<Transform> keyBuffer = new();

        void Awake() => nodeLink = GetComponent<NodeLink2>();

        void OnEnable() => registry[nodeLink] = this;

        void OnDisable()
        {
            // 恢复所有被隐藏的 agent 并回收门计数
            foreach (var kvp in trackedAgents)
            {
                SetAlphaAll(kvp.Value.sprites, 1f);
                CleanupDoorTraversals(kvp.Value);
            }

            trackedAgents.Clear();
            registry.Remove(nodeLink);
        }

        /// <summary>
        /// 静态查询：通过 NodeLink2 获取对应的 teleporter 配置。
        /// 找不到返回 null（该链接无隐身效果）。
        /// </summary>
        public static AILerpLinkTeleporter GetTeleporter(NodeLink2 link)
        {
            registry.TryGetValue(link, out var t);
            return t;
        }

        /// <summary>
        /// 由 LinkTraversalDetector 调用：
        /// agent 的路径经过此链接，注册为"正在接近"状态。
        /// 只有当 agent 物理到达起点时才会隐藏。
        /// </summary>
        public void RegisterAgent(Transform agent, SpriteRenderer[] sprites, Vector3 startPos, Vector3 endPos)
        {
            if (!hideOnTraversal || sprites == null || sprites.Length == 0) return;

            // 根据行进方向确定入口/出口门
            // 使用 Anchor.position（实际寻路锚点），不是 transform.position（prefab pivot）
            ElevatorDoorInstance entryDoor = null, exitDoor = null;
            if (doorA != null && doorB != null)
            {
                float distA = (startPos - doorA.Anchor.position).sqrMagnitude;
                float distB = (startPos - doorB.Anchor.position).sqrMagnitude;
                entryDoor = distA < distB ? doorA : doorB;
                exitDoor = distA < distB ? doorB : doorA;
            }

            trackedAgents[agent] = new AgentState
            {
                sprites = sprites,
                startPosition = startPos,
                endPosition = endPos,
                phase = Phase.Approaching,
                entryDoor = entryDoor,
                exitDoor = exitDoor
            };

            if (debugMode)
                Debug.Log($"[LinkTeleporter] 注册 {agent.name}: {startPos} → {endPos}", this);
        }

        /// <summary>
        /// 由 LinkTraversalDetector 调用：
        /// agent 获得新路径或被销毁时，取消注册并恢复可见。
        /// </summary>
        public void UnregisterAgent(Transform agent)
        {
            if (!trackedAgents.TryGetValue(agent, out var state)) return;

            if (state.phase == Phase.Hidden)
                SetAlphaAll(state.sprites, 1f);

            // 回收电梯门计数（防止异常退出导致门永久打开）
            CleanupDoorTraversals(state);

            trackedAgents.Remove(agent);

            if (debugMode)
                Debug.Log($"[LinkTeleporter] 取消注册 {agent.name}", this);
        }

        void Update()
        {
            if (trackedAgents.Count == 0) return;

            float thresholdSqr = arrivalThreshold * arrivalThreshold;
            toRemove.Clear();

            // 复制 keys 到缓冲区，避免遍历中修改字典导致 InvalidOperationException
            keyBuffer.Clear();
            keyBuffer.AddRange(trackedAgents.Keys);

            foreach (var agent in keyBuffer)
            {
                var state = trackedAgents[agent];

                if (agent == null)
                {
                    // agent 被销毁（如关店超时强制清除），回收门计数
                    CleanupDoorTraversals(state);
                    toRemove.Add(agent);
                    continue;
                }

                var pos = agent.position;

                if (state.phase == Phase.Approaching)
                {
                    // 等待 agent 物理到达链接起点才隐藏
                    if ((pos - state.startPosition).sqrMagnitude <= thresholdSqr)
                    {
                        // 入口门开门
                        state.entryDoor?.OpenDoor();

                        SetAlphaAll(state.sprites, 0f);
                        state.phase = Phase.Hidden;
                        trackedAgents[agent] = state;

                        if (debugMode)
                            Debug.Log($"[LinkTeleporter] {agent.name} 到达起点，隐藏", this);
                    }
                }
                else
                {
                    // 等待 agent 物理到达链接终点才恢复
                    if ((pos - state.endPosition).sqrMagnitude <= thresholdSqr)
                    {
                        // 出口门开门
                        state.exitDoor?.OpenDoor();

                        SetAlphaAll(state.sprites, 1f);
                        toRemove.Add(agent);

                        // 通行完成，通知两端门
                        state.entryDoor?.NotifyTraversalComplete();
                        state.exitDoor?.NotifyTraversalComplete();

                        if (debugMode)
                            Debug.Log($"[LinkTeleporter] {agent.name} 到达终点，恢复", this);
                    }
                }
            }

            foreach (var key in toRemove)
                trackedAgents.Remove(key);
        }

        /// <summary>
        /// 回收电梯门的 activeTraversals 计数。
        /// 根据 agent 已经到达的阶段决定通知哪些门。
        /// </summary>
        private static void CleanupDoorTraversals(AgentState state)
        {
            // Approaching 阶段：还没开过任何门，不需要回收
            // Hidden 阶段：入口门已 OpenDoor()，需要通知入口门
            if (state.phase == Phase.Hidden)
            {
                state.entryDoor?.NotifyTraversalComplete();
            }
            // 注意：正常完成时 entryDoor 和 exitDoor 都在 Update 中已调用
            // NotifyTraversalComplete，这里只处理异常退出
        }

        private static void SetAlphaAll(SpriteRenderer[] renderers, float alpha)
        {
            if (renderers == null) return;
            foreach (var sr in renderers)
            {
                if (sr == null) continue;
                var c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }
    }
}
