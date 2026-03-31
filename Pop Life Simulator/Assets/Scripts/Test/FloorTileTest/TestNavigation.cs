using UnityEngine;
using Pathfinding;

namespace PopLife.Test
{
    /// <summary>
    /// 测试用导航管理器 — 配置 GridGraph 并根据地板状态同步 walkability
    /// </summary>
    public class TestNavigation : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private TestFloorGrid grid;

        private AstarPath astarPath;
        private GridGraph gridGraph;

        void Start()
        {
            InitializeGraph();

            if (grid != null)
                grid.OnFloorChanged += OnFloorChanged;
        }

        void OnDestroy()
        {
            if (grid != null)
                grid.OnFloorChanged -= OnFloorChanged;
        }

        private void InitializeGraph()
        {
            astarPath = AstarPath.active;
            if (astarPath == null)
            {
                Debug.LogError("[TestNavigation] AstarPath not found in scene. Add an AstarPath component.");
                return;
            }

            // 获取或创建 GridGraph
            gridGraph = astarPath.data.gridGraph;
            if (gridGraph == null)
            {
                gridGraph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;
            }

            // 配置 GridGraph 覆盖 gizmoSize 区域
            var gizmoSize = grid != null ? grid.gizmoSize : new Vector2Int(20, 15);
            float cellSize = grid != null ? grid.cellSize : 1f;

            gridGraph.SetDimensions(gizmoSize.x, gizmoSize.y, cellSize);

            // center 设为网格区域的中心
            var originPos = grid != null ? grid.OriginPos : Vector3.zero;
            gridGraph.center = originPos + new Vector3(
                gizmoSize.x * cellSize * 0.5f,
                gizmoSize.y * cellSize * 0.5f,
                0);

            // 2D 平面旋转（XY 平面）
            gridGraph.rotation = new Vector3(-90, 0, 0);

            // 关闭碰撞和高度检测，用程序化 walkability
            gridGraph.collision.mask = 0;
            gridGraph.collision.heightMask = 0;
            gridGraph.collision.unwalkableWhenNoGround = false;
            gridGraph.collision.heightCheck = false;

            // 扫描
            astarPath.Scan();

            // 初始同步
            SyncWalkability();
        }

        private void OnFloorChanged()
        {
            SyncWalkability();
        }

        /// <summary>
        /// 遍历所有节点，根据地板/建筑状态设置 walkability
        /// 有地板且无建筑 → walkable，否则 → unwalkable
        /// </summary>
        // 有垂直分量的方向 + 对应的偏移（2D模式: Z→Y）
        // Dir: 0(下), 2(上), 4(右下), 5(右上), 6(左上), 7(左下)
        private static readonly int[] verticalDirs = { 0, 2, 4, 5, 6, 7 };
        private static readonly Vector2Int[] dirOffsets =
        {
            new(0, -1),  // 0: 下
            new(1, 0),   // 1: 右
            new(0, 1),   // 2: 上
            new(-1, 0),  // 3: 左
            new(1, -1),  // 4: 右下
            new(1, 1),   // 5: 右上
            new(-1, 1),  // 6: 左上
            new(-1, -1), // 7: 左下
        };

        public void SyncWalkability()
        {
            if (gridGraph == null || grid == null || astarPath == null) return;

            astarPath.AddWorkItem(new AstarWorkItem(ctx =>
            {
                // ① 设 walkability
                gridGraph.GetNodes(node =>
                {
                    Vector3 worldPos = (Vector3)node.position;
                    Vector2Int gridCoord = grid.WorldToGrid(worldPos);

                    bool hasFloor = grid.HasFloorTileAt(gridCoord);
                    bool hasBuilding = grid.HasBuildingAt(gridCoord);

                    node.Walkable = hasFloor && !hasBuilding;
                });

                // ② 重算连接
                gridGraph.GetNodes(n => gridGraph.CalculateConnections((GridNodeBase)n));

                // ③ 断开跨 FloorTile 的垂直连接
                gridGraph.GetNodes(node =>
                {
                    if (!node.Walkable) return;

                    var gn = (GridNode)node;
                    Vector3 worldPos = (Vector3)node.position;
                    Vector2Int pos = grid.WorldToGrid(worldPos);
                    string myOwner = grid.GetFloorTileOwnerId(pos);

                    if (myOwner == null) return;

                    foreach (int dir in verticalDirs)
                    {
                        if (!gn.HasConnectionInDirection(dir)) continue;

                        Vector2Int neighborPos = pos + dirOffsets[dir];
                        string neighborOwner = grid.GetFloorTileOwnerId(neighborPos);

                        // 不同 FloorTile → 断开垂直连接
                        if (neighborOwner != null && neighborOwner != myOwner)
                        {
                            gn.SetConnection(dir, false);
                        }
                    }
                });
            }));
        }

        /// <summary>
        /// 检查两点是否可达
        /// </summary>
        public bool IsReachable(Vector3 start, Vector3 end)
        {
            if (astarPath == null) return false;

            var node1 = astarPath.GetNearest(start).node;
            var node2 = astarPath.GetNearest(end).node;

            if (node1 == null || node2 == null) return false;
            return PathUtilities.IsPathPossible(node1, node2);
        }
    }
}
