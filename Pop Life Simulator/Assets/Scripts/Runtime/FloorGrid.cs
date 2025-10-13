using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    public class FloorGrid : MonoBehaviour
    {
        [Header("楼层配置")]
        public int floorId;
        public Vector2Int gridSize = new(20, 10);
        public float cellSize = 1f;
        public Transform buildingContainer;
        public Transform origin;
        Vector3 OriginPos => origin ? origin.position : transform.position;

        [Header("Level Design Only")]
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        public Color gridSelectedColor = new Color(0.2f, 0.8f, 1f, 0.5f);

        [HideInInspector]
        public bool isSelected = false; // 由 ConstructionManager 设置
        
        private Cell[,] grid;
        private readonly Dictionary<string, BuildingInstance> instances = new();
        private readonly HashSet<Vector2Int> occupiedCells = new(); // 可删，若保留需同步
        private readonly HashSet<int> columnsWithGroundBuildings = new(); // 记录第一层被占用的列

        private class Cell
        {
            public bool occupied;
            public string buildingId;
            public float embarrassment; // 供AI
        }

        void Awake() => Init();

        void Start()
        {
            // 初始化楼层检测碰撞体
            InitializeFloorDetection();

            // 若在运行态时已由外部（如 FloorManager）完成重建，则避免重复注册
            if (Application.isPlaying && instances.Count > 0)
                return;
            // 自动注册场景中已存在的建筑（用于编辑器预建造）
            RegisterAllChildBuildings();
        }

        // 初始化楼层检测碰撞体（用于鼠标自动检测系统）
        private void InitializeFloorDetection()
        {
            // 检查是否已有BoxCollider2D
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            bool isNewCollider = (collider == null);

            if (isNewCollider)
            {
                collider = gameObject.AddComponent<BoxCollider2D>();
            }

            // 配置Collider为触发器
            collider.isTrigger = true;

            // 只在新建Collider时设置大小和偏移
            if (isNewCollider)
            {
                // 计算Collider大小（覆盖整个网格）
                Vector2 size = new Vector2(
                    gridSize.x * cellSize,
                    gridSize.y * cellSize
                );
                collider.size = size;

                // 计算中心点（网格中心）
                Vector2 center = new Vector2(
                    size.x / 2f,
                    size.y / 2f
                );
                collider.offset = center;
            }

            // 设置专用Layer（如果Layer存在）
            int layerId = LayerMask.NameToLayer("FloorDetection");
            if (layerId != -1)
            {
                gameObject.layer = layerId;
            }
            else
            {
                Debug.LogWarning($"FloorGrid '{name}': Layer 'FloorDetection' not found. Please create it in Project Settings.");
            }
        }

        // 运行时/楼层激活时：从场景子物体重建占用与实例映射
        public void RebuildFromScene()
        {
            // 清理格子、列占用、缓存与实例映射，然后按场景子物体重建
            Init();
            occupiedCells.Clear();
            instances.Clear();
            RegisterAllChildBuildings();
        }

        // 判断当前是否已经有任何建筑注册到该楼层（用于避免重复重建）
        public bool HasAnyRegisteredBuildings()
        {
            return instances.Count > 0;
        }

        public void Init()
        {
            grid = new Cell[gridSize.x, gridSize.y];
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                    grid[x, y] = new Cell();

            // 清空列占用记录
            columnsWithGroundBuildings.Clear();
        }

        // 注册所有子建筑到网格（编辑器预建造 + 场景加载恢复）
        private void RegisterAllChildBuildings()
        {
            if (buildingContainer == null) return;

            BuildingInstance[] allBuildings = buildingContainer.GetComponentsInChildren<BuildingInstance>();

            foreach (var building in allBuildings)
            {
                // 跳过已注册的建筑
                if (!string.IsNullOrEmpty(building.instanceId) && instances.ContainsKey(building.instanceId)) continue;

                // 确保建筑数据完整
                if (building.archetype == null)
                {
                    Debug.LogWarning($"FloorGrid: Building {building.name} missing archetype, skipping registration");
                    continue;
                }

                // 优先使用保存的数据，如果没有则从Transform推断
                Vector2Int gridPos;
                int rotation;

                // 使用 instanceId 判断是否有有效的序列化数据（避免 (0,0) 位置被误判）
                if (!string.IsNullOrEmpty(building.instanceId) && building.floorId == this.floorId)
                {
                    // 使用序列化保存的数据（更可靠）
                    gridPos = building.gridPosition;
                    rotation = building.rotation;
                }
                else
                {
                    // 从Transform推断（用于旧版本兼容或手动放置的建筑）
                    gridPos = WorldToGrid(building.transform.position);
                    rotation = Mathf.RoundToInt(building.transform.eulerAngles.z / 90f) % 4;
                }

                var footprint = building.archetype.GetRotatedFootprint(rotation);

                // 验证位置可用
                if (CanPlaceFootprint(footprint, gridPos))
                {
                    // 更新建筑实例数据
                    building.rotation = rotation;
                    building.gridPosition = gridPos;
                    building.floorId = this.floorId;

                    // 生成 instanceId（如果没有）
                    if (string.IsNullOrEmpty(building.instanceId))
                    {
                        building.instanceId = System.Guid.NewGuid().ToString();
                    }

                    // 注册到网格
                    if (RegisterExistingBuilding(building, gridPos, rotation))
                    {
                        Debug.Log($"FloorGrid: Auto-registered building '{building.archetype.displayName}' at {gridPos}");
                    }
                    else
                    {
                        Debug.LogWarning($"FloorGrid: Failed to register building '{building.archetype.displayName}' at {gridPos}");
                    }
                }
                else
                {
                    Debug.LogWarning($"FloorGrid: Building '{building.name}' at {gridPos} conflicts with existing buildings");
                }
            }
        }

        // —— 放置校验 ——
        public bool CanPlaceFootprint(List<Vector2Int> fp, Vector2Int origin)
        {
            // 检查是否至少有一格在第一层
            bool hasGroundLevel = false;
            foreach (var off in fp)
            {
                var p = origin + off;
                if (p.y == 0)
                {
                    hasGroundLevel = true;
                    break;
                }
            }

            // 新规则1: 建筑必须至少有一格在第一层
            if (!hasGroundLevel) return false;

            foreach (var off in fp)
            {
                var p = origin + off;
                if (!InBounds(p)) return false;
                if (grid[p.x, p.y].occupied) return false;

                // 新规则2: 如果该列第一层已被占用，则不能在该列的任何位置建造新建筑
                if (columnsWithGroundBuildings.Contains(p.x))
                {
                    // 该列已有建筑物在第一层，禁止在该列建造
                    return false;
                }
            }
            return true;
        }

        public bool CanPlaceFootprintAllowSelf(List<Vector2Int> fp, Vector2Int origin, string selfId)
        {
            // 检查是否至少有一格在第一层
            bool hasGroundLevel = false;
            foreach (var off in fp)
            {
                var p = origin + off;
                if (p.y == 0)
                {
                    hasGroundLevel = true;
                    break;
                }
            }

            // 新规则1: 建筑必须至少有一格在第一层
            if (!hasGroundLevel) return false;

            // 获取自身原来占用的列
            HashSet<int> selfOccupiedColumns = new HashSet<int>();
            if (instances.TryGetValue(selfId, out var selfInstance))
            {
                var selfFp = selfInstance.archetype.GetRotatedFootprint(selfInstance.rotation);
                foreach (var off in selfFp)
                {
                    var p = selfInstance.gridPosition + off;
                    if (p.y == 0)
                    {
                        selfOccupiedColumns.Add(p.x);
                    }
                }
            }

            foreach (var off in fp)
            {
                var p = origin + off;
                if (!InBounds(p)) return false;
                var c = grid[p.x, p.y];
                if (c.occupied && c.buildingId != selfId) return false;

                // 新规则2: 如果该列第一层已被其他建筑占用，则不能在该列建造
                if (columnsWithGroundBuildings.Contains(p.x) && !selfOccupiedColumns.Contains(p.x))
                {
                    // 该列已有其他建筑物在第一层，禁止在该列建造
                    return false;
                }
            }
            return true;
        }

        // —— 事务式放置 ——
        public BuildingInstance PlaceBuildingTransactional(BuildingArchetype arch, Vector2Int pos, int rot)
        {
            var fp = arch.GetRotatedFootprint(rot);
            if (!CanPlaceFootprint(fp, pos)) return null;

            if (arch.requiresBlueprint && !BlueprintManager.Instance.HasBlueprint(arch.archetypeId)) return null;
            if (!ResourceManager.Instance.CanAfford(arch.buildCost, 0)) return null;

            ResourceManager.Instance.SpendMoney(arch.buildCost);
            if (arch.requiresBlueprint) BlueprintManager.Instance.ConsumeBlueprint(arch.archetypeId);

            BuildingInstance inst = null;
            try
            {
                inst = InternalInstantiate(arch, pos, rot);
                MarkOccupied(inst, fp, pos, true);
                return inst;
            }
            catch
            {
                // 回滚资源
                ResourceManager.Instance.AddMoney(arch.buildCost);
                if (arch.requiresBlueprint) BlueprintManager.Instance.AddBlueprint(arch.archetypeId);
                if (inst) Destroy(inst.gameObject);
                return null;
            }
        }

        private BuildingInstance InternalInstantiate(BuildingArchetype arch, Vector2Int pos, int rot)
        {
            var world = GridToWorld(pos);
            var go = Instantiate(arch.prefab, world, Quaternion.Euler(0, 0, rot * 90), buildingContainer);
            var bi = go.GetComponent<BuildingInstance>();
            bi.rotation = rot;
            bi.Initialize(arch, pos, floorId);
            instances[bi.instanceId] = bi;
            return bi;
        }

        private void MarkOccupied(BuildingInstance bi, List<Vector2Int> fp, Vector2Int origin, bool occupy)
        {
            // 先收集所有需要更新的列
            HashSet<int> affectedColumns = new HashSet<int>();
            foreach (var off in fp)
            {
                var p = origin + off;
                if (p.y == 0)
                {
                    affectedColumns.Add(p.x);
                }
            }

            foreach (var off in fp)
            {
                var p = origin + off;
                var c = grid[p.x, p.y];
                c.occupied = occupy;
                c.buildingId = occupy ? bi.instanceId : null;
                if (occupy) occupiedCells.Add(p); else occupiedCells.Remove(p);
            }

            // 更新列占用记录
            foreach (var x in affectedColumns)
            {
                if (occupy)
                {
                    columnsWithGroundBuildings.Add(x);
                }
                else
                {
                    // 移除建筑后，检查该列第一层是否还有其他建筑
                    if (!grid[x, 0].occupied)
                    {
                        columnsWithGroundBuildings.Remove(x);
                    }
                }
            }
        }

        public bool MoveBuilding(BuildingInstance bi, Vector2Int newPos, int newRot)
        {
            var oldFp = bi.archetype.GetRotatedFootprint(bi.rotation);
            var newFp = bi.archetype.GetRotatedFootprint(newRot);

            // 不清旧格先验证
            if (!CanPlaceFootprintAllowSelf(newFp, newPos, bi.instanceId)) return false;
            if (!ResourceManager.Instance.CanAfford(bi.archetype.moveCost, 0)) return false;

            ResourceManager.Instance.SpendMoney(bi.archetype.moveCost);

            // 清旧
            MarkOccupied(bi, oldFp, bi.gridPosition, false);

            // 写新
            MarkOccupied(bi, newFp, newPos, true);

            bi.gridPosition = newPos;
            bi.rotation = newRot;
            bi.transform.SetPositionAndRotation(GridToWorld(newPos), Quaternion.Euler(0, 0, newRot * 90));
            return true;
        }

        public void RemoveBuilding(BuildingInstance bi, bool refundBlueprint)
        {
            var fp = bi.archetype.GetRotatedFootprint(bi.rotation);
            MarkOccupied(bi, fp, bi.gridPosition, false);
            instances.Remove(bi.instanceId);

            if (refundBlueprint && bi.archetype.requiresBlueprint)
                BlueprintManager.Instance.AddBlueprint(bi.archetype.archetypeId);
        }

        // 注册一个已存在的建筑到楼层（用于跨楼层移动）
        public bool RegisterExistingBuilding(BuildingInstance bi, Vector2Int newPos, int newRot)
        {
            if (bi == null) return false;

            var fp = bi.archetype.GetRotatedFootprint(newRot);

            // 验证位置可用
            if (!CanPlaceFootprint(fp, newPos))
                return false;

            // 更新建筑实例信息
            bi.floorId = this.floorId;
            bi.gridPosition = newPos;
            bi.rotation = newRot;

            // 注册到实例字典
            instances[bi.instanceId] = bi;

            // 标记占用
            MarkOccupied(bi, fp, newPos, true);

            return true;
        }

        public bool HasFacilityOfType(FacilityType type)
        {
            foreach (var kv in instances)
            {
                if (kv.Value is FacilityInstance fi)
                {
                    var fa = (FacilityArchetype)fi.archetype;
                    if (fa != null && fa.facilityType == type) return true;
                }
            }
            return false;
        }

        public Vector3 GridToWorld(Vector2Int g)
            => OriginPos + new Vector3(g.x * cellSize, g.y * cellSize, 0);

        public Vector2Int WorldToGrid(Vector3 w) {
            var local = w - OriginPos;
            return new Vector2Int(Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.y / cellSize));
        }
        private bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize.x && p.y < gridSize.y;

        // 供AI
        public float GetEmbarrassment(Vector2Int p) => InBounds(p) ? grid[p.x, p.y].embarrassment : 0f;
        public IEnumerable<ShelfInstance> AllShelves()
        {
            foreach (var kv in instances) if (kv.Value is ShelfInstance s) yield return s;
        }

        public IEnumerable<BuildingInstance> AllBuildings()
        {
            foreach (var kv in instances) yield return kv.Value;
        }

        public IEnumerable<FacilityInstance> AllFacilities()
        {
            foreach (var kv in instances) if (kv.Value is FacilityInstance f) yield return f;
        }
#if UNITY_EDITOR
        private void OnValidate() {
            if (!origin && buildingContainer) origin = buildingContainer; // 你想用container当原点时，拖好一次即可
        }
#endif
        void OnDrawGizmos() {
            var o = OriginPos;
            // 根据选中状态使用不同颜色
            Gizmos.color = isSelected ? gridSelectedColor : gridColor;
            for (int x=0;x<gridSize.x;x++)
            for (int y=0;y<gridSize.y;y++) {
                var p = o + new Vector3(x*cellSize, y*cellSize, 0);
                Gizmos.DrawWireCube(p, Vector3.one * cellSize * 0.98f);
            }
        }
    }
}
