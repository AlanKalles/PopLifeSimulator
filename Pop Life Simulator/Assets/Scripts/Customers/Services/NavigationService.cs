using System;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using PopLife.Runtime;
using PopLife.Data;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 导航服务 - 封装 A* Pathfinding Project 功能
    /// 每个 FloorTileInstance 拥有独立的 GridGraph，通过 NodeLink2 portal 连接相邻地板
    /// 订阅 WorldGrid.OnStructureChanged 自动重建所有 graph
    /// </summary>
    public class NavigationService : MonoBehaviour
    {
        public static NavigationService Instance;

        [Header("A* 设置")]
        [SerializeField] private AstarPath astarPath;
        [SerializeField] private LayerMask obstacleLayer = -1;

        [Header("Outside Graph")]
        [Tooltip("场景中预配置的 outside GridGraph 在 AstarPath.data.graphs 中的索引")]
        [SerializeField] private int outsideGraphIndexInData = 0;

        private GridGraph outsideGraph;

        // 每个 FloorTileInstance 对应一个 GridGraph
        private readonly Dictionary<string, GridGraph> tileGraphs = new();
        // Portal NodeLink2 对象列表（用于清理）
        private readonly List<GameObject> portalLinks = new();

        /// <summary> Outside graph 是否可用 </summary>
        public bool HasOutsideGraph => outsideGraph != null;

        /// <summary> Outside graph 的 GraphMask。缺失时返回空 mask 并报错。 </summary>
        public GraphMask OutsideGraphMask
        {
            get
            {
                if (outsideGraph != null)
                    return GraphMask.FromGraphIndex(outsideGraph.graphIndex);
                Debug.LogError("[NavigationService] Outside graph missing!");
                return default;
            }
        }

        void Awake()
        {
            Instance = this;
            InitializeAstar();
        }

        void Start()
        {
            if (WorldGrid.Instance != null)
                WorldGrid.Instance.OnStructureChanged += OnStructureChanged;
            RebuildAllGraphs();
        }

        void OnDestroy()
        {
            if (WorldGrid.Instance != null)
                WorldGrid.Instance.OnStructureChanged -= OnStructureChanged;
        }

        private void OnStructureChanged()
        {
            RebuildAllGraphs();
        }

        // ========== 初始化 ==========

        private void InitializeAstar()
        {
#if ASTAR_PATHFINDING_PROJECT
            if (astarPath == null)
            {
                astarPath = AstarPath.active;
                if (astarPath == null)
                {
                    Debug.LogError("[NavigationService] 找不到 AstarPath 组件");
                    return;
                }
            }
#endif
        }

        // ========== Graph 重建 ==========

        /// <summary>
        /// 重建所有 FloorTile 的 GridGraph 和 Portal NodeLink2
        /// </summary>
        public void RebuildAllGraphs()
        {
#if ASTAR_PATHFINDING_PROJECT
            if (astarPath == null) return;

            // 获取场景预配置的 outside graph（通过 Inspector 配置的索引）
            var graphs = astarPath.data.graphs;
            if (outsideGraphIndexInData >= 0 && outsideGraphIndexInData < graphs.Length
                && graphs[outsideGraphIndexInData] is GridGraph og)
            {
                outsideGraph = og;
            }
            else
            {
                Debug.LogError($"[NavigationService] Outside graph not found at index {outsideGraphIndexInData}");
                outsideGraph = null;
            }

            // 清理旧 interior graphs（不动 outside graph）
            foreach (var kv in tileGraphs)
                astarPath.data.RemoveGraph(kv.Value);
            tileGraphs.Clear();

            // 清理旧 portal links
            foreach (var go in portalLinks)
                if (go != null) Destroy(go);
            portalLinks.Clear();

            var wg = WorldGrid.Instance;
            if (wg == null) return;

            // 为每个 FloorTileInstance 创建 GridGraph
            foreach (var tile in wg.AllFloorTiles())
            {
                if (tile.Interior == null) continue;
                var graph = CreateGraphForTile(tile);
                tileGraphs[tile.instanceId] = graph;
            }

            // 扫描所有新 graph
            astarPath.Scan();

            // 程序化设 walkability
            foreach (var tile in wg.AllFloorTiles())
            {
                if (tile.Interior == null) continue;
                if (tileGraphs.TryGetValue(tile.instanceId, out var graph))
                    SyncGraphWalkability(graph, tile.Interior);
            }

            // 创建 Portal NodeLink2
            CreatePortalLinks(wg);
#endif
        }

        // ========== 单 Tile Graph 创建 ==========

#if ASTAR_PATHFINDING_PROJECT
        private GridGraph CreateGraphForTile(FloorTileInstance tile)
        {
            var interior = tile.Interior;
            var graph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;

            graph.SetDimensions(interior.GridSize.x, interior.GridSize.y, interior.CellSize);
            graph.center = interior.CenterWorld;

            // 2D 模式
            graph.rotation = new Vector3(-90, 0, 0);

            // 程序化 walkability: 关闭碰撞检测
            graph.collision.mask = 0;
            graph.collision.heightMask = 0;
            graph.collision.unwalkableWhenNoGround = false;
            graph.collision.heightCheck = false;

            return graph;
        }
#endif

        // ========== Walkability 同步 ==========

#if ASTAR_PATHFINDING_PROJECT
        /// <summary>
        /// 同步单个 GridGraph 的 walkability（基于 InteriorGrid 配置层）
        /// walkable 层在 InteriorGrid 初始化后不可变，仅在 graph 创建时同步一次
        /// </summary>
        private void SyncGraphWalkability(GridGraph graph, InteriorGrid interior)
        {
            astarPath.AddWorkItem(new AstarWorkItem(ctx =>
            {
                graph.GetNodes(node =>
                {
                    Vector3 worldPos = (Vector3)node.position;
                    Vector2Int localPos = interior.WorldToLocal(worldPos);
                    node.Walkable = interior.IsWalkable(localPos);
                });
                graph.GetNodes(n => graph.CalculateConnections((GridNodeBase)n));
            }));
        }
#endif

        // ========== Portal NodeLink2 创建 ==========

#if ASTAR_PATHFINDING_PROJECT
        /// <summary>
        /// 扫描所有 FloorTile 对，为左右紧贴的 tile 在 portal cell 之间创建 NodeLink2
        /// </summary>
        private void CreatePortalLinks(WorldGrid wg)
        {
            var tiles = new List<FloorTileInstance>();
            foreach (var t in wg.AllFloorTiles()) tiles.Add(t);

            for (int i = 0; i < tiles.Count; i++)
            {
                for (int j = i + 1; j < tiles.Count; j++)
                {
                    TryCreatePortalsBetween(tiles[i], tiles[j]);
                    TryCreatePortalsBetween(tiles[j], tiles[i]); // 反方向也检查
                }
            }
        }

        /// <summary>
        /// 尝试在 leftTile 的右边缘和 rightTile 的左边缘之间创建 portal
        /// 条件: leftTile 最右列 + 1 == rightTile 最左列（世界网格坐标紧贴）
        /// </summary>
        private void TryCreatePortalsBetween(FloorTileInstance leftTile, FloorTileInstance rightTile)
        {
            if (leftTile.archetype is not FloorTileArchetype leftArch) return;
            if (rightTile.archetype is not FloorTileArchetype rightArch) return;
            if (leftTile.Interior == null || rightTile.Interior == null) return;

            // 计算 leftTile 在世界网格中的最右列
            var leftFp = leftTile.archetype.GetRotatedFootprint(leftTile.rotation);
            var rightFp = rightTile.archetype.GetRotatedFootprint(rightTile.rotation);

            int leftMaxX = int.MinValue;
            foreach (var off in leftFp)
            {
                var p = leftTile.gridPosition + off;
                if (p.x > leftMaxX) leftMaxX = p.x;
            }

            int rightMinX = int.MaxValue;
            foreach (var off in rightFp)
            {
                var p = rightTile.gridPosition + off;
                if (p.x < rightMinX) rightMinX = p.x;
            }

            // 紧贴: leftMaxX + 1 == rightMinX
            if (leftMaxX + 1 != rightMinX) return;

            var leftPortals = leftArch.RightPortalCells;
            var rightPortals = rightArch.LeftPortalCells;
            if (leftPortals == null || rightPortals == null) return;
            if (leftPortals.Count == 0 || rightPortals.Count == 0) return;

            // 匹配 portal cell: 按世界 Y 坐标重叠
            foreach (int leftLocalY in leftPortals)
            {
                // 左侧 tile portal: interior 最右列, local Y
                Vector2Int leftLocalCell = new(leftTile.Interior.GridSize.x - 1, leftLocalY);
                if (!leftTile.Interior.InBounds(leftLocalCell)) continue;
                Vector3 leftWorld = leftTile.Interior.LocalToWorld(leftLocalCell);

                foreach (int rightLocalY in rightPortals)
                {
                    // 右侧 tile portal: interior 最左列, local Y
                    Vector2Int rightLocalCell = new(0, rightLocalY);
                    if (!rightTile.Interior.InBounds(rightLocalCell)) continue;
                    Vector3 rightWorld = rightTile.Interior.LocalToWorld(rightLocalCell);

                    // 世界 Y 必须匹配（容差内）
                    if (Mathf.Abs(leftWorld.y - rightWorld.y) > 0.01f) continue;

                    // 创建 NodeLink2（双向，agent 以普通移动方式通过）
                    float halfCell = leftTile.Interior.CellSize * 0.5f;
                    var linkGo = new GameObject($"Portal_{leftTile.instanceId}_{rightTile.instanceId}_{leftLocalY}_{rightLocalY}");
                    linkGo.transform.position = leftWorld + new Vector3(halfCell, halfCell, 0);

                    var link = linkGo.AddComponent<NodeLink2>();
                    linkGo.AddComponent<NormalTraversalLink>(); // 标记为普通移动（非传送）

                    var endGo = new GameObject("End");
                    endGo.transform.SetParent(linkGo.transform);
                    endGo.transform.position = rightWorld + new Vector3(rightTile.Interior.CellSize * 0.5f, rightTile.Interior.CellSize * 0.5f, 0);
                    link.end = endGo.transform;

                    portalLinks.Add(linkGo);
                }
            }
        }
#endif

        // ========== 路径请求 ==========

        public bool TryRequestPath(Vector3 start, Vector3 end, out List<Vector3> path)
        {
            path = new List<Vector3>();

#if ASTAR_PATHFINDING_PROJECT
            ABPath abPath = ABPath.Construct(start, end, null);
            AstarPath.StartPath(abPath);
            abPath.BlockUntilCalculated();

            if (abPath.error)
            {
                Debug.LogWarning($"[NavigationService] 路径计算失败: {abPath.errorLog}");
                path.Add(start);
                path.Add(end);
                return false;
            }

            path = abPath.vectorPath;
            return true;
#else
            path.Add(start);
            path.Add(end);
            return true;
#endif
        }

        public void RequestPathAsync(Vector3 start, Vector3 end, Action<List<Vector3>> callback)
        {
#if ASTAR_PATHFINDING_PROJECT
            ABPath path = ABPath.Construct(start, end, (p) =>
            {
                if (p.error)
                {
                    Debug.LogWarning($"[NavigationService] 异步路径计算失败");
                    callback?.Invoke(new List<Vector3> { start, end });
                }
                else
                {
                    callback?.Invoke(p.vectorPath);
                }
            });

            AstarPath.StartPath(path);
#else
            callback?.Invoke(new List<Vector3> { start, end });
#endif
        }

        public bool IsReachable(Vector3 start, Vector3 end)
        {
#if ASTAR_PATHFINDING_PROJECT
            var node1 = astarPath.GetNearest(start).node;
            var node2 = astarPath.GetNearest(end).node;

            if (node1 == null || node2 == null)
                return false;

            return PathUtilities.IsPathPossible(node1, node2);
#else
            RaycastHit2D hit = Physics2D.Linecast(start, end, obstacleLayer);
            return hit.collider == null;
#endif
        }

        // ========== 公共 API ==========

        /// <summary>
        /// 完全重新扫描导航网格
        /// </summary>
        public void RescanNavMesh()
        {
#if ASTAR_PATHFINDING_PROJECT
            RebuildAllGraphs();
            Debug.Log("[NavigationService] 导航网格已重新扫描");
#endif
        }
    }
}
