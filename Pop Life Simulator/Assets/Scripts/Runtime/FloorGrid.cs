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

        private Cell[,] grid;
        private readonly Dictionary<string, BuildingInstance> instances = new();
        private readonly HashSet<Vector2Int> occupiedCells = new(); // 可删，若保留需同步

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
        }

        // —— 放置校验 ——
        public bool CanPlaceFootprint(List<Vector2Int> fp, Vector2Int origin)
        {
            foreach (var off in fp)
            {
                var p = origin + off;
                if (!InBounds(p)) return false;
                if (grid[p.x, p.y].occupied) return false;
            }
            return true;
        }

        public bool CanPlaceFootprintAllowSelf(List<Vector2Int> fp, Vector2Int origin, string selfId)
        {
            foreach (var off in fp)
            {
                var p = origin + off;
                if (!InBounds(p)) return false;
                var c = grid[p.x, p.y];
                if (c.occupied && c.buildingId != selfId) return false;
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
            foreach (var off in fp)
            {
                var p = origin + off;
                var c = grid[p.x, p.y];
                c.occupied = occupy;
                c.buildingId = occupy ? bi.instanceId : null;
                if (occupy) occupiedCells.Add(p); else occupiedCells.Remove(p);
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

        public Vector3 GridToWorld(Vector2Int g) => new(g.x * cellSize, g.y * cellSize, 0);
        public Vector2Int WorldToGrid(Vector3 w) => new(Mathf.FloorToInt(w.x / cellSize), Mathf.FloorToInt(w.y / cellSize));
        private bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize.x && p.y < gridSize.y;

        // 供AI
        public float GetEmbarrassment(Vector2Int p) => InBounds(p) ? grid[p.x, p.y].embarrassment : 0f;
        public IEnumerable<ShelfInstance> AllShelves()
        {
            foreach (var kv in instances) if (kv.Value is ShelfInstance s) yield return s;
        }
    }
}
