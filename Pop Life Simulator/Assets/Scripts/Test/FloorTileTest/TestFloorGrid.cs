using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Test
{
    /// <summary>
    /// 独立测试用网格系统 — 稀疏字典，无固定边界
    /// 双层：地板层 + 建筑层
    /// 支持默认一楼、堆叠规则、带建筑移动
    /// </summary>
    public class TestFloorGrid : MonoBehaviour
    {
        [Header("网格配置")]
        public float cellSize = 1f;
        public Transform buildingContainer;
        public Transform origin;

        [Header("默认一楼")]
        [SerializeField] private FloorTileArchetype defaultFloorArchetype;
        [SerializeField] private Vector2Int defaultFloorPosition = Vector2Int.zero;
        [SerializeField] private int defaultFloorRotation;

        [Header("Gizmo（仅视觉参考）")]
        public Vector2Int gizmoSize = new(20, 15);
        public Color emptyColor = new(0.3f, 0.3f, 0.3f, 0.15f);
        public Color floorColor = new(0.2f, 0.6f, 1f, 0.4f);
        public Color occupiedColor = new(0.2f, 1f, 0.2f, 0.4f);
        public Color defaultFloorColor = new(0.8f, 0.6f, 0.2f, 0.4f);

        public Vector3 OriginPos => origin ? origin.position : transform.position;

        /// <summary> 地板/建筑变化时触发（供导航系统订阅） </summary>
        public event Action OnFloorChanged;

        /// <summary> 只读访问地板格子集合 </summary>
        public IReadOnlyCollection<Vector2Int> FloorCells => floorTileCells;

        /// <summary> 只读访问建筑格子集合 </summary>
        public IReadOnlyCollection<Vector2Int> BuildingCells => buildingCells;

        /// <summary> 单格查询是否有建筑 </summary>
        public bool HasBuildingAt(Vector2Int pos) => buildingCells.Contains(pos);

        /// <summary> 获取默认一楼实例 </summary>
        public FloorTileInstance GetDefaultFloor()
        {
            foreach (var kv in floorTiles)
                if (kv.Value.isDefault) return kv.Value;
            return null;
        }

        // 地板层（稀疏）
        private readonly HashSet<Vector2Int> floorTileCells = new();
        private readonly Dictionary<Vector2Int, string> floorTileOwnerMap = new();
        private readonly Dictionary<string, FloorTileInstance> floorTiles = new();

        // 建筑层（稀疏）
        private readonly HashSet<Vector2Int> buildingCells = new();
        private readonly Dictionary<Vector2Int, string> buildingOwnerMap = new();
        private readonly Dictionary<string, TestShelfInstance> shelves = new();

        // 电梯
        [Serializable]
        public struct ElevatorData
        {
            public Vector2Int bottom;
            public Vector2Int top;
            public GameObject go;
        }
        private readonly List<ElevatorData> elevators = new();

        /// <summary> 只读访问电梯列表 </summary>
        public IReadOnlyList<ElevatorData> Elevators => elevators;

        void Start()
        {
            PlaceDefaultFloor();
        }

        // ========== 默认一楼 ==========

        private void PlaceDefaultFloor()
        {
            if (defaultFloorArchetype == null) return;

            var inst = PlaceFloorTileInternal(defaultFloorArchetype, defaultFloorPosition, defaultFloorRotation);
            if (inst != null)
                inst.isDefault = true;
            OnFloorChanged?.Invoke();
        }

        // ========== 坐标转换 ==========

        public Vector3 GridToWorld(Vector2Int g)
            => OriginPos + new Vector3(g.x * cellSize, g.y * cellSize, 0);

        public Vector2Int WorldToGrid(Vector3 w)
        {
            var local = w - OriginPos;
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.y / cellSize));
        }

        // ========== 地板放置 ==========

        /// <summary>
        /// 检查是否可以放置地板
        /// 规则: 无重叠 + 底部至少一格有地板支撑（默认一楼除外）
        /// </summary>
        public bool CanPlaceFloorTile(List<Vector2Int> fp, Vector2Int gridOrigin, bool skipSupportCheck = false)
        {
            // 检查无重叠
            foreach (var off in fp)
            {
                var p = gridOrigin + off;
                if (floorTileCells.Contains(p)) return false;
            }

            // 堆叠规则：底部需要支撑
            if (!skipSupportCheck)
            {
                if (!HasSupportBelow(fp, gridOrigin))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 检查 footprint 底部是否有地板支撑
        /// 底部 = footprint 中 y 最小的那一行
        /// 至少一个底部格子的正下方 (y-1) 有地板
        /// </summary>
        private bool HasSupportBelow(List<Vector2Int> fp, Vector2Int gridOrigin)
        {
            // 找 footprint 中最小的 y（世界坐标）
            int minY = int.MaxValue;
            foreach (var off in fp)
            {
                int worldY = gridOrigin.y + off.y;
                if (worldY < minY) minY = worldY;
            }

            // 检查底部行的正下方是否有地板
            foreach (var off in fp)
            {
                int worldY = gridOrigin.y + off.y;
                if (worldY == minY)
                {
                    var below = new Vector2Int(gridOrigin.x + off.x, worldY - 1);
                    if (floorTileCells.Contains(below))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 放置地板瓦片（玩家操作，受堆叠规则约束）
        /// </summary>
        public FloorTileInstance PlaceFloorTile(FloorTileArchetype arch, Vector2Int pos, int rot)
        {
            var fp = arch.GetFootprint(rot);
            if (!CanPlaceFloorTile(fp, pos))
                return null;

            var inst = PlaceFloorTileInternal(arch, pos, rot);
            if (inst != null) OnFloorChanged?.Invoke();
            return inst;
        }

        /// <summary>
        /// 内部放置（跳过堆叠检查，用于默认一楼和重新注册）
        /// </summary>
        private FloorTileInstance PlaceFloorTileInternal(FloorTileArchetype arch, Vector2Int pos, int rot)
        {
            var fp = arch.GetFootprint(rot);

            // 实例化
            var world = GridToWorld(pos);
            GameObject go;
            if (arch.prefab != null)
            {
                go = Instantiate(arch.prefab, world, Quaternion.identity, buildingContainer);
            }
            else
            {
                go = CreatePlaceholderFloorTile(arch, rot);
                go.transform.SetParent(buildingContainer);
                go.transform.position = world;
            }

            var inst = go.GetComponent<FloorTileInstance>();
            if (inst == null) inst = go.AddComponent<FloorTileInstance>();
            inst.archetype = arch;
            inst.instanceId = Guid.NewGuid().ToString();
            inst.gridPosition = pos;
            inst.rotation = rot;

            // 标记地板层
            foreach (var off in fp)
            {
                var p = pos + off;
                floorTileCells.Add(p);
                floorTileOwnerMap[p] = inst.instanceId;
            }

            floorTiles[inst.instanceId] = inst;
            return inst;
        }

        /// <summary>
        /// 检查地板上是否有建筑
        /// </summary>
        public bool HasBuildingsOnFloorTile(FloorTileInstance inst)
        {
            var fp = inst.archetype.GetFootprint(inst.rotation);
            foreach (var off in fp)
            {
                var p = inst.gridPosition + off;
                if (buildingCells.Contains(p))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取地板上的所有建筑
        /// </summary>
        public List<TestShelfInstance> GetBuildingsOnFloorTile(FloorTileInstance inst)
        {
            var result = new List<TestShelfInstance>();
            var ids = new HashSet<string>();
            var fp = inst.archetype.GetFootprint(inst.rotation);
            foreach (var off in fp)
            {
                var p = inst.gridPosition + off;
                if (buildingOwnerMap.TryGetValue(p, out var ownerId) && ids.Add(ownerId))
                {
                    if (shelves.TryGetValue(ownerId, out var shelf))
                        result.Add(shelf);
                }
            }
            return result;
        }

        /// <summary>
        /// 移除地板（地板上不能有建筑）
        /// </summary>
        public bool RemoveFloorTile(FloorTileInstance inst)
        {
            if (inst.isDefault) return false;
            if (HasBuildingsOnFloorTile(inst)) return false;

            // 检查移除后是否会导致上方的地板失去支撑
            if (WouldBreakSupportAbove(inst)) return false;

            ClearFloorTileCells(inst);
            floorTiles.Remove(inst.instanceId);
            Destroy(inst.gameObject);
            OnFloorChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 检查移除此地板后，是否有上方的地板会失去支撑
        /// </summary>
        private bool WouldBreakSupportAbove(FloorTileInstance inst)
        {
            var fp = inst.archetype.GetFootprint(inst.rotation);
            var cellsToRemove = new HashSet<Vector2Int>();
            foreach (var off in fp)
                cellsToRemove.Add(inst.gridPosition + off);

            // 找所有直接在上方的地板实例
            var aboveIds = new HashSet<string>();
            foreach (var cell in cellsToRemove)
            {
                var above = new Vector2Int(cell.x, cell.y + 1);
                if (floorTileOwnerMap.TryGetValue(above, out var id) && id != inst.instanceId)
                    aboveIds.Add(id);
            }

            // 检查每个上方的地板是否还有其他支撑
            foreach (var id in aboveIds)
            {
                if (!floorTiles.TryGetValue(id, out var aboveInst)) continue;
                var aboveFp = aboveInst.archetype.GetFootprint(aboveInst.rotation);

                // 找底部行
                int minY = int.MaxValue;
                foreach (var off in aboveFp)
                {
                    int wy = aboveInst.gridPosition.y + off.y;
                    if (wy < minY) minY = wy;
                }

                // 检查底部行是否还有支撑（排除即将移除的格子）
                bool stillSupported = false;
                foreach (var off in aboveFp)
                {
                    int wy = aboveInst.gridPosition.y + off.y;
                    if (wy == minY)
                    {
                        var below = new Vector2Int(aboveInst.gridPosition.x + off.x, wy - 1);
                        if (!cellsToRemove.Contains(below) && floorTileCells.Contains(below))
                        {
                            stillSupported = true;
                            break;
                        }
                    }
                }

                if (!stillSupported) return true; // 会导致上方地板失去支撑
            }

            return false;
        }

        /// <summary>
        /// 临时反注册地板（不销毁 GO，用于移动）
        /// </summary>
        public void UnregisterFloorTile(FloorTileInstance inst)
        {
            ClearFloorTileCells(inst);
            floorTiles.Remove(inst.instanceId);
        }

        /// <summary>
        /// 重新注册地板到新位置（不创建新 GO）
        /// </summary>
        public bool ReregisterFloorTile(FloorTileInstance inst, Vector2Int newPos, int newRot, bool skipSupportCheck = false)
        {
            var fp = inst.archetype.GetFootprint(newRot);

            // 检查无重叠
            foreach (var off in fp)
            {
                var p = newPos + off;
                if (floorTileCells.Contains(p)) return false;
            }

            // 堆叠检查
            if (!skipSupportCheck && !HasSupportBelow(fp, newPos))
                return false;

            foreach (var off in fp)
            {
                var p = newPos + off;
                floorTileCells.Add(p);
                floorTileOwnerMap[p] = inst.instanceId;
            }
            floorTiles[inst.instanceId] = inst;
            inst.gridPosition = newPos;
            inst.rotation = newRot;
            inst.transform.position = GridToWorld(newPos);
            OnFloorChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 单格查询是否有地板
        /// </summary>
        public bool HasFloorTileAt(Vector2Int pos)
            => floorTileCells.Contains(pos);

        /// <summary>
        /// 获取指定位置的地板实例
        /// </summary>
        public FloorTileInstance GetFloorTileAt(Vector2Int pos)
        {
            if (!floorTileOwnerMap.TryGetValue(pos, out var id)) return null;
            floorTiles.TryGetValue(id, out var inst);
            return inst;
        }

        private void ClearFloorTileCells(FloorTileInstance inst)
        {
            var fp = inst.archetype.GetFootprint(inst.rotation);
            foreach (var off in fp)
            {
                var p = inst.gridPosition + off;
                floorTileCells.Remove(p);
                floorTileOwnerMap.Remove(p);
            }
        }

        // ========== 建筑放置 ==========

        /// <summary>
        /// 检查是否可以放置建筑（未占用 + 所有格子有地板）
        /// </summary>
        public bool CanPlaceBuilding(List<Vector2Int> fp, Vector2Int gridOrigin)
        {
            foreach (var off in fp)
            {
                var p = gridOrigin + off;
                if (buildingCells.Contains(p)) return false;
                if (!floorTileCells.Contains(p)) return false; // 必须在地板上
            }
            return true;
        }

        /// <summary>
        /// 放置建筑
        /// </summary>
        public TestShelfInstance PlaceBuilding(TestShelfArchetype arch, Vector2Int pos, int rot)
        {
            var fp = arch.footprint;
            if (!CanPlaceBuilding(fp, pos)) return null;

            var world = GridToWorld(pos);
            GameObject go;
            if (arch.prefab != null)
            {
                go = Instantiate(arch.prefab, world, Quaternion.identity, buildingContainer);
            }
            else
            {
                go = CreatePlaceholderShelf();
                go.transform.SetParent(buildingContainer);
                go.transform.position = world;
            }

            var inst = go.GetComponent<TestShelfInstance>();
            if (inst == null) inst = go.AddComponent<TestShelfInstance>();
            inst.archetype = arch;
            inst.instanceId = Guid.NewGuid().ToString();
            inst.gridPosition = pos;

            foreach (var off in fp)
            {
                var p = pos + off;
                buildingCells.Add(p);
                buildingOwnerMap[p] = inst.instanceId;
            }

            shelves[inst.instanceId] = inst;
            OnFloorChanged?.Invoke();
            return inst;
        }

        /// <summary>
        /// 移除建筑
        /// </summary>
        public bool RemoveBuilding(TestShelfInstance inst)
        {
            ClearBuildingCells(inst);
            shelves.Remove(inst.instanceId);
            Destroy(inst.gameObject);
            OnFloorChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 临时反注册建筑（不销毁 GO）
        /// </summary>
        public void UnregisterBuilding(TestShelfInstance inst)
        {
            ClearBuildingCells(inst);
            shelves.Remove(inst.instanceId);
        }

        /// <summary>
        /// 重新注册建筑到新位置（不创建新 GO）
        /// </summary>
        public bool ReregisterBuilding(TestShelfInstance inst, Vector2Int newPos)
        {
            var fp = inst.archetype.footprint;
            if (!CanPlaceBuilding(fp, newPos)) return false;

            foreach (var off in fp)
            {
                var p = newPos + off;
                buildingCells.Add(p);
                buildingOwnerMap[p] = inst.instanceId;
            }
            shelves[inst.instanceId] = inst;
            inst.gridPosition = newPos;
            inst.transform.position = GridToWorld(newPos);
            OnFloorChanged?.Invoke();
            return true;
        }

        private void ClearBuildingCells(TestShelfInstance inst)
        {
            var fp = inst.archetype.footprint;
            foreach (var off in fp)
            {
                var p = inst.gridPosition + off;
                buildingCells.Remove(p);
                buildingOwnerMap.Remove(p);
            }
        }

        // ========== 带建筑移动地板 ==========

        /// <summary>
        /// 验证地板连同建筑是否可以移动到新位置
        /// </summary>
        public bool CanMoveFloorTileWithBuildings(
            FloorTileInstance floorInst, List<TestShelfInstance> buildingsOnTile,
            Vector2Int newPos, int newRot)
        {
            var fp = floorInst.archetype.GetFootprint(newRot);

            // 检查地板新位置无重叠
            foreach (var off in fp)
            {
                var p = newPos + off;
                if (floorTileCells.Contains(p)) return false;
            }

            // 堆叠支撑检查
            if (!HasSupportBelow(fp, newPos))
                return false;

            // 计算地板移动偏移
            var delta = newPos - floorInst.gridPosition;

            // 检查每个建筑在新位置是否合法
            // 新地板格子集合
            var newFloorCells = new HashSet<Vector2Int>();
            foreach (var off in fp)
                newFloorCells.Add(newPos + off);

            foreach (var shelf in buildingsOnTile)
            {
                var newShelfPos = shelf.gridPosition + delta;
                foreach (var off in shelf.archetype.footprint)
                {
                    var p = newShelfPos + off;
                    // 建筑必须在新地板上
                    if (!newFloorCells.Contains(p) && !floorTileCells.Contains(p))
                        return false;
                    // 建筑不能和其他建筑重叠（排除自身已反注册）
                    if (buildingCells.Contains(p))
                        return false;
                }
            }

            return true;
        }

        // ========== 电梯 ==========

        /// <summary> 获取格子所属 FloorTile 的 instanceId（null = 无地板） </summary>
        public string GetFloorTileOwnerId(Vector2Int pos)
        {
            floorTileOwnerMap.TryGetValue(pos, out var id);
            return id;
        }

        /// <summary> 检查两个 FloorTile 是否为相邻楼层（共享垂直边界） </summary>
        public bool AreAdjacentFloors(FloorTileInstance a, FloorTileInstance b)
        {
            if (a == null || b == null || a.instanceId == b.instanceId) return false;

            var fpA = a.archetype.GetFootprint(a.rotation);
            var fpB = b.archetype.GetFootprint(b.rotation);

            // 检查是否存在 cellA ∈ A 和 cellB ∈ B，使得 cellB.y == cellA.y + 1 且 cellB.x == cellA.x
            var cellsA = new HashSet<Vector2Int>();
            foreach (var off in fpA) cellsA.Add(a.gridPosition + off);

            foreach (var off in fpB)
            {
                var cellB = b.gridPosition + off;
                var belowB = new Vector2Int(cellB.x, cellB.y - 1);
                if (cellsA.Contains(belowB)) return true;
            }

            // 反向也检查（A 在 B 上方）
            var cellsBSet = new HashSet<Vector2Int>();
            foreach (var off in fpB) cellsBSet.Add(b.gridPosition + off);

            foreach (var off in fpA)
            {
                var cellA = a.gridPosition + off;
                var belowA = new Vector2Int(cellA.x, cellA.y - 1);
                if (cellsBSet.Contains(belowA)) return true;
            }

            return false;
        }

        /// <summary>
        /// 放置电梯：连接 bottomPos 和 topPos，必须在相邻楼层的不同 FloorTile 上
        /// </summary>
        public bool PlaceElevator(Vector2Int bottomPos, Vector2Int topPos)
        {
            // 两格都必须有地板
            if (!HasFloorTileAt(bottomPos) || !HasFloorTileAt(topPos)) return false;

            // 两格不能有非电梯建筑（已有电梯可以复用）
            if (HasBuildingAt(bottomPos) && !IsElevatorAt(bottomPos)) return false;
            if (HasBuildingAt(topPos) && !IsElevatorAt(topPos)) return false;

            // 两格必须属于不同 FloorTile
            var bottomOwner = GetFloorTileOwnerId(bottomPos);
            var topOwner = GetFloorTileOwnerId(topPos);
            if (bottomOwner == topOwner) return false;

            // 必须是相邻楼层
            var bottomTile = GetFloorTileAt(bottomPos);
            var topTile = GetFloorTileAt(topPos);
            if (!AreAdjacentFloors(bottomTile, topTile)) return false;

            // 占用 buildingCells（已有电梯的格子不重复添加）
            if (!IsElevatorAt(bottomPos))
            {
                buildingCells.Add(bottomPos);
                buildingOwnerMap[bottomPos] = "elevator";
            }
            if (!IsElevatorAt(topPos))
            {
                buildingCells.Add(topPos);
                buildingOwnerMap[topPos] = "elevator";
            }

            bool wasBottomElevator = IsElevatorAt(bottomPos);
            bool wasTopElevator = IsElevatorAt(topPos);

            // 创建 NodeLink2 GameObject
            var bottomWorld = GridToWorld(bottomPos) + new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0);
            var topWorld = GridToWorld(topPos) + new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0);

            var linkGo = new GameObject($"Elevator_{bottomPos}_{topPos}");
            linkGo.transform.SetParent(buildingContainer);
            linkGo.transform.position = bottomWorld;

            var link = linkGo.AddComponent<Pathfinding.NodeLink2>();
            // NodeLink2 的 end 用一个子物体的 Transform
            var endGo = new GameObject("End");
            endGo.transform.SetParent(linkGo.transform);
            endGo.transform.position = topWorld;
            link.end = endGo.transform;

            // 视觉标记（只在新电梯格子上创建，已有电梯的格子跳过）
            if (!wasBottomElevator)
                CreateElevatorMarker(linkGo.transform, Vector3.zero);
            if (!wasTopElevator)
                CreateElevatorMarker(endGo.transform, Vector3.zero);

            elevators.Add(new ElevatorData { bottom = bottomPos, top = topPos, go = linkGo });
            OnFloorChanged?.Invoke();
            return true;
        }

        /// <summary> 移除指定位置的电梯 </summary>
        public bool RemoveElevator(Vector2Int pos)
        {
            for (int i = elevators.Count - 1; i >= 0; i--)
            {
                var e = elevators[i];
                if (e.bottom == pos || e.top == pos)
                {
                    Destroy(e.go);
                    elevators.RemoveAt(i);

                    // 只在没有其他电梯使用该格子时释放 buildingCells
                    if (!IsElevatorAt(e.bottom))
                    {
                        buildingCells.Remove(e.bottom);
                        buildingOwnerMap.Remove(e.bottom);
                    }
                    if (!IsElevatorAt(e.top))
                    {
                        buildingCells.Remove(e.top);
                        buildingOwnerMap.Remove(e.top);
                    }

                    OnFloorChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        /// <summary> 查询位置是否有电梯 </summary>
        public bool IsElevatorAt(Vector2Int pos)
        {
            foreach (var e in elevators)
                if (e.bottom == pos || e.top == pos) return true;
            return false;
        }

        private void CreateElevatorMarker(Transform parent, Vector3 localPos)
        {
            var marker = new GameObject("Marker");
            marker.transform.SetParent(parent);
            marker.transform.localPosition = localPos;
            var sr = marker.AddComponent<SpriteRenderer>();
            // 居中 pivot 的 sprite
            int ppu = 16;
            var tex = new Texture2D(ppu, ppu);
            tex.filterMode = FilterMode.Point;
            var color = new Color(1f, 0.8f, 0.2f, 0.8f);
            var pixels = new Color[ppu * ppu];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, ppu, ppu), new Vector2(0.5f, 0.5f), ppu);
            sr.sortingOrder = 20;
        }

        // ========== 占位符生成 ==========

        private GameObject CreatePlaceholderFloorTile(FloorTileArchetype arch, int rotation)
        {
            var size = arch.GetEffectiveSize(rotation);
            var go = new GameObject($"FloorTile_{size.x}x{size.y}");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRectSprite(size.x, size.y, new Color(0.3f, 0.5f, 0.8f, 0.6f));
            sr.sortingOrder = -10;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(size.x * cellSize, size.y * cellSize);
            col.offset = new Vector2(size.x * cellSize * 0.5f, size.y * cellSize * 0.5f);

            go.AddComponent<FloorTileInstance>();
            return go;
        }

        private GameObject CreatePlaceholderShelf()
        {
            var go = new GameObject("TestShelf");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateRectSprite(1, 1, new Color(0.2f, 0.8f, 0.2f, 0.8f));
            sr.sortingOrder = 10;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(cellSize * 0.9f, cellSize * 0.9f);
            col.offset = new Vector2(cellSize * 0.5f, cellSize * 0.5f);

            go.AddComponent<TestShelfInstance>();
            return go;
        }

        private Sprite CreateRectSprite(int widthCells, int heightCells, Color color)
        {
            int ppu = 16;
            int w = widthCells * ppu;
            int h = heightCells * ppu;
            var tex = new Texture2D(w, h);
            tex.filterMode = FilterMode.Point;

            var borderColor = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, color.a);
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    bool isGridLine = x % ppu == 0 || y % ppu == 0;
                    pixels[y * w + x] = (isBorder || isGridLine) ? borderColor : color;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, ppu);
        }

        // ========== Gizmo ==========

        void OnDrawGizmos()
        {
            var o = OriginPos;

            // 绘制参考网格
            Gizmos.color = emptyColor;
            for (int x = 0; x < gizmoSize.x; x++)
            {
                for (int y = 0; y < gizmoSize.y; y++)
                {
                    var p = o + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0);
                    Gizmos.DrawWireCube(p, Vector3.one * cellSize * 0.95f);
                }
            }

            if (!Application.isPlaying) return;

            // 绘制有地板的格子
            foreach (var cell in floorTileCells)
            {
                var p = o + new Vector3((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize, 0);

                if (buildingCells.Contains(cell))
                    Gizmos.color = occupiedColor;
                else if (floorTileOwnerMap.TryGetValue(cell, out var ownerId) &&
                         floorTiles.TryGetValue(ownerId, out var inst) && inst.isDefault)
                    Gizmos.color = defaultFloorColor;
                else
                    Gizmos.color = floorColor;

                Gizmos.DrawCube(p, Vector3.one * cellSize * 0.9f);
            }
        }
    }
}
