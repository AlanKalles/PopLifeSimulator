using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    /// <summary>
    /// 全局世界网格（单例）—— 只管理 FloorTile 的放置、支撑/堆叠规则和电梯。
    /// 货架/设施的放置由各 FloorTileInstance.InteriorGrid 管理。
    /// </summary>
    public class WorldGrid : MonoBehaviour
    {
        // ================================================================
        //  单例
        // ================================================================

        public static WorldGrid Instance { get; private set; }

        // ================================================================
        //  配置字段
        // ================================================================

        [Header("网格配置")]
        public Vector2Int gridSize = new(20, 10);
        public float cellSize = 1f;
        public Transform buildingContainer;
        public Transform origin;

        [Header("预设楼层")]
        [SerializeField] private List<PresetFloor> presetFloors;

        [Header("Level Design Only")]
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // ================================================================
        //  数据
        // ================================================================

        private Vector3 OriginPos => origin ? origin.position : transform.position;

        // 地板占用层
        private bool[,] floorTileLayer;
        private string[,] floorTileOwnerId;
        private readonly Dictionary<string, FloorTileInstance> floorTileInstances = new();

        // 电梯
        [Serializable]
        public struct ElevatorData
        {
            public string startTileId;
            public Vector2Int startLocalCell;
            public string endTileId;
            public Vector2Int endLocalCell;
            public GameObject go;
        }
        private readonly List<ElevatorData> elevators = new();

        // ================================================================
        //  事件
        // ================================================================

        /// <summary> 地板结构变化时触发（放置/移除/移动 FloorTile、电梯变化） </summary>
        public event Action OnStructureChanged;

        /// <summary> 公共触发点，供外部类调用 </summary>
        public void NotifyStructureChanged() => OnStructureChanged?.Invoke();

        // ================================================================
        //  只读属性
        // ================================================================

        public IReadOnlyCollection<Vector2Int> FloorCells
        {
            get
            {
                var set = new HashSet<Vector2Int>();
                for (int x = 0; x < gridSize.x; x++)
                    for (int y = 0; y < gridSize.y; y++)
                        if (floorTileLayer[x, y]) set.Add(new Vector2Int(x, y));
                return set;
            }
        }

        public IReadOnlyList<ElevatorData> Elevators => elevators;

        // ================================================================
        //  内部类型
        // ================================================================

        [Serializable]
        public class PresetFloor
        {
            public FloorTileArchetype archetype;
            public Vector2Int position;
            public int rotation;
            public bool isDefault;
        }

        // ================================================================
        //  生命周期
        // ================================================================

        void Awake()
        {
            Instance = this;
            Init();
        }

        void Start()
        {
            // 放置预设楼层
            PlacePresetFloors();

            // 注册场景中已有的子物体（FloorTile + interior 建筑）
            // 即使已有预设楼层也需要执行，因为 Phase 2 负责将已有 shelf/facility 注册到 InteriorGrid
            RegisterAllChildBuildings();
        }

        // ================================================================
        //  初始化与重建
        // ================================================================

        public void Init()
        {
            floorTileLayer = new bool[gridSize.x, gridSize.y];
            floorTileOwnerId = new string[gridSize.x, gridSize.y];
        }

        public void RebuildFromScene()
        {
            Init();
            floorTileInstances.Clear();
            RegisterAllChildBuildings();
        }

        // ================================================================
        //  预设楼层
        // ================================================================

        private void PlacePresetFloors()
        {
            if (presetFloors == null) return;

            foreach (var preset in presetFloors)
            {
                if (preset.archetype == null) continue;

                var fp = preset.archetype.GetRotatedFootprint(preset.rotation);

                // 第一个预设楼层（通常是一楼）跳过支撑检查，后续需要支撑
                bool isFirst = floorTileInstances.Count == 0;
                if (!isFirst && !CanPlaceFloorTile(fp, preset.position))
                {
                    Debug.LogWarning($"WorldGrid: Preset floor '{preset.archetype.displayName}' at {preset.position} fails placement validation, skipping");
                    continue;
                }

                // 即使是第一个也要检查范围和重叠
                if (isFirst)
                {
                    bool valid = true;
                    foreach (var off in fp)
                    {
                        var p = preset.position + off;
                        if (!InBounds(p) || floorTileLayer[p.x, p.y]) { valid = false; break; }
                    }
                    if (!valid)
                    {
                        Debug.LogWarning($"WorldGrid: Preset floor '{preset.archetype.displayName}' at {preset.position} out of bounds or overlaps, skipping");
                        continue;
                    }
                }

                var inst = PlaceFloorTileInternal(preset.archetype, preset.position, preset.rotation);
                if (inst != null)
                    inst.IsDefault = preset.isDefault;
            }
            NotifyStructureChanged();
        }

        // ================================================================
        //  场景子物体注册
        // ================================================================

        /// <summary>
        /// 两遍注册：
        ///   1) FloorTile → WorldGrid
        ///   2) 各 FloorTile 内部建筑 → InteriorGrid
        /// </summary>
        private void RegisterAllChildBuildings()
        {
            if (buildingContainer == null) return;

            BuildingInstance[] allBuildings = buildingContainer.GetComponentsInChildren<BuildingInstance>();

            // 第一遍：注册 FloorTile
            foreach (var building in allBuildings)
            {
                if (building is not FloorTileInstance fti) continue;
                RegisterChildBuilding(fti);
            }

            // 第二遍：各 FloorTile 注册其内部建筑到 InteriorGrid
            foreach (var kv in floorTileInstances)
            {
                kv.Value.RegisterExistingBuildingsInInterior();
            }
        }

        /// <summary> 注册单个 FloorTileInstance（跳过非 FloorTile） </summary>
        private void RegisterChildBuilding(BuildingInstance building)
        {
            if (building is not FloorTileInstance fti) return;

            // 跳过已注册
            if (!string.IsNullOrEmpty(building.instanceId) && floorTileInstances.ContainsKey(building.instanceId))
                return;

            if (building.archetype == null)
            {
                Debug.LogWarning($"WorldGrid: Building {building.name} missing archetype, skipping");
                return;
            }

            // 推断位置
            Vector2Int gridPos;
            int rotation;

            if (!string.IsNullOrEmpty(building.instanceId))
            {
                gridPos = building.gridPosition;
                rotation = building.rotation;
            }
            else
            {
                gridPos = WorldToGrid(building.transform.position);
                rotation = Mathf.RoundToInt(building.transform.eulerAngles.z / 90f) % 4;
            }

            var footprint = building.archetype.GetRotatedFootprint(rotation);

            // 验证（跳过支撑检查，因为可能是存档或编辑器中已摆好的）
            if (!CanPlaceFloorTile(footprint, gridPos, skipSupportCheck: true))
            {
                Debug.LogWarning($"WorldGrid: FloorTile '{building.name}' at {gridPos} conflicts, skipping");
                return;
            }

            building.rotation = rotation;
            building.gridPosition = gridPos;

            if (string.IsNullOrEmpty(building.instanceId))
                building.instanceId = Guid.NewGuid().ToString();

            RegisterFloorTileInternal(fti, footprint, gridPos);

            // 初始化 InteriorGrid
            fti.InitializeInterior();
        }

        // ================================================================
        //  FloorTile 注册内部
        // ================================================================

        private void RegisterFloorTileInternal(FloorTileInstance inst, List<Vector2Int> fp, Vector2Int pos)
        {
            // 标记全占地
            foreach (var off in fp)
            {
                var p = pos + off;
                if (InBounds(p))
                {
                    floorTileLayer[p.x, p.y] = true;
                    floorTileOwnerId[p.x, p.y] = inst.instanceId;
                }
            }

            floorTileInstances[inst.instanceId] = inst;
        }

        // ================================================================
        //  FloorTile 放置校验
        // ================================================================

        /// <summary>
        /// 检查地板是否可放置
        /// 规则: 范围内 + 无地板重叠 + 底部有支撑（默认楼层除外）
        /// </summary>
        public bool CanPlaceFloorTile(List<Vector2Int> fp, Vector2Int origin, bool skipSupportCheck = false)
        {
            foreach (var off in fp)
            {
                var p = origin + off;
                if (!InBounds(p)) return false;
                if (floorTileLayer[p.x, p.y]) return false;
            }

            if (!skipSupportCheck && !HasSupport(fp, origin))
                return false;

            return true;
        }

        /// <summary>
        /// 检查 footprint 是否有支撑（垂直下方 或 横向连接）
        /// 垂直: 底行至少一格的 y-1 有地板
        /// 横向: 左侧或右侧整列紧贴已有 FloorTile，且接触面格子数一致
        /// </summary>
        private bool HasSupport(List<Vector2Int> fp, Vector2Int origin)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var off in fp)
            {
                var p = origin + off;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            int height = maxY - minY + 1;

            // 垂直支撑: 底行至少一格正下方有地板
            foreach (var off in fp)
            {
                var p = origin + off;
                if (p.y == minY)
                {
                    var below = new Vector2Int(p.x, p.y - 1);
                    if (InBounds(below) && floorTileLayer[below.x, below.y])
                        return true;
                }
            }

            // 横向支撑: 左侧 (minX-1) 或右侧 (maxX+1)
            if (HasHorizontalSupport(minX - 1, minY, height))
                return true;
            if (HasHorizontalSupport(maxX + 1, minY, height))
                return true;

            return false;
        }

        /// <summary>
        /// 检查指定列是否存在一个 FloorTile 的边缘，且接触面高度与 height 一致
        /// </summary>
        private bool HasHorizontalSupport(int x, int minY, int height)
        {
            string ownerId = null;
            for (int y = minY; y < minY + height; y++)
            {
                var pos = new Vector2Int(x, y);
                if (!InBounds(pos) || !floorTileLayer[pos.x, pos.y]) return false;

                var id = floorTileOwnerId[pos.x, pos.y];
                if (id == null) return false;
                if (ownerId == null) ownerId = id;
                else if (ownerId != id) return false;
            }

            if (ownerId == null || !floorTileInstances.TryGetValue(ownerId, out var neighbor))
                return false;

            // 该 FloorTile 在 x 列上的格子数必须等于 height
            var nFp = neighbor.archetype.GetRotatedFootprint(neighbor.rotation);
            int neighborHeightAtX = 0;
            foreach (var off in nFp)
            {
                var p = neighbor.gridPosition + off;
                if (p.x == x) neighborHeightAtX++;
            }
            return neighborHeightAtX == height;
        }

        // ================================================================
        //  FloorTile 事务式放置（玩家操作）
        // ================================================================

        /// <summary> 放置地板瓦片（玩家操作，受堆叠规则约束） </summary>
        public FloorTileInstance PlaceFloorTileTransactional(FloorTileArchetype arch, Vector2Int pos, int rot)
        {
            var fp = arch.GetRotatedFootprint(rot);
            if (!CanPlaceFloorTile(fp, pos)) return null;

            if (!BlueprintManager.Instance.HasBlueprint(arch.archetypeId)) return null;

            int finalBuildCost = GlobalModifierManager.Instance != null
                ? Mathf.RoundToInt(arch.buildCost * GlobalModifierManager.Instance.GetConstructionCostMultiplier())
                : arch.buildCost;
            if (!ResourceManager.Instance.CanAfford(finalBuildCost, 0)) return null;

            ResourceManager.Instance.SpendMoney(finalBuildCost);
            BlueprintManager.Instance.ConsumeBlueprint(arch.archetypeId);

            FloorTileInstance inst = null;
            try
            {
                inst = PlaceFloorTileInternal(arch, pos, rot);
                NotifyStructureChanged();
                return inst;
            }
            catch
            {
                ResourceManager.Instance.RefundMoney(finalBuildCost);
                if (inst) Destroy(inst.gameObject);
                return null;
            }
        }

        /// <summary> 内部放置（跳过蓝图和资源检查，用于预设楼层和重注册） </summary>
        private FloorTileInstance PlaceFloorTileInternal(FloorTileArchetype arch, Vector2Int pos, int rot)
        {
            var fp = arch.GetRotatedFootprint(rot);
            var world = GridToWorld(pos);
            var go = Instantiate(arch.prefab, world, Quaternion.Euler(0, 0, rot * 90), buildingContainer);
            var inst = go.GetComponent<FloorTileInstance>();
            inst.rotation = rot;
            inst.Initialize(arch, pos, ""); // hostTileId = "" for FloorTiles (they ARE the tile)

            RegisterFloorTileInternal(inst, fp, pos);

            // 初始化 InteriorGrid
            inst.InitializeInterior();

            return inst;
        }

        // ================================================================
        //  FloorTile 查询
        // ================================================================

        /// <summary> 检查地板上是否有建筑（通过 InteriorGrid） </summary>
        public bool HasBuildingsOnFloorTile(FloorTileInstance inst)
        {
            return inst != null && inst.HasBuildingsInInterior();
        }

        /// <summary> 获取地板上的所有建筑（通过 InteriorGrid） </summary>
        public List<BuildingInstance> GetBuildingsOnFloorTile(FloorTileInstance inst)
        {
            if (inst == null) return new List<BuildingInstance>();
            return inst.GetBuildingsInInterior();
        }

        // ================================================================
        //  FloorTile 移除
        // ================================================================

        /// <summary> 移除地板（检查 isDefault + 无内部建筑 + 无电梯 + 不悬空） </summary>
        public bool RemoveFloorTile(FloorTileInstance inst, bool refundMoney = false)
        {
            if (inst.IsDefault) return false;
            if (inst.HasBuildingsInInterior()) return false;
            if (HasElevatorOnFloorTile(inst)) return false;
            if (WouldBreakSupport(inst)) return false;

            ClearFloorTileCells(inst);
            floorTileInstances.Remove(inst.instanceId);

            if (refundMoney && inst.archetype != null)
            {
                int refundAmount = Mathf.RoundToInt(inst.archetype.buildCost * inst.archetype.destroyRefundRate);
                if (refundAmount > 0)
                    ResourceManager.Instance.RefundMoney(refundAmount);
            }

            Destroy(inst.gameObject);
            NotifyStructureChanged();
            return true;
        }

        // ================================================================
        //  支撑破坏检测
        // ================================================================

        /// <summary> 检查移除此地板是否会导致上方或横向依赖的地板悬空 </summary>
        public bool WouldBreakSupport(FloorTileInstance inst)
        {
            var fp = inst.archetype.GetRotatedFootprint(inst.rotation);
            var cellsToRemove = new HashSet<Vector2Int>();
            foreach (var off in fp)
                cellsToRemove.Add(inst.gridPosition + off);

            // 收集所有直接依赖该地板的相邻 FloorTile（上方 + 横向）
            var dependentIds = new HashSet<string>();
            foreach (var cell in cellsToRemove)
            {
                // 上方
                var above = new Vector2Int(cell.x, cell.y + 1);
                if (InBounds(above) && floorTileOwnerId[above.x, above.y] != null
                    && floorTileOwnerId[above.x, above.y] != inst.instanceId)
                    dependentIds.Add(floorTileOwnerId[above.x, above.y]);

                // 左侧
                var left = new Vector2Int(cell.x - 1, cell.y);
                if (InBounds(left) && floorTileOwnerId[left.x, left.y] != null
                    && floorTileOwnerId[left.x, left.y] != inst.instanceId)
                    dependentIds.Add(floorTileOwnerId[left.x, left.y]);

                // 右侧
                var right = new Vector2Int(cell.x + 1, cell.y);
                if (InBounds(right) && floorTileOwnerId[right.x, right.y] != null
                    && floorTileOwnerId[right.x, right.y] != inst.instanceId)
                    dependentIds.Add(floorTileOwnerId[right.x, right.y]);
            }

            // 对每个依赖的 FloorTile，模拟移除后检查它是否还有支撑
            foreach (var id in dependentIds)
            {
                if (!floorTileInstances.TryGetValue(id, out var depInst)) continue;
                if (!WouldStillHaveSupport(depInst, cellsToRemove))
                    return true; // 会悬空
            }
            return false;
        }

        /// <summary> 模拟移除 cellsToRemove 后，depInst 是否还有支撑 </summary>
        private bool WouldStillHaveSupport(FloorTileInstance depInst, HashSet<Vector2Int> cellsToRemove)
        {
            var depFp = depInst.archetype.GetRotatedFootprint(depInst.rotation);

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var off in depFp)
            {
                var p = depInst.gridPosition + off;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            int height = maxY - minY + 1;

            // 垂直支撑: 底行至少一格正下方有地板（排除 cellsToRemove）
            foreach (var off in depFp)
            {
                var p = depInst.gridPosition + off;
                if (p.y == minY)
                {
                    var below = new Vector2Int(p.x, p.y - 1);
                    if (!cellsToRemove.Contains(below) && InBounds(below) && floorTileLayer[below.x, below.y])
                        return true;
                }
            }

            // 横向支撑: 左侧或右侧紧贴的 FloorTile（排除 cellsToRemove）
            if (WouldStillHaveHorizontalSupport(minX - 1, minY, height, cellsToRemove))
                return true;
            if (WouldStillHaveHorizontalSupport(maxX + 1, minY, height, cellsToRemove))
                return true;

            return false;
        }

        private bool WouldStillHaveHorizontalSupport(int x, int minY, int height, HashSet<Vector2Int> cellsToRemove)
        {
            string ownerId = null;
            for (int y = minY; y < minY + height; y++)
            {
                var pos = new Vector2Int(x, y);
                if (cellsToRemove.Contains(pos)) return false;
                if (!InBounds(pos) || !floorTileLayer[pos.x, pos.y]) return false;

                var id = floorTileOwnerId[pos.x, pos.y];
                if (id == null) return false;
                if (ownerId == null) ownerId = id;
                else if (ownerId != id) return false;
            }

            if (ownerId == null || !floorTileInstances.TryGetValue(ownerId, out var neighbor))
                return false;

            var nFp = neighbor.archetype.GetRotatedFootprint(neighbor.rotation);
            int neighborHeightAtX = 0;
            foreach (var off in nFp)
            {
                var p = neighbor.gridPosition + off;
                if (p.x == x) neighborHeightAtX++;
            }
            return neighborHeightAtX == height;
        }

        // ================================================================
        //  FloorTile 反注册 / 重注册（用于移动）
        // ================================================================

        /// <summary> 临时反注册地板（不销毁 GO，用于移动）。有电梯时拒绝。 </summary>
        public bool UnregisterFloorTile(FloorTileInstance inst)
        {
            if (HasElevatorOnFloorTile(inst)) return false;
            ClearFloorTileCells(inst);
            floorTileInstances.Remove(inst.instanceId);
            return true;
        }

        /// <summary> 重新注册地板到新位置 </summary>
        public bool ReregisterFloorTile(FloorTileInstance inst, Vector2Int newPos, int newRot, bool skipSupportCheck = false)
        {
            var fp = inst.archetype.GetRotatedFootprint(newRot);

            foreach (var off in fp)
            {
                var p = newPos + off;
                if (!InBounds(p) || floorTileLayer[p.x, p.y]) return false;
            }

            if (!skipSupportCheck && !HasSupport(fp, newPos))
                return false;

            // 标记全占地
            foreach (var off in fp)
            {
                var p = newPos + off;
                floorTileLayer[p.x, p.y] = true;
                floorTileOwnerId[p.x, p.y] = inst.instanceId;
            }

            floorTileInstances[inst.instanceId] = inst;
            inst.gridPosition = newPos;
            inst.rotation = newRot;
            inst.transform.SetParent(buildingContainer);
            inst.transform.position = GridToWorld(newPos);
            inst.UpdateSortingOrder();

            // 重新初始化 InteriorGrid（位置/旋转变了，interior origin 需要更新）
            inst.InitializeInterior();

            NotifyStructureChanged();
            return true;
        }

        /// <summary> 带建筑移动地板的验证 — 简化版：有内部建筑或电梯时拒绝 </summary>
        public bool CanMoveFloorTileWithBuildings(FloorTileInstance floorInst, Vector2Int newPos, int newRot)
        {
            // 有内部建筑时禁止移动
            if (floorInst.HasBuildingsInInterior())
                return false;

            // 有电梯时禁止移动
            if (HasElevatorOnFloorTile(floorInst))
                return false;

            var fp = floorInst.archetype.GetRotatedFootprint(newRot);

            // 占地重叠检查
            foreach (var off in fp)
            {
                var p = newPos + off;
                if (!InBounds(p) || floorTileLayer[p.x, p.y]) return false;
            }

            if (!HasSupport(fp, newPos))
                return false;

            return true;
        }

        // ================================================================
        //  重建层数据
        // ================================================================

        /// <summary> 从 floorTileInstances 重建 floorTileLayer + floorTileOwnerId </summary>
        public void RebuildFloorLayers()
        {
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                {
                    floorTileLayer[x, y] = false;
                    floorTileOwnerId[x, y] = null;
                }

            foreach (var kv in floorTileInstances)
            {
                var inst = kv.Value;
                var fp = inst.archetype.GetRotatedFootprint(inst.rotation);
                foreach (var off in fp)
                {
                    var p = inst.gridPosition + off;
                    if (InBounds(p))
                    {
                        floorTileLayer[p.x, p.y] = true;
                        floorTileOwnerId[p.x, p.y] = inst.instanceId;
                    }
                }
            }
        }

        // ================================================================
        //  FloorTile 查询
        // ================================================================

        public bool HasFloorTileAt(Vector2Int pos)
            => InBounds(pos) && floorTileLayer[pos.x, pos.y];

        public FloorTileInstance GetFloorTileAt(Vector2Int pos)
        {
            if (!InBounds(pos)) return null;
            var id = floorTileOwnerId[pos.x, pos.y];
            if (id == null) return null;
            floorTileInstances.TryGetValue(id, out var inst);
            return inst;
        }

        public string GetFloorTileOwnerId(Vector2Int pos)
        {
            if (!InBounds(pos)) return null;
            return floorTileOwnerId[pos.x, pos.y];
        }

        /// <summary> 检查两个 FloorTile 是否为相邻楼层 </summary>
        public bool AreAdjacentFloors(FloorTileInstance a, FloorTileInstance b)
        {
            if (a == null || b == null || a.instanceId == b.instanceId) return false;

            var fpA = a.archetype.GetRotatedFootprint(a.rotation);
            foreach (var off in fpA)
            {
                var cellA = a.gridPosition + off;
                var above = new Vector2Int(cellA.x, cellA.y + 1);
                if (InBounds(above) && floorTileOwnerId[above.x, above.y] == b.instanceId)
                    return true;
                var below = new Vector2Int(cellA.x, cellA.y - 1);
                if (InBounds(below) && floorTileOwnerId[below.x, below.y] == b.instanceId)
                    return true;
            }
            return false;
        }

        public IEnumerable<FloorTileInstance> AllFloorTiles()
        {
            foreach (var kv in floorTileInstances) yield return kv.Value;
        }

        private void ClearFloorTileCells(FloorTileInstance inst)
        {
            var fp = inst.archetype.GetRotatedFootprint(inst.rotation);
            foreach (var off in fp)
            {
                var p = inst.gridPosition + off;
                if (InBounds(p))
                {
                    floorTileLayer[p.x, p.y] = false;
                    floorTileOwnerId[p.x, p.y] = null;
                }
            }
        }

        // ================================================================
        //  电梯
        // ================================================================

        /// <summary> 在两个 FloorTile 之间放置电梯（使用 tile-local 坐标） </summary>
        public bool PlaceElevator(FloorTileInstance startTile, Vector2Int startLocal,
                                   FloorTileInstance endTile, Vector2Int endLocal)
        {
            if (startTile == null || endTile == null) return false;
            if (startTile.Interior == null || endTile.Interior == null) return false;
            if (startTile.instanceId == endTile.instanceId) return false;

            // Interior 验证：范围内 + 未被建筑占用
            if (!startTile.Interior.InBounds(startLocal) || startTile.Interior.IsOccupied(startLocal)) return false;
            if (!endTile.Interior.InBounds(endLocal) || endTile.Interior.IsOccupied(endLocal)) return false;

            // 结构验证: 必须是相邻 FloorTile
            if (!AreAdjacentFloors(startTile, endTile)) return false;

            // 创建 NodeLink2
            var startWorld = startTile.Interior.LocalToWorld(startLocal)
                             + new Vector3(startTile.Interior.CellSize * 0.5f, startTile.Interior.CellSize * 0.5f, 0);
            var endWorld = endTile.Interior.LocalToWorld(endLocal)
                           + new Vector3(endTile.Interior.CellSize * 0.5f, endTile.Interior.CellSize * 0.5f, 0);

            var linkGo = new GameObject($"Elevator_{startTile.instanceId}_{endTile.instanceId}");
            linkGo.transform.SetParent(buildingContainer);
            linkGo.transform.position = startWorld;

            var link = linkGo.AddComponent<Pathfinding.NodeLink2>();
            linkGo.AddComponent<AILerpLinkTeleporter>();
            var endGo = new GameObject("End");
            endGo.transform.SetParent(linkGo.transform);
            endGo.transform.position = endWorld;
            link.end = endGo.transform;

            elevators.Add(new ElevatorData
            {
                startTileId = startTile.instanceId,
                startLocalCell = startLocal,
                endTileId = endTile.instanceId,
                endLocalCell = endLocal,
                go = linkGo
            });
            NotifyStructureChanged();
            return true;
        }

        /// <summary> 移除与指定 tile+cell 关联的电梯 </summary>
        public bool RemoveElevator(string tileId, Vector2Int localCell)
        {
            for (int i = elevators.Count - 1; i >= 0; i--)
            {
                var e = elevators[i];
                if ((e.startTileId == tileId && e.startLocalCell == localCell) ||
                    (e.endTileId == tileId && e.endLocalCell == localCell))
                {
                    Destroy(e.go);
                    elevators.RemoveAt(i);
                    NotifyStructureChanged();
                    return true;
                }
            }
            return false;
        }

        /// <summary> 检查地板上是否有电梯端点 </summary>
        public bool HasElevatorOnFloorTile(FloorTileInstance tile)
        {
            string id = tile.instanceId;
            foreach (var e in elevators)
                if (e.startTileId == id || e.endTileId == id) return true;
            return false;
        }

        // ================================================================
        //  便利遍历（遍历所有 FloorTile 的 InteriorGrid）
        // ================================================================

        public IEnumerable<ShelfInstance> AllShelves()
        {
            foreach (var kv in floorTileInstances)
            {
                if (kv.Value.Interior == null) continue;
                foreach (var s in kv.Value.Interior.AllShelves()) yield return s;
            }
        }

        public IEnumerable<BuildingInstance> AllBuildings()
        {
            foreach (var kv in floorTileInstances)
            {
                if (kv.Value.Interior == null) continue;
                foreach (var b in kv.Value.Interior.AllBuildings()) yield return b;
            }
        }

        public IEnumerable<FacilityInstance> AllFacilities()
        {
            foreach (var kv in floorTileInstances)
            {
                if (kv.Value.Interior == null) continue;
                foreach (var f in kv.Value.Interior.AllFacilities()) yield return f;
            }
        }

        public bool HasFacilityOfType(FacilityType type)
        {
            foreach (var kv in floorTileInstances)
            {
                if (kv.Value.Interior == null) continue;
                if (kv.Value.Interior.HasFacilityOfType(type)) return true;
            }
            return false;
        }

        // ================================================================
        //  坐标工具
        // ================================================================

        public Vector3 GridToWorld(Vector2Int g)
            => OriginPos + new Vector3(g.x * cellSize, g.y * cellSize, 0);

        public Vector2Int WorldToGrid(Vector3 w)
        {
            var local = w - OriginPos;
            return new Vector2Int(Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.y / cellSize));
        }

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize.x && p.y < gridSize.y;

        // ================================================================
        //  编辑器
        // ================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!origin && buildingContainer) origin = buildingContainer;
        }
#endif

        void OnDrawGizmosSelected()
        {
            var o = OriginPos;
            float half = cellSize * 0.5f;

            // 基础网格线（始终显示）
            // DrawWireCube 以 center 为中心绘制，需要偏移半格使线框对齐格子边界
            Gizmos.color = gridColor;
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                {
                    var p = o + new Vector3(x * cellSize + half, y * cellSize + half, 0);
                    Gizmos.DrawWireCube(p, Vector3.one * cellSize * 0.98f);
                }

            // 建造模式: floorTileLayer 可视化
            if (!Application.isPlaying) return;
            if (floorTileLayer == null) return;

            var cm = FindFirstObjectByType<ConstructionManager>();
            if (cm == null || cm.mode == ConstructionManager.Mode.None) return;

            // 占地线框（白色半透明）
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                    if (floorTileLayer[x, y])
                    {
                        var p = o + new Vector3(x * cellSize + half, y * cellSize + half, 0);
                        Gizmos.DrawWireCube(p, Vector3.one * cellSize * 0.96f);
                    }
        }
    }
}
