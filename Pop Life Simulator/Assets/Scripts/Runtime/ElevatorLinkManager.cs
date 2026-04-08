using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Runtime
{
    /// <summary>
    /// 管理电梯门之间的自动连接。
    /// 连接基于 FloorTile 的结构分支（branch）：同一分支内的门两两直连。
    /// 分支由 root (IsDefault) 到 leaf 的路径定义，每次分叉产生新 branch。
    /// </summary>
    [DefaultExecutionOrder(100)] // 在 NavigationService 之后执行
    public class ElevatorLinkManager : MonoBehaviour
    {
        private readonly List<LinkData> activeLinks = new();

        public struct LinkData
        {
            public ElevatorDoorInstance doorA;
            public ElevatorDoorInstance doorB;
            public GameObject linkGo; // NodeLink2 + AILerpLinkTeleporter
        }

        /// <summary> 只读访问当前连接（供调试器使用） </summary>
        public IReadOnlyList<LinkData> ActiveLinks => activeLinks;

        private void Start()
        {
            // 兜底：场景加载后首次重建
            RebuildAllLinks();
            // 刷新所有电梯门的楼层标签（Editor 放置时 WorldGrid 未初始化，标签可能是 "?"）
            RefreshAllDoorLabels();
        }

        /// <summary> 全量重建所有电梯连接 </summary>
        public void RebuildAllLinks()
        {
            // 1. 销毁所有现有 link
            foreach (var link in activeLinks)
            {
                if (link.linkGo != null)
                    Destroy(link.linkGo);
            }
            activeLinks.Clear();

            var wg = WorldGrid.Instance;
            if (wg == null) return;

            // 2. 构建结构分支映射
            var tileBranches = BuildBranchMap(wg);

            // 3. 收集所有电梯门（从 InteriorGrid 注册数据，不用 FindObjectsByType）
            var doorEntries = new List<(ElevatorDoorInstance door, string tileId)>();
            foreach (var tile in wg.AllFloorTiles())
            {
                if (tile.Interior == null) continue;
                foreach (var building in tile.Interior.AllBuildings())
                {
                    if (building is ElevatorDoorInstance door)
                        doorEntries.Add((door, tile.instanceId));
                }
            }

            if (doorEntries.Count < 2) return;

            // 4. 对每对门，检查所在 tile 是否共享至少一个 branch
            for (int i = 0; i < doorEntries.Count; i++)
            {
                for (int j = i + 1; j < doorEntries.Count; j++)
                {
                    var a = doorEntries[i];
                    var b = doorEntries[j];

                    if (ShareBranch(tileBranches, a.tileId, b.tileId))
                        CreateLink(a.door, b.door, wg);
                }
            }
        }

        /// <summary> 检查指定 FloorTile 上是否有 ElevatorDoorInstance </summary>
        public bool HasElevatorOnTile(FloorTileInstance tile)
        {
            if (tile == null || tile.Interior == null) return false;
            foreach (var building in tile.Interior.AllBuildings())
            {
                if (building is ElevatorDoorInstance) return true;
            }
            return false;
        }

        // ================================================================
        //  楼层标签刷新
        // ================================================================

        /// <summary> 刷新所有电梯门的楼层标签（运行时 FloorLevel 可用后调用） </summary>
        private void RefreshAllDoorLabels()
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return;

            foreach (var tile in wg.AllFloorTiles())
            {
                if (tile.Interior == null) continue;
                foreach (var building in tile.Interior.AllBuildings())
                {
                    if (building is ElevatorDoorInstance door)
                        door.SetFloorLabel(tile.FloorDisplayName);
                }
            }
        }

        // ================================================================
        //  分支映射
        // ================================================================

        /// <summary>
        /// 构建结构分支映射（两步算法）。
        ///
        /// Step 1 — 同层连通组件：
        ///   用 IsSameFloorNeighbor 把水平邻接的 tile 合并为超级节点。
        ///
        /// Step 2 — 超级节点级 DFS：
        ///   从 root 超级节点向上遍历（IsUpperFloorNeighbor）。
        ///   上层 1 个超级节点 → 继承 branch。
        ///   上层 2+ 个超级节点 → 每个开新 branch。
        ///   枚举所有 root-to-leaf 路径，每条路径 = 一个 branch ID。
        /// </summary>
        private Dictionary<string, HashSet<int>> BuildBranchMap(WorldGrid wg)
        {
            var result = new Dictionary<string, HashSet<int>>();
            var allTiles = new List<FloorTileInstance>();
            foreach (var t in wg.AllFloorTiles()) allTiles.Add(t);
            if (allTiles.Count == 0) return result;

            // Step 1: 同层连通组件（BFS）
            var visited = new HashSet<string>();
            var superNodes = new List<List<FloorTileInstance>>();
            var tileToSuperNode = new Dictionary<string, int>(); // tileId → superNode index

            foreach (var tile in allTiles)
            {
                if (visited.Contains(tile.instanceId)) continue;

                var group = new List<FloorTileInstance>();
                var queue = new Queue<FloorTileInstance>();
                queue.Enqueue(tile);
                visited.Add(tile.instanceId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);

                    foreach (var other in allTiles)
                    {
                        if (visited.Contains(other.instanceId)) continue;
                        if (wg.IsSameFloorNeighbor(current, other))
                        {
                            visited.Add(other.instanceId);
                            queue.Enqueue(other);
                        }
                    }
                }

                int snIndex = superNodes.Count;
                superNodes.Add(group);
                foreach (var t in group)
                    tileToSuperNode[t.instanceId] = snIndex;
            }

            // Step 2: 构建超级节点之间的上下邻接（下 → 上）
            // adjacency[lowerSN] = set of upper SN indices
            var upAdjacency = new Dictionary<int, HashSet<int>>();
            for (int i = 0; i < superNodes.Count; i++)
                upAdjacency[i] = new HashSet<int>();

            for (int i = 0; i < superNodes.Count; i++)
            {
                for (int j = 0; j < superNodes.Count; j++)
                {
                    if (i == j) continue;
                    // 检查 i 中任意 tile 是否是 j 中任意 tile 的下层
                    bool found = false;
                    foreach (var lower in superNodes[i])
                    {
                        foreach (var upper in superNodes[j])
                        {
                            if (wg.IsUpperFloorNeighbor(lower, upper))
                            {
                                upAdjacency[i].Add(j);
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                }
            }

            // Step 3: 找 root 超级节点
            int rootSN = -1;
            for (int i = 0; i < superNodes.Count; i++)
            {
                foreach (var t in superNodes[i])
                {
                    if (t.IsDefault) { rootSN = i; break; }
                }
                if (rootSN >= 0) break;
            }

            if (rootSN < 0)
            {
                // 无 root，所有 tile 分配 branch 0
                foreach (var t in allTiles)
                {
                    if (!result.ContainsKey(t.instanceId))
                        result[t.instanceId] = new HashSet<int>();
                    result[t.instanceId].Add(0);
                }
                return result;
            }

            // 初始化 result
            foreach (var t in allTiles)
                result[t.instanceId] = new HashSet<int>();

            // Step 4: DFS 枚举 root-to-leaf 路径（传递祖先路径）
            int nextBranchId = 0;
            var visitedSN = new HashSet<int>();
            var ancestorPath = new List<int>(); // root → current 的超级节点索引
            DfsEnumerateBranches(rootSN, nextBranchId, ancestorPath, superNodes, upAdjacency, visitedSN, result, ref nextBranchId);

            return result;
        }

        private void DfsEnumerateBranches(
            int snIndex, int currentBranch,
            List<int> ancestorPath,
            List<List<FloorTileInstance>> superNodes,
            Dictionary<int, HashSet<int>> upAdjacency,
            HashSet<int> visitedSN,
            Dictionary<string, HashSet<int>> result,
            ref int nextBranchId)
        {
            if (visitedSN.Contains(snIndex)) return;
            visitedSN.Add(snIndex);

            // 当前超级节点的所有 tile 属于当前 branch
            foreach (var tile in superNodes[snIndex])
                result[tile.instanceId].Add(currentBranch);

            // 把自己加入祖先路径
            ancestorPath.Add(snIndex);

            // 找未访问的上层子超级节点
            var children = new List<int>();
            foreach (var upSN in upAdjacency[snIndex])
            {
                if (!visitedSN.Contains(upSN))
                    children.Add(upSN);
            }

            if (children.Count == 0)
            {
                // 叶子，路径结束
            }
            else if (children.Count == 1)
            {
                // 不分叉，继承当前 branch
                DfsEnumerateBranches(children[0], currentBranch, ancestorPath, superNodes, upAdjacency, visitedSN, result, ref nextBranchId);
            }
            else
            {
                // 分叉：每个子节点开新 branch，并把所有祖先也加入新 branch
                foreach (var child in children)
                {
                    nextBranchId++;
                    int newBranch = nextBranchId;

                    // 祖先路径上所有超级节点的 tile 也属于新 branch
                    // （确保 root 和中间分叉点与深层叶子共享 branch）
                    foreach (var ancestorSN in ancestorPath)
                    {
                        foreach (var tile in superNodes[ancestorSN])
                            result[tile.instanceId].Add(newBranch);
                    }

                    DfsEnumerateBranches(child, newBranch, ancestorPath, superNodes, upAdjacency, visitedSN, result, ref nextBranchId);
                }
            }

            // 回溯：从祖先路径移除自己
            ancestorPath.RemoveAt(ancestorPath.Count - 1);
        }

        // ================================================================
        //  连接判定与创建
        // ================================================================

        private static bool ShareBranch(Dictionary<string, HashSet<int>> tileBranches, string tileIdA, string tileIdB)
        {
            if (!tileBranches.TryGetValue(tileIdA, out var branchesA)) return false;
            if (!tileBranches.TryGetValue(tileIdB, out var branchesB)) return false;
            foreach (var b in branchesA)
            {
                if (branchesB.Contains(b)) return true;
            }
            return false;
        }

        private void CreateLink(ElevatorDoorInstance doorA, ElevatorDoorInstance doorB, WorldGrid wg)
        {
            var startWorld = doorA.Anchor.position;
            var endWorld = doorB.Anchor.position;

            var linkGo = new GameObject($"ElevatorLink_{doorA.instanceId}_{doorB.instanceId}");
            if (wg.buildingContainer != null)
                linkGo.transform.SetParent(wg.buildingContainer);
            linkGo.transform.position = startWorld;

            var link = linkGo.AddComponent<Pathfinding.NodeLink2>();
            var teleporter = linkGo.AddComponent<AILerpLinkTeleporter>();
            teleporter.SetDoors(doorA, doorB);

            var endGo = new GameObject("End");
            endGo.transform.SetParent(linkGo.transform);
            endGo.transform.position = endWorld;
            link.end = endGo.transform;

            activeLinks.Add(new LinkData
            {
                doorA = doorA,
                doorB = doorB,
                linkGo = linkGo
            });
        }
    }
}
