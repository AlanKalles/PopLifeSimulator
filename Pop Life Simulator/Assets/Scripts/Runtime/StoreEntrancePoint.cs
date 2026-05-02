using UnityEngine;
using Pathfinding;

namespace PopLife.Runtime
{
    /// <summary>
    /// 商店入口/出口点
    /// 标记商店与外部街道的连接位置
    /// 通过 NodeLink2 连接两个 Grid Graph
    ///
    /// 使用说明：
    /// 1. 在场景中创建一个包含 NodeLink2 的 GameObject
    /// 2. 在 NodeLink2 的 Inspector 中配置 start（自身位置）和 end（目标位置）
    /// 3. 将该 NodeLink2 引用到此组件
    /// 4. outsideAnchor = nodeLink.StartTransform（外部入口）
    /// 5. insideAnchor = nodeLink.EndTransform（内部入口）
    /// </summary>
    public class StoreEntrancePoint : MonoBehaviour
    {
        [Header("入口标识")]
        [Tooltip("入口唯一ID")]
        public string entranceId = "MainEntrance";

        [Tooltip("是否为主入口（用于顾客默认选择）")]
        public bool isMainEntrance = true;

        [SerializeField]
        [Tooltip("此入口所属商店 ID")]
        private StoreIdKind storeId = StoreIdKind.PlayerStore;

        public string StoreId => StoreIds.ToId(storeId);

        [Header("A* Pathfinding 配置")]
        [Tooltip("NodeLink2 组件引用（在场景中手动配置好 start 和 end）\n双向通行：oneWay = false")]
        public NodeLink2 nodeLink;

        [Header("锚点引用（自动从 NodeLink2 获取）")]
        [Tooltip("外部锚点（街道侧）= NodeLink2.StartTransform")]
        public Transform outsideAnchor;

        [Tooltip("内部锚点（商店一楼侧）= NodeLink2.EndTransform")]
        public Transform insideAnchor;

        void Awake()
        {
            // 验证 NodeLink2 配置
            if (nodeLink == null)
            {
                Debug.LogError($"[StoreEntrancePoint] '{entranceId}' 缺少 NodeLink2 引用！");
                return;
            }

            // 自动从 NodeLink2 获取锚点
            outsideAnchor = nodeLink.StartTransform;
            insideAnchor = nodeLink.EndTransform;

            // 验证锚点
            if (outsideAnchor == null)
            {
                Debug.LogError($"[StoreEntrancePoint] '{entranceId}' 无法获取 outsideAnchor (NodeLink2.StartTransform)！");
            }

            if (insideAnchor == null)
            {
                Debug.LogError($"[StoreEntrancePoint] '{entranceId}' 无法获取 insideAnchor (NodeLink2.EndTransform)！");
            }

            Debug.Log($"[StoreEntrancePoint] '{entranceId}' 已初始化：外部={outsideAnchor?.position}, 内部={insideAnchor?.position}");
        }

        void OnEnable()
        {
            // 注册到 EntranceManager
            if (EntranceManager.Instance != null)
            {
                EntranceManager.Instance.RegisterEntrance(this);
            }
        }

        void OnDisable()
        {
            // 从 EntranceManager 注销
            if (EntranceManager.Instance != null)
            {
                EntranceManager.Instance.UnregisterEntrance(this);
            }
        }


        /// <summary>
        /// 获取距离指定位置最近的锚点（用于智能选择入口/出口侧）
        /// </summary>
        public Transform GetNearestAnchor(Vector3 position)
        {
            if (outsideAnchor == null) return insideAnchor;
            if (insideAnchor == null) return outsideAnchor;

            float distToOutside = Vector3.Distance(position, outsideAnchor.position);
            float distToInside = Vector3.Distance(position, insideAnchor.position);

            return distToOutside < distToInside ? outsideAnchor : insideAnchor;
        }

        /// <summary>
        /// 在 Scene 视图中绘制 Gizmos
        /// </summary>
        void OnDrawGizmos()
        {
            if (outsideAnchor == null || insideAnchor == null) return;

            // 外部锚点：蓝色球体
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
            Gizmos.DrawSphere(outsideAnchor.position, 0.3f);

            // 内部锚点：绿色球体
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.8f);
            Gizmos.DrawSphere(insideAnchor.position, 0.3f);

            // 连接线：黄色虚线
            Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
            Gizmos.DrawLine(outsideAnchor.position, insideAnchor.position);

            // 主入口标记：橙色圆环
            if (isMainEntrance)
            {
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
                Gizmos.DrawWireSphere(outsideAnchor.position, 0.5f);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (outsideAnchor == null || insideAnchor == null) return;

            // 高亮显示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(outsideAnchor.position, 0.6f);
            Gizmos.DrawWireSphere(insideAnchor.position, 0.6f);
        }
    }
}
