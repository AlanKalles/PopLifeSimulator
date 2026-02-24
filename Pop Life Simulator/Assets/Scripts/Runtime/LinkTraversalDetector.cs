using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

namespace PopLife.Runtime
{
    /// <summary>
    /// 挂载在 AILerp agent 上的极薄桥接组件。
    /// 监听路径回调，检测路径中的 NodeLink2，
    /// 通知对应的 AILerpLinkTeleporter 进行隐身管理。
    /// </summary>
    [RequireComponent(typeof(Seeker))]
    public class LinkTraversalDetector : MonoBehaviour
    {
        private Seeker seeker;
        private SpriteRenderer[] allRenderers;
        private readonly List<AILerpLinkTeleporter> registeredTeleporters = new();

        void Awake()
        {
            seeker = GetComponent<Seeker>();
            // 收集所有子级 SpriteRenderer（6个部件 + emoji）
            allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        void OnEnable() => seeker.postProcessPath += OnPathComplete;

        void OnDisable()
        {
            seeker.postProcessPath -= OnPathComplete;
            UnregisterAll();
        }

        private void OnPathComplete(Path p)
        {
            // 新路径到达，先清除旧的注册
            UnregisterAll();

            if (p == null || p.error || p.path == null || p.path.Count < 2) return;

            for (int i = 0; i < p.path.Count - 1; i++)
            {
                if (p.path[i] is LinkNode startNode && p.path[i + 1] is LinkNode endNode)
                {
                    var nodeLink = NodeLink2.GetNodeLink(startNode);
                    if (nodeLink != null)
                    {
                        var teleporter = AILerpLinkTeleporter.GetTeleporter(nodeLink);
                        if (teleporter != null)
                        {
                            teleporter.RegisterAgent(
                                transform,
                                allRenderers,
                                (Vector3)startNode.position,
                                (Vector3)endNode.position
                            );
                            registeredTeleporters.Add(teleporter);
                        }
                    }
                    i++; // 跳过 endNode
                }
            }
        }

        private void UnregisterAll()
        {
            foreach (var t in registeredTeleporters)
                if (t != null) t.UnregisterAgent(transform);
            registeredTeleporters.Clear();
        }
    }
}
