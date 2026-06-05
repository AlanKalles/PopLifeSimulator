using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    /// <summary>
    /// 全局世界网格（单例）—— 管理 FloorTile 的放置、支撑/堆叠规则。
    /// 货架/设施的放置由各 FloorTileInstance.InteriorGrid 管理。
    /// 电梯连接由 ElevatorLinkManager 自动管理。
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

        // 楼层层级缓存 (instanceId → 相对层级, default=0)
        private readonly Dictionary<string, int> floorLevels = new();
        private bool floorLevelsDirty = true;

        // 电梯
        [Header("Elevator")]
        [SerializeField] private Data.ElevatorArchetype defaultElevatorArchetype;
        [SerializeField] private ElevatorLinkManager elevatorLinkManager;
        public Data.ElevatorArchetype DefaultElevatorArchetype => defaultElevatorArchetype;
        public ElevatorLinkManager ElevatorLinks => elevatorLinkManager;

        // ================================================================
        //  事件
        // ================================================================

        /// <summary> 地板结构变化时触发（放置/移除/移动 FloorTile） </summary>
        public event Action OnStructureChanged;

        /// <summary> 公共触发点，供外部类调用 </summary>
        public void NotifyStructureChanged()
        {
            InvalidateFloorLevels();
            OnStructureChanged?.Invoke();
        }

        private void InvalidateFloorLevels() => floorLevelsDirty = true;

        /// <summary>
        /// BFS 重算所有 FloorTile 的楼层层级。
        /// default tile = 0, 上方 +1, 下方 -1, 水平相邻同层。
        /// 不可达的 tile 不会被分配层级（GetFloorLevel 返回 null）。
        /// </summary>
        private void RecalculateFloorLevels()
        {
            floorLevels.Clear();
            floorLevelsDirty = false;

            // 找 default tile
            FloorTileInstance defaultTile = null;
            foreach (var kv in floorTileInstances)
            {
                if (kv.Value.IsDefault) { defaultTile = kv.Value; break; }
            }
            if (defaultTile == null)
            {
                if (floorTileInstances.Count > 0)
                    Debug.LogWarning("WorldGrid: 没有 IsDefault=true 的 FloorTile，无法计算楼层层级");
                return;
            }

            // BFS
            var queue = new Queue<string>();
            floorLevels[defaultTile.instanceId] = 0;
            queue.Enqueue(defaultTile.instanceId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var currentTile = floorTileInstances[currentId];
                int currentLevel = floorLevels[currentId];

                foreach (var kv in floorTileInstances)
                {
                    var otherId = kv.Key;
                    var otherTile = kv.Value;

                    int proposedLevel;

                    if (IsSameFloorNeighbor(currentTile, otherTile))
                        proposedLevel = currentLevel;
                    else if (IsUpperFloorNeighbor(currentTile, otherTile))
                        proposedLevel = currentLevel + 1;
                    else if (IsUpperFloorNeighbor(otherTile, currentTile))
                        proposedLevel = currentLevel - 1;
                    else
                        continue;

                    if (floorLevels.TryGetValue(otherId, out int existingLevel))
                    {
                        // 冲突检测：已分配但值不一致
                        if (existingLevel != proposedLevel)
                            Debug.LogWarning($"WorldGrid: FloorTile '{otherTile.name}' 层级冲突: " +
                                             $"已有={existingLevel}, 从 '{currentTile.name}' 推导={proposedLevel}。检查拓扑或 default 配置");
                        continue;
                    }

                    floorLevels[otherId] = proposedLevel;
                    queue.Enqueue(otherId);
                }
            }
        }

        // ================================================================
        //  楼层层级查询 API
        // ================================================================

        /// <summary>
        /// 获取 FloorTile 的相对层级（default=0, 上方+1, 下方-1...）。
        /// 不可达或未注册返回 null。
        /// </summary>
        public int? GetFloorLevel(FloorTileInstance tile)
        {
            if (tile == null) return null;
            return GetFloorLevel(tile.instanceId);
        }

        /// <summary> 通过 instanceId 获取楼层层级 </summary>
        public int? GetFloorLevel(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            if (floorLevelsDirty) RecalculateFloorLevels();
            return floorLevels.TryGetValue(instanceId, out int level) ? level : null;
        }

        /// <summary> 获取指定层级的所有 FloorTile </summary>
        public IEnumerable<FloorTileInstance> GetFloorTilesOnLevel(int level)
        {
            if (floorLevelsDirty) RecalculateFloorLevels();
            foreach (var kv in floorLevels)
            {
                if (kv.Value == level && floorTileInstances.TryGetValue(kv.Key, out var tile))
                    yield return tile;
            }
        }

        /// <summary> 总楼层数（最高层级 - 最低层级 + 1，无 tile 返回 0） </summary>
        public int TotalFloorCount
        {
            get
            {
                if (floorLevelsDirty) RecalculateFloorLevels();
                if (floorLevels.Count == 0) return 0;
                int min = int.MaxValue, max = int.MinValue;
                foreach (var kv in floorLevels)
                {
                    if (kv.Value < min) min = kv.Value;
                    if (kv.Value > max) max = kv.Value;
                }
                return max - min + 1;
            }
        }

        /// <summary> 层级→显示名 (0→"1F", 1→"2F", -1→"B1", -2→"B2") </summary>
        public static string FloorLevelToDisplayName(int level)
        {
            if (level >= 0) return $"{level + 1}F";
            return $"B{-level}";
        }

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

        /// <summary> 通过 instanceId 查找 FloorTileInstance </summary>
        public FloorTileInstance GetFloorTileById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            floorTileInstances.TryGetValue(id, out var tile);
            return tile;
        }

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

            // 通知结构变化，触发 NavigationService.RebuildAllGraphs() 创建 interior GridGraph
            // 必须在 RegisterAllChildBuildings 之后，此时 Interior 已初始化
            NotifyStructureChanged();
        }

        // ================================================================
        //  初始化与重建
        // ================================================================

        public void Init()
        {
            floorTileLayer = new bool[gridSize.x, gridSize.y];
            floorTileOwnerId = new string[gridSize.x, gridSize.y];
            InvalidateFloorLevels();
        }

        public void RebuildFromScene()
        {
            Init();
            floorTileInstances.Clear();
            RegisterAllChildBuildings();
            InvalidateFloorLevels();
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

            ResourceManager.Instance.SpendOnConstruction(finalBuildCost);
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
            inst.SetStoreId(arch.StoreId, rebuildNav: false);

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
            if (inst == null) return false;
            if (ConstructionGuards.IsTileReadOnlyForPlayer(inst)) return false;
            if (inst.IsDefault) return false;
            // 编辑器锁定（玩家不可拆除）：与 IsDefault 同等的兜底，
            // 主流程已在 ConstructionManager 检查 EditorLockedDestroy
            if (inst.EditorLockedDestroy) return false;
            if (inst.HasBuildingsInInterior()) return false;
            if (elevatorLinkManager != null && elevatorLinkManager.HasElevatorOnTile(inst)) return false;
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
            if (inst == null) return false;
            if (ConstructionGuards.IsTileReadOnlyForPlayer(inst)) return false;
            if (elevatorLinkManager != null && elevatorLinkManager.HasElevatorOnTile(inst)) return false;
            ClearFloorTileCells(inst);
            floorTileInstances.Remove(inst.instanceId);
            InvalidateFloorLevels();
            return true;
        }

        /// <summary> 重新注册地板到新位置 </summary>
        public bool ReregisterFloorTile(FloorTileInstance inst, Vector2Int newPos, int newRot, bool skipSupportCheck = false)
        {
            if (inst == null) return false;
            if (ConstructionGuards.IsTileReadOnlyForPlayer(inst)) return false;
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
            if (elevatorLinkManager != null && elevatorLinkManager.HasElevatorOnTile(floorInst))
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
            InvalidateFloorLevels();
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

        // ================================================================
        //  统一邻接 Helper
        // ================================================================

        /// <summary> 获取 FloorTile 在世界网格中的 Y 范围 </summary>
        private (int minY, int maxY) GetTileYRange(FloorTileInstance tile)
        {
            var fp = tile.archetype.GetRotatedFootprint(tile.rotation);
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var off in fp)
            {
                var p = tile.gridPosition + off;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            return (minY, maxY);
        }

        /// <summary> 获取 FloorTile 在世界网格中的 X 范围 </summary>
        private (int minX, int maxX) GetTileXRange(FloorTileInstance tile)
        {
            var fp = tile.archetype.GetRotatedFootprint(tile.rotation);
            int minX = int.MaxValue, maxX = int.MinValue;
            foreach (var off in fp)
            {
                var p = tile.gridPosition + off;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
            }
            return (minX, maxX);
        }

        /// <summary>
        /// 两个 FloorTile 是否为同楼层水平邻居。
        /// 条件: Y 范围完全一致 + 列紧贴 + 接触面高度一致。
        /// </summary>
        public bool IsSameFloorNeighbor(FloorTileInstance a, FloorTileInstance b)
        {
            if (a == null || b == null || a.instanceId == b.instanceId) return false;

            var (aMinY, aMaxY) = GetTileYRange(a);
            var (bMinY, bMaxY) = GetTileYRange(b);
            if (aMinY != bMinY || aMaxY != bMaxY) return false;

            var (aMinX, aMaxX) = GetTileXRange(a);
            var (bMinX, bMaxX) = GetTileXRange(b);

            int height = aMaxY - aMinY + 1;

            // a 在 b 左侧
            if (aMaxX + 1 == bMinX)
                return CheckContactHeight(b, bMinX, height);
            // a 在 b 右侧
            if (bMaxX + 1 == aMinX)
                return CheckContactHeight(a, aMinX, height);

            return false;
        }

        /// <summary>
        /// 检查 tile 在指定列 x 上的格子数是否等于 expectedHeight。
        /// 复用 HasHorizontalSupport 的接触面高度一致逻辑。
        /// </summary>
        private bool CheckContactHeight(FloorTileInstance tile, int x, int expectedHeight)
        {
            var fp = tile.archetype.GetRotatedFootprint(tile.rotation);
            int count = 0;
            foreach (var off in fp)
            {
                var p = tile.gridPosition + off;
                if (p.x == x) count++;
            }
            return count == expectedHeight;
        }

        /// <summary>
        /// lower 是否是 upper 的正下方楼层。
        /// 条件: upper 的底行 minY == lower 的顶行 maxY + 1，且至少一个 upper 底行格子正下方属于 lower。
        /// </summary>
        public bool IsUpperFloorNeighbor(FloorTileInstance lower, FloorTileInstance upper)
        {
            if (lower == null || upper == null || lower.instanceId == upper.instanceId) return false;

            var (_, lowerMaxY) = GetTileYRange(lower);
            var (upperMinY, _) = GetTileYRange(upper);
            if (upperMinY != lowerMaxY + 1) return false;

            // 检查至少一个 upper 底行格子正下方属于 lower
            var fpUpper = upper.archetype.GetRotatedFootprint(upper.rotation);
            foreach (var off in fpUpper)
            {
                var p = upper.gridPosition + off;
                if (p.y != upperMinY) continue;
                var below = new Vector2Int(p.x, p.y - 1);
                if (InBounds(below) && floorTileOwnerId[below.x, below.y] == lower.instanceId)
                    return true;
            }
            return false;
        }

        /// <summary> 检查两个 FloorTile 是否为相邻楼层（上下关系，任一方向） </summary>
        public bool AreAdjacentFloors(FloorTileInstance a, FloorTileInstance b)
            => IsUpperFloorNeighbor(a, b) || IsUpperFloorNeighbor(b, a);

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

        // 旧电梯系统已移至 ElevatorLinkManager（自动连接模式）

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

        public static FloorTileInstance ResolveHostTile(BuildingInstance building)
        {
            if (building == null) return null;
            if (building is FloorTileInstance floorTile) return floorTile;
            if (string.IsNullOrEmpty(building.hostFloorTileInstanceId)) return null;
            return Instance != null ? Instance.GetFloorTileById(building.hostFloorTileInstanceId) : null;
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

            // 楼层标签（Rebuild from Scene 后可用，编辑模式+运行时均显示）
#if UNITY_EDITOR
            if (floorTileInstances.Count > 0)
            {
                var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = Color.cyan },
                    alignment = TextAnchor.MiddleCenter
                };
                foreach (var kv in floorTileInstances)
                {
                    var tile = kv.Value;
                    if (tile == null) continue;
                    var level = GetFloorLevel(tile);
                    var label = level.HasValue ? FloorLevelToDisplayName(level.Value) : "?";
                    UnityEditor.Handles.Label(tile.transform.position + Vector3.up * 0.5f, label, style);
                }
            }
#endif

            // 以下仅运行时
            if (!Application.isPlaying) return;
            if (floorTileLayer == null) return;

            // 建造模式: floorTileLayer 可视化
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
