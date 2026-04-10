using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

namespace PopLife.Runtime
{
    /// <summary>
    /// 挂载在电梯 NodeLink2 物体上。
    /// 只负责：门引用存储、静态注册表、方向判断。
    /// 实际穿越逻辑由 LinkTraversalDetector（挂在 agent 上）通过协程执行。
    /// </summary>
    [RequireComponent(typeof(NodeLink2))]
    public class AILerpLinkTeleporter : MonoBehaviour
    {
        private NodeLink2 nodeLink;
        private ElevatorDoorInstance doorA;
        private ElevatorDoorInstance doorB;

        // 全局注册表：通过 NodeLink2 查找对应的 teleporter
        private static readonly Dictionary<NodeLink2, AILerpLinkTeleporter> registry = new();

        void Awake() => nodeLink = GetComponent<NodeLink2>();
        void OnEnable() => registry[nodeLink] = this;
        void OnDisable() => registry.Remove(nodeLink);

        /// <summary> 静态查询：通过 NodeLink2 获取对应的 teleporter。找不到返回 null。 </summary>
        public static AILerpLinkTeleporter GetTeleporter(NodeLink2 link)
        {
            registry.TryGetValue(link, out var t);
            return t;
        }

        /// <summary> 设置电梯门引用，由 ElevatorLinkManager 在创建连接时调用 </summary>
        public void SetDoors(ElevatorDoorInstance a, ElevatorDoorInstance b)
        {
            doorA = a;
            doorB = b;
        }

        public ElevatorDoorInstance DoorA => doorA;
        public ElevatorDoorInstance DoorB => doorB;

        /// <summary> 根据 agent 起点位置判断入口/出口门 </summary>
        public (ElevatorDoorInstance entry, ElevatorDoorInstance exit) ResolveDoors(Vector3 agentPos)
        {
            if (doorA == null || doorB == null) return (null, null);
            float distA = (agentPos - doorA.Anchor.position).sqrMagnitude;
            float distB = (agentPos - doorB.Anchor.position).sqrMagnitude;
            return distA < distB ? (doorA, doorB) : (doorB, doorA);
        }
    }
}
