using System;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 导航服务 - 封装 A* Pathfinding Project 功能
    /// </summary>
    public class NavigationService : MonoBehaviour
    {
        public static NavigationService Instance;

        [Header("A* 设置")]
        [SerializeField] private AstarPath astarPath;
        [SerializeField] private float gridGraphNodeSize = 1f;
        [SerializeField] private LayerMask obstacleLayer = -1;

        void Awake()
        {
            Instance = this;
            InitializeAstar();
        }

        /// <summary>
        /// 初始化 A* 系统
        /// </summary>
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

            // 可以在这里动态配置 A* 图形
            ConfigureGridGraph();
#endif
        }

        /// <summary>
        /// 配置网格图
        /// </summary>
        private void ConfigureGridGraph()
        {
#if ASTAR_PATHFINDING_PROJECT
            var gridGraph = astarPath.data.gridGraph;
            if (gridGraph == null)
            {
                Debug.Log("[NavigationService] 创建新的网格图");
                gridGraph = astarPath.data.AddGraph(typeof(GridGraph)) as GridGraph;
            }

            // 设置网格大小和位置
            if (FloorGrid.Instance != null)
            {
                var floorSize = FloorGrid.Instance.GetGridSize();
                gridGraph.SetDimensions(floorSize.x, floorSize.y, gridGraphNodeSize);
                gridGraph.center = FloorGrid.Instance.transform.position;
            }
            else
            {
                // 默认设置
                gridGraph.SetDimensions(50, 50, gridGraphNodeSize);
                gridGraph.center = Vector3.zero;
            }

            // 设置碰撞检测
            gridGraph.collision.type = ColliderType.Circle;
            gridGraph.collision.diameter = 0.5f;
            gridGraph.collision.mask = obstacleLayer;

            // 扫描图形
            astarPath.Scan();
#endif
        }

        /// <summary>
        /// 请求路径
        /// </summary>
        public bool TryRequestPath(Vector3 start, Vector3 end, out List<Vector3> path)
        {
            path = new List<Vector3>();

#if ASTAR_PATHFINDING_PROJECT
            // 创建路径请求
            ABPath abPath = ABPath.Construct(start, end, null);

            // 同步计算路径
            AstarPath.StartPath(abPath);
            abPath.BlockUntilCalculated();

            if (abPath.error)
            {
                Debug.LogWarning($"[NavigationService] 路径计算失败: {abPath.errorLog}");
                path.Add(start);
                path.Add(end);
                return false;
            }

            // 获取路径点
            path = abPath.vectorPath;
            return true;
#else
            // 没有 A* 时的简单实现
            path.Add(start);
            path.Add(end);
            return true;
#endif
        }

        /// <summary>
        /// 异步请求路径
        /// </summary>
        public void RequestPathAsync(Vector3 start, Vector3 end, System.Action<List<Vector3>> callback)
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

        /// <summary>
        /// 检查两点之间是否可达
        /// </summary>
        public bool IsReachable(Vector3 start, Vector3 end)
        {
#if ASTAR_PATHFINDING_PROJECT
            var node1 = astarPath.GetNearest(start).node;
            var node2 = astarPath.GetNearest(end).node;

            if (node1 == null || node2 == null)
                return false;

            return PathUtilities.IsPathPossible(node1, node2);
#else
            // 简单的射线检测
            RaycastHit2D hit = Physics2D.Linecast(start, end, obstacleLayer);
            return hit.collider == null;
#endif
        }

        /// <summary>
        /// 更新导航网格（当建筑物改变时调用）
        /// </summary>
        public void UpdateNavMesh(Bounds area)
        {
#if ASTAR_PATHFINDING_PROJECT
            if (astarPath == null) return;

            var guo = new GraphUpdateObject(area);
            astarPath.UpdateGraphs(guo);
#endif
        }

        /// <summary>
        /// 完全重新扫描导航网格
        /// </summary>
        public void RescanNavMesh()
        {
#if ASTAR_PATHFINDING_PROJECT
            if (astarPath == null) return;

            astarPath.Scan();
            Debug.Log("[NavigationService] 导航网格已重新扫描");
#endif
        }
    }
}