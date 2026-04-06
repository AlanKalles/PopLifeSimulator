using System.Collections.Generic;
using UnityEngine;
using PopLife.Runtime;
using PopLife.Data;

namespace PopLife.Editor
{
    /// <summary>
    /// Editor 侧 WorldGrid 快照。从场景已有 FloorTileInstance 构建，
    /// 用于放置校验。不修改真实 WorldGrid 实例。
    /// </summary>
    public class EditorGridSnapshot
    {
        private readonly bool[,] tileLayer;
        private readonly string[,] tileOwnerId;
        private readonly Vector2Int gridSize;
        private readonly float cellSize;
        private readonly Vector3 origin;

        // FloorTileInstance 查找表（用于横向支撑检查）
        private readonly Dictionary<string, FloorTileInstance> tileInstances = new();

        private EditorGridSnapshot(Vector2Int gridSize, float cellSize, Vector3 origin)
        {
            this.gridSize = gridSize;
            this.cellSize = cellSize;
            this.origin = origin;
            tileLayer = new bool[gridSize.x, gridSize.y];
            tileOwnerId = new string[gridSize.x, gridSize.y];
        }

        /// <summary> 从场景中的 WorldGrid + 已有 FloorTileInstance 构建快照 </summary>
        public static EditorGridSnapshot Build(WorldGrid wg)
        {
            Vector3 originPos = wg.origin != null ? wg.origin.position : wg.transform.position;
            var snap = new EditorGridSnapshot(wg.gridSize, wg.cellSize, originPos);
            var container = wg.buildingContainer;
            if (container == null) return snap;

            foreach (var fti in container.GetComponentsInChildren<FloorTileInstance>())
            {
                if (fti.archetype == null) continue;
                string id = !string.IsNullOrEmpty(fti.instanceId) ? fti.instanceId : fti.name;
                snap.tileInstances[id] = fti;

                var fp = fti.archetype.GetRotatedFootprint(fti.rotation);
                foreach (var off in fp)
                {
                    var p = fti.gridPosition + off;
                    if (snap.InBounds(p))
                    {
                        snap.tileLayer[p.x, p.y] = true;
                        snap.tileOwnerId[p.x, p.y] = id;
                    }
                }
            }
            return snap;
        }

        // --- 校验 ---

        /// <summary> 检查 FloorTile 能否放在此位置（范围+无重叠+有支撑） </summary>
        public bool CanPlaceFloorTile(List<Vector2Int> fp, Vector2Int fpOrigin, bool skipSupportCheck = false)
        {
            foreach (var off in fp)
            {
                var p = fpOrigin + off;
                if (!InBounds(p)) return false;
                if (tileLayer[p.x, p.y]) return false;
            }
            if (!skipSupportCheck && !HasSupport(fp, fpOrigin))
                return false;
            return true;
        }

        // --- 支撑逻辑（与 WorldGrid 同逻辑） ---

        private bool HasSupport(List<Vector2Int> fp, Vector2Int fpOrigin)
        {
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            foreach (var off in fp)
            {
                var p = fpOrigin + off;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            int height = maxY - minY + 1;

            // 垂直支撑: 底行至少一格正下方有地板
            foreach (var off in fp)
            {
                var p = fpOrigin + off;
                if (p.y == minY)
                {
                    var below = new Vector2Int(p.x, p.y - 1);
                    if (InBounds(below) && tileLayer[below.x, below.y])
                        return true;
                }
            }

            // 横向支撑: 左侧或右侧整列紧贴已有 FloorTile
            if (HasHorizontalSupport(minX - 1, minY, height)) return true;
            if (HasHorizontalSupport(maxX + 1, minY, height)) return true;
            return false;
        }

        private bool HasHorizontalSupport(int x, int minY, int height)
        {
            string ownerId = null;
            for (int y = minY; y < minY + height; y++)
            {
                var pos = new Vector2Int(x, y);
                if (!InBounds(pos) || !tileLayer[pos.x, pos.y]) return false;
                var id = tileOwnerId[pos.x, pos.y];
                if (id == null) return false;
                if (ownerId == null) ownerId = id;
                else if (ownerId != id) return false;
            }
            if (ownerId == null || !tileInstances.TryGetValue(ownerId, out var neighbor)) return false;
            var nFp = neighbor.archetype.GetRotatedFootprint(neighbor.rotation);
            int neighborHeightAtX = 0;
            foreach (var off in nFp)
            {
                var p = neighbor.gridPosition + off;
                if (p.x == x) neighborHeightAtX++;
            }
            return neighborHeightAtX == height;
        }

        // --- 坐标工具 ---

        public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < gridSize.x && p.y < gridSize.y;

        public Vector3 GridToWorld(Vector2Int g) => origin + new Vector3(g.x * cellSize, g.y * cellSize, 0);

        public Vector2Int WorldToGrid(Vector3 w)
        {
            var l = w - origin;
            return new Vector2Int(Mathf.FloorToInt(l.x / cellSize), Mathf.FloorToInt(l.y / cellSize));
        }

        public bool HasTileAt(Vector2Int p) => InBounds(p) && tileLayer[p.x, p.y];

        public Vector2Int GridSize => gridSize;
        public float CellSize => cellSize;
        public Vector3 Origin => origin;

        /// <summary> 查找鼠标位置下方的 FloorTileInstance（用于擦除模式） </summary>
        public FloorTileInstance GetFloorTileAt(Vector2Int gridPos)
        {
            if (!InBounds(gridPos)) return null;
            var id = tileOwnerId[gridPos.x, gridPos.y];
            if (id == null) return null;
            tileInstances.TryGetValue(id, out var inst);
            return inst;
        }

        /// <summary> 构建 FloorTileInstance 的临时 InteriorGrid（用于 interior 校验） </summary>
        public static InteriorGrid BuildEditorInterior(FloorTileInstance tile)
        {
            var fta = tile.archetype as FloorTileArchetype;
            if (fta == null) return null;

            var interiorOrigin = tile.transform.position + new Vector3(fta.InteriorOffset.x, fta.InteriorOffset.y, 0);
            var interior = new InteriorGrid(
                fta.InteriorGridSize, fta.InteriorCellSize, interiorOrigin,
                fta.WalkableRows, fta.LeftPortalCells, fta.RightPortalCells);

            // 填充已有子物体的 occupied 状态
            foreach (var bi in tile.GetComponentsInChildren<BuildingInstance>(true))
            {
                if (bi == tile || bi is FloorTileInstance) continue;
                if (bi.archetype == null) continue;
                var localPos = interior.WorldToLocal(bi.transform.position);
                var fp = bi.archetype.GetRotatedFootprint(bi.rotation);
                interior.RegisterBuilding(bi, fp, localPos);
            }

            return interior;
        }
    }
}
