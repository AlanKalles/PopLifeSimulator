using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    /// <summary>
    /// FloorTile 内部的矩形网格，管理建筑放置、占用、可行走三层布尔数据。
    /// 纯 C# 类，不继承 MonoBehaviour。
    ///
    /// 三层模型（以 8×4, walkableRows=2 为例）：
    ///   Row 3: |    | place | place | place | place | place | place |    |
    ///   Row 2: |    | place | place | place | place | place | place |    |
    ///   Row 1: | walk | PW  |  PW  |  PW  |  PW  |  PW  |  PW  | walk |
    ///   Row 0: | walk | walk | walk | walk | walk | walk | walk | walk |
    ///          ^edge                                              ^edge
    ///
    /// placeable — 配置层，初始化时设定，标记可放置建筑的格子（排除左右 portal 列）
    /// occupied  — 运行时层，Register/Unregister 切换
    /// walkable  — 配置层，初始化后不可变，标记顾客可行走的格子
    /// </summary>
    public class InteriorGrid
    {
        // ── 核心数据 ──────────────────────────────────────────────

        private readonly Vector2Int gridSize;
        private readonly float cellSize;
        private readonly Vector3 originWorld;

        // 三层布尔矩阵
        private readonly bool[,] placeable;  // 配置层：可放置
        private readonly bool[,] occupied;   // 运行时层：已占用
        private readonly bool[,] walkable;   // 配置层：可行走（初始化后不可变）

        // 占用者 ID（与 occupied 对应）
        private readonly string[,] occupantId;

        // 建筑实例字典
        private readonly Dictionary<string, BuildingInstance> buildings = new();

        // ── 属性 ──────────────────────────────────────────────────

        public Vector2Int GridSize => gridSize;
        public float CellSize => cellSize;
        public Vector3 OriginWorld => originWorld;

        /// <summary> 网格世界空间中心点 </summary>
        public Vector3 CenterWorld => originWorld + new Vector3(
            gridSize.x * cellSize * 0.5f,
            gridSize.y * cellSize * 0.5f,
            0f);

        // ── 事件 ──────────────────────────────────────────────────

        /// <summary> 占用状态变化时触发（Register/Unregister/Move 后） </summary>
        public event Action OnOccupancyChanged;

        /// <summary> 公开触发占用变化事件（C# 事件只能在声明类内 Invoke） </summary>
        public void NotifyOccupancyChanged()
        {
            OnOccupancyChanged?.Invoke();
        }

        // ── 构造函数 ──────────────────────────────────────────────

        /// <summary>
        /// 创建一个 Interior 网格。
        /// </summary>
        /// <param name="gridSize">网格尺寸（宽×高）</param>
        /// <param name="cellSize">每格世界单位大小</param>
        /// <param name="originWorld">网格左下角世界坐标</param>
        /// <param name="walkableRows">底部可行走行数</param>
        /// <param name="portalLeftYs">左侧 portal 列额外可行走的 Y 索引</param>
        /// <param name="portalRightYs">右侧 portal 列额外可行走的 Y 索引</param>
        public InteriorGrid(
            Vector2Int gridSize,
            float cellSize,
            Vector3 originWorld,
            int walkableRows,
            IReadOnlyList<int> portalLeftYs,
            IReadOnlyList<int> portalRightYs)
        {
            this.gridSize = gridSize;
            this.cellSize = cellSize;
            this.originWorld = originWorld;

            placeable = new bool[gridSize.x, gridSize.y];
            occupied = new bool[gridSize.x, gridSize.y];
            walkable = new bool[gridSize.x, gridSize.y];
            occupantId = new string[gridSize.x, gridSize.y];

            InitializeLayers(walkableRows, portalLeftYs, portalRightYs);
        }

        /// <summary>
        /// 根据规则初始化 walkable 和 placeable 层。
        /// </summary>
        private void InitializeLayers(
            int walkableRows,
            IReadOnlyList<int> portalLeftCols,
            IReadOnlyList<int> portalRightCols)
        {
            int w = gridSize.x;
            int h = gridSize.y;

            // 构建 portal 列集合（从左起第 N 列 / 从右起第 N 列）
            var portalColumns = new HashSet<int>();
            if (portalLeftCols != null)
            {
                foreach (int col in portalLeftCols)
                    if (col >= 0 && col < w) portalColumns.Add(col);
            }
            if (portalRightCols != null)
            {
                foreach (int col in portalRightCols)
                {
                    int actualX = w - 1 - col; // 从右起：0=最右列，1=倒数第二列...
                    if (actualX >= 0 && actualX < w) portalColumns.Add(actualX);
                }
            }

            for (int x = 0; x < w; x++)
            {
                bool isPortalCol = portalColumns.Contains(x);

                for (int y = 0; y < h; y++)
                {
                    // ── walkable ──
                    // 底部 walkableRows 行可走（所有列，含 portal 列）
                    if (y < walkableRows)
                    {
                        walkable[x, y] = true;
                    }

                    // ── placeable ──
                    // portal 列不可放置建筑
                    placeable[x, y] = !isPortalCol;
                }
            }
        }

        // ── 坐标转换 ─────────────────────────────────────────────

        /// <summary> 本地网格坐标 → 世界坐标（格子左下角） </summary>
        public Vector3 LocalToWorld(Vector2Int localPos)
        {
            return originWorld + new Vector3(
                localPos.x * cellSize,
                localPos.y * cellSize,
                0f);
        }

        /// <summary> 世界坐标 → 本地网格坐标（FloorToInt） </summary>
        public Vector2Int WorldToLocal(Vector3 worldPos)
        {
            Vector3 offset = worldPos - originWorld;
            return new Vector2Int(
                Mathf.FloorToInt(offset.x / cellSize),
                Mathf.FloorToInt(offset.y / cellSize));
        }

        /// <summary> 判断坐标是否在网格范围内 </summary>
        public bool InBounds(Vector2Int p)
        {
            return p.x >= 0 && p.x < gridSize.x
                && p.y >= 0 && p.y < gridSize.y;
        }

        // ── 放置验证 ─────────────────────────────────────────────

        /// <summary>
        /// 检查占地模板能否放置在指定原点。
        /// 所有格子必须：InBounds + placeable + !occupied
        /// </summary>
        public bool CanPlace(List<Vector2Int> footprint, Vector2Int origin)
        {
            if (footprint == null) return false;

            int minY = int.MaxValue;
            foreach (var offset in footprint)
            {
                var cell = origin + offset;
                if (!InBounds(cell)) return false;
                if (!placeable[cell.x, cell.y]) return false;
                if (occupied[cell.x, cell.y]) return false;
                if (cell.y < minY) minY = cell.y;
            }

            // 接地规则：footprint 最底行必须在 y=0（建筑必须站在地面上）
            if (minY != 0) return false;

            return true;
        }

        /// <summary>
        /// 同 CanPlace，但允许被 selfId 占用的格子（用于移动建筑时检测）。
        /// </summary>
        public bool CanPlaceAllowSelf(List<Vector2Int> footprint, Vector2Int origin, string selfId)
        {
            if (footprint == null) return false;

            int minY = int.MaxValue;
            foreach (var offset in footprint)
            {
                var cell = origin + offset;
                if (!InBounds(cell)) return false;
                if (!placeable[cell.x, cell.y]) return false;
                if (occupied[cell.x, cell.y] && occupantId[cell.x, cell.y] != selfId) return false;
                if (cell.y < minY) minY = cell.y;
            }

            // 接地规则
            if (minY != 0) return false;

            return true;
        }

        // ── 建筑管理 ─────────────────────────────────────────────

        /// <summary>
        /// 注册建筑：标记占用、记录到字典。
        /// </summary>
        public void RegisterBuilding(BuildingInstance bi, List<Vector2Int> footprint, Vector2Int origin)
        {
            if (bi == null) return;

            string id = bi.instanceId;
            buildings[id] = bi;

            foreach (var offset in footprint)
            {
                var cell = origin + offset;
                if (!InBounds(cell)) continue;

                occupied[cell.x, cell.y] = true;
                occupantId[cell.x, cell.y] = id;
            }

            NotifyOccupancyChanged();
        }

        /// <summary>
        /// 取消注册建筑：清除占用、从字典移除。
        /// 使用建筑自身的 archetype.GetRotatedFootprint(rotation) 和 gridPosition 获取占地信息。
        /// </summary>
        public void UnregisterBuilding(BuildingInstance bi)
        {
            if (bi == null || bi.archetype == null) return;

            string id = bi.instanceId;
            var footprint = bi.archetype.GetRotatedFootprint(bi.rotation);
            var origin = bi.gridPosition;

            foreach (var offset in footprint)
            {
                var cell = origin + offset;
                if (!InBounds(cell)) continue;

                // 只清除属于该建筑的占用
                if (occupantId[cell.x, cell.y] == id)
                {
                    occupied[cell.x, cell.y] = false;
                    occupantId[cell.x, cell.y] = null;
                }
            }

            buildings.Remove(id);
            NotifyOccupancyChanged();
        }

        /// <summary>
        /// 移动建筑到新位置和旋转。
        /// 先取消注册旧位置，验证新位置可放置，再注册新位置。
        /// 若无法放置，回滚到旧状态并返回 false。
        /// </summary>
        public bool MoveBuilding(BuildingInstance bi, Vector2Int newPos, int newRot)
        {
            if (bi == null || bi.archetype == null) return false;

            // 记录旧状态
            var oldPos = bi.gridPosition;
            var oldRot = bi.rotation;
            var oldFootprint = bi.archetype.GetRotatedFootprint(oldRot);

            // 先取消注册（但不触发事件——最后统一触发）
            foreach (var offset in oldFootprint)
            {
                var cell = oldPos + offset;
                if (!InBounds(cell)) continue;
                if (occupantId[cell.x, cell.y] == bi.instanceId)
                {
                    occupied[cell.x, cell.y] = false;
                    occupantId[cell.x, cell.y] = null;
                }
            }
            buildings.Remove(bi.instanceId);

            // 检查新位置是否可放置
            var newFootprint = bi.archetype.GetRotatedFootprint(newRot);
            if (!CanPlace(newFootprint, newPos))
            {
                // 回滚：重新注册旧位置
                buildings[bi.instanceId] = bi;
                foreach (var offset in oldFootprint)
                {
                    var cell = oldPos + offset;
                    if (!InBounds(cell)) continue;
                    occupied[cell.x, cell.y] = true;
                    occupantId[cell.x, cell.y] = bi.instanceId;
                }
                return false;
            }

            // 更新建筑实例数据
            bi.gridPosition = newPos;
            bi.rotation = newRot;

            // 注册新位置
            buildings[bi.instanceId] = bi;
            foreach (var offset in newFootprint)
            {
                var cell = newPos + offset;
                if (!InBounds(cell)) continue;
                occupied[cell.x, cell.y] = true;
                occupantId[cell.x, cell.y] = bi.instanceId;
            }

            NotifyOccupancyChanged();
            return true;
        }

        // ── 查询 ─────────────────────────────────────────────────

        /// <summary> 指定格子是否被建筑占用 </summary>
        public bool IsOccupied(Vector2Int pos)
        {
            if (!InBounds(pos)) return false;
            return occupied[pos.x, pos.y];
        }

        /// <summary> 指定格子是否可行走（配置层，不受建筑影响） </summary>
        public bool IsWalkable(Vector2Int pos)
        {
            if (!InBounds(pos)) return false;
            return walkable[pos.x, pos.y];
        }

        /// <summary> 指定格子是否可放置建筑（配置层） </summary>
        public bool IsPlaceable(Vector2Int pos)
        {
            if (!InBounds(pos)) return false;
            return placeable[pos.x, pos.y];
        }

        /// <summary> 获取指定格子的占用者 ID，无占用返回 null </summary>
        public string GetOccupantId(Vector2Int pos)
        {
            if (!InBounds(pos)) return null;
            return occupantId[pos.x, pos.y];
        }

        /// <summary> 根据 instanceId 获取建筑实例 </summary>
        public BuildingInstance GetBuilding(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            buildings.TryGetValue(id, out var bi);
            return bi;
        }

        /// <summary> 遍历所有已注册的建筑 </summary>
        public IEnumerable<BuildingInstance> AllBuildings()
        {
            return buildings.Values;
        }

        /// <summary> 遍历所有货架实例 </summary>
        public IEnumerable<ShelfInstance> AllShelves()
        {
            foreach (var bi in buildings.Values)
            {
                if (bi is ShelfInstance shelf)
                    yield return shelf;
            }
        }

        /// <summary> 遍历所有设施实例 </summary>
        public IEnumerable<FacilityInstance> AllFacilities()
        {
            foreach (var bi in buildings.Values)
            {
                if (bi is FacilityInstance facility)
                    yield return facility;
            }
        }

        /// <summary> 检查是否存在指定类型的设施 </summary>
        public bool HasFacilityOfType(FacilityType type)
        {
            foreach (var bi in buildings.Values)
            {
                if (bi is FacilityInstance fi
                    && fi.archetype is FacilityArchetype fa
                    && fa.facilityType == type)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
