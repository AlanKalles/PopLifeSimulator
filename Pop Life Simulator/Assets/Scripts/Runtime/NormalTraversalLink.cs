using UnityEngine;
using Pathfinding;

namespace PopLife.Runtime
{
    /// <summary>
    /// 标记组件：挂载在 NodeLink2 上，表示该链接不使用传送，
    /// agent 将以普通移动方式通过此链接。
    /// </summary>
    [RequireComponent(typeof(NodeLink2))]
    public class NormalTraversalLink : MonoBehaviour { }
}
