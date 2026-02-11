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

        // 全局注册表：通过 NodeLink2 查找对应的 teleporter
        private static readonly Dictionary<NodeLink2, AILerpLinkTeleporter> registry = new();

        private enum Phase { Approaching, Hidden }

        private struct AgentState
        {
            public SpriteRenderer sprite;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public Phase phase;
        }

        private readonly Dictionary<Transform, AgentState> trackedAgents = new();
        private readonly List<Transform> toRemove = new();
        private readonly List<Transform> keyBuffer = new();

        void Awake() => nodeLink = GetComponent<NodeLink2>();

        void OnEnable() => registry[nodeLink] = this;

        void OnDisable()
        {
            // 恢复所有被隐藏的 agent
            foreach (var kvp in trackedAgents)
                if (kvp.Value.sprite != null)
                    SetAlpha(kvp.Value.sprite, 1f);

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
        public void RegisterAgent(Transform agent, SpriteRenderer sprite, Vector3 startPos, Vector3 endPos)
        {
            if (!hideOnTraversal || sprite == null) return;

            trackedAgents[agent] = new AgentState
            {
                sprite = sprite,
                startPosition = startPos,
                endPosition = endPos,
                phase = Phase.Approaching
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

            if (state.phase == Phase.Hidden && state.sprite != null)
                SetAlpha(state.sprite, 1f);

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
                    toRemove.Add(agent);
                    continue;
                }

                var pos = agent.position;

                if (state.phase == Phase.Approaching)
                {
                    // 等待 agent 物理到达链接起点才隐藏
                    if ((pos - state.startPosition).sqrMagnitude <= thresholdSqr)
                    {
                        SetAlpha(state.sprite, 0f);
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
                        SetAlpha(state.sprite, 1f);
                        toRemove.Add(agent);

                        if (debugMode)
                            Debug.Log($"[LinkTeleporter] {agent.name} 到达终点，恢复", this);
                    }
                }
            }

            foreach (var key in toRemove)
                trackedAgents.Remove(key);
        }

        private static void SetAlpha(SpriteRenderer sr, float alpha)
        {
            if (sr == null) return;
            var c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
