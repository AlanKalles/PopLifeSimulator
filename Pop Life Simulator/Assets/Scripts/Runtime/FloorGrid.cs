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

        public void Init()
        {
            grid = new Cell[gridSize.x, gridSize.y];
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                    grid[x, y] = new Cell();

            // 清空列占用记录
            columnsWithGroundBuildings.Clear();
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
