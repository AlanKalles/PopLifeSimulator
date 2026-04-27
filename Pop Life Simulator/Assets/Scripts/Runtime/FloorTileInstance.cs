using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    /// <summary>
    /// 地板瓦片运行时实例
    /// 每个 FloorTileInstance 拥有一个 InteriorGrid，管理内部建筑（货架/设施）的放置
    /// </summary>
    public class FloorTileInstance : BuildingInstance
    {
        [Header("地板标记")]
        [SerializeField] private bool isDefaultFloor;

        /// <summary> 默认一楼标记（不可移动/拆除） </summary>
        public bool IsDefault
        {
            get => isDefaultFloor;
            set => isDefaultFloor = value;
        }

        /// <summary> 获取地板尺寸（从原型） </summary>
        public Vector2Int TileSize => archetype is FloorTileArchetype fta ? fta.TileSize : Vector2Int.one;

        /// <summary> 相对层级（default=0, 上方+1, 下方-1），不可达返回 null </summary>
        public int? FloorLevel => WorldGrid.Instance?.GetFloorLevel(this);

        /// <summary> 显示名（"1F", "2F", "B1"...），不可达返回 "?" </summary>
        public string FloorDisplayName => FloorLevel.HasValue
            ? WorldGrid.FloorLevelToDisplayName(FloorLevel.Value)
            : "?";

        /// <summary> 内部网格，管理货架/设施放置 </summary>
        public InteriorGrid Interior { get; private set; }

        /// <summary>
        /// Interior 建筑的父容器（避免与 FloorTile 自身的 sprite 子物体混淆）。
        /// 自动查找名为 "InteriorContainer" 的子 Transform，不存在则创建。
        /// </summary>
        public Transform InteriorContainer
        {
            get
            {
                var existing = transform.Find("InteriorContainer");
                if (existing != null) return existing;

                var go = new GameObject("InteriorContainer");
                go.transform.SetParent(transform, false);
                return go.transform;
            }
        }

        public override int GetMaintenanceFee() => 0;

        protected override void OnInitialized()
        {
            // 设置独立 Layer
            int layerId = LayerMask.NameToLayer("FloorTile");
            if (layerId != -1)
                gameObject.layer = layerId;
        }

        // ── InteriorGrid 初始化 ──────────────────────────────────

        /// <summary>
        /// 由 WorldGrid 在放置瓦片后调用，根据 FloorTileArchetype 配置创建 InteriorGrid
        /// </summary>
        public void InitializeInterior()
        {
            if (archetype is not FloorTileArchetype fta) return;

            // interior 世界空间原点 = tile 世界位置 + interiorOffset
            Vector3 tileWorldPos = transform.position;
            Vector3 interiorOrigin = tileWorldPos + new Vector3(fta.InteriorOffset.x, fta.InteriorOffset.y, 0);

            Interior = new InteriorGrid(
                fta.InteriorGridSize,
                fta.InteriorCellSize,
                interiorOrigin,
                fta.WalkableRows,
                fta.LeftPortalCells,
                fta.RightPortalCells
            );
        }

        // ── 建筑放置（事务式） ───────────────────────────────────

        /// <summary>
        /// 在 interior 内放置一个建筑（货架/设施）。
        /// 包含蓝图验证、资源检查、扣费、实例化，失败时回滚。
        /// </summary>
        /// <param name="arch">建筑原型</param>
        /// <param name="localPos">interior 局部网格坐标</param>
        /// <param name="rot">旋转（0/1/2/3 对应 0/90/180/270 度）</param>
        /// <returns>成功放置的建筑实例，失败返回 null</returns>
        public BuildingInstance PlaceBuildingTransactional(BuildingArchetype arch, Vector2Int localPos, int rot)
        {
            if (Interior == null) return null;

            // 通过 IInteriorPlaceable 接口验证（支持 archetype 级自定义规则）
            if (arch is Data.IInteriorPlaceable ip)
            {
                if (!ip.ValidateInteriorPlacement(Interior, localPos, rot)) return null;
            }
            else
            {
                // 回退：直接检查 InteriorGrid
                var fp = arch.GetRotatedFootprint(rot);
                if (!Interior.CanPlace(fp, localPos)) return null;
            }

            // 电梯绕过蓝图检查
            bool isElevator = arch is ElevatorArchetype;
            if (!isElevator && !BlueprintManager.Instance.HasBlueprint(arch.archetypeId)) return null;

            // 电梯按当前 FloorLevel 计算成本
            int rawCost = arch is ElevatorArchetype ea
                ? ea.GetPlacementCost(FloorLevel ?? 0)
                : arch.buildCost;
            int finalBuildCost = GlobalModifierManager.Instance != null
                ? Mathf.RoundToInt(rawCost * GlobalModifierManager.Instance.GetConstructionCostMultiplier())
                : rawCost;
            if (!ResourceManager.Instance.CanAfford(finalBuildCost, 0)) return null;

            ResourceManager.Instance.SpendMoney(finalBuildCost);
            if (!isElevator)
                BlueprintManager.Instance.ConsumeBlueprint(arch.archetypeId);

            BuildingInstance inst = null;
            try
            {
                // 实例化建筑到 interior 世界坐标
                var worldPos = Interior.LocalToWorld(localPos);
                var container = InteriorContainer;
                var go = Instantiate(arch.prefab, worldPos, Quaternion.Euler(0, 0, rot * 90), container);
                inst = go.GetComponent<BuildingInstance>();
                inst.rotation = rot;
                inst.Initialize(arch, localPos, instanceId); // hostTileId = this.instanceId

                Interior.RegisterBuilding(inst, arch.GetRotatedFootprint(rot), localPos);
                return inst;
            }
            catch
            {
                ResourceManager.Instance.RefundMoney(finalBuildCost);
                if (inst != null) Destroy(inst.gameObject);
                return null;
            }
        }

        // ── 建筑移动 ─────────────────────────────────────────────

        /// <summary>
        /// 在 interior 内移动建筑到新位置/旋转。
        /// InteriorGrid.MoveBuilding 负责验证和数据更新，本方法负责 Transform 同步。
        /// </summary>
        public bool MoveBuilding(BuildingInstance bi, Vector2Int newLocalPos, int newRot)
        {
            if (Interior == null) return false;

            if (!Interior.MoveBuilding(bi, newLocalPos, newRot))
                return false;

            // MoveBuilding 已更新 bi.gridPosition 和 bi.rotation
            bi.transform.SetPositionAndRotation(
                Interior.LocalToWorld(newLocalPos),
                Quaternion.Euler(0, 0, newRot * 90));
            bi.UpdateSortingOrder();

            return true;
        }

        /// <summary>
        /// 将 shelf/facility 移动到同一 tile 或跨 tile 的新位置。原子操作，失败自动回滚。
        /// this = 源 FloorTileInstance（bi.hostFloorTileInstanceId 指向的那个）
        /// </summary>
        public bool MoveBuildingTo(BuildingInstance bi, FloorTileInstance targetTile,
                                   Vector2Int targetLocalPos, int newRot)
        {
            if (bi == null || targetTile == null || targetTile.Interior == null) return false;
            if (bi is FloorTileInstance) return false; // FloorTile 走 WorldGrid.ReregisterFloorTile 路径

            // 同 tile 快速通道：复用既有逻辑
            if (targetTile == this) return MoveBuilding(bi, targetLocalPos, newRot);

            // 电梯门不允许跨楼层（其 ElevatorLink 绑定依赖 FloorTile 身份）
            if (bi is ElevatorDoorInstance) return false;

            if (Interior == null) return false;

            var oldPos = bi.gridPosition;
            var oldRot = bi.rotation;
            var newFp  = bi.archetype.GetRotatedFootprint(newRot);

            // 1) 从源 Interior 反注册（不变更 bi.gridPosition / rotation）
            Interior.UnregisterBuilding(bi);

            // 2) 验证目标位置
            if (!targetTile.Interior.CanPlace(newFp, targetLocalPos))
            {
                // 回滚到源
                Interior.RegisterBuilding(bi, bi.archetype.GetRotatedFootprint(oldRot), oldPos);
                return false;
            }

            // 3) 提交：更新数据 → 注册到目标 → reparent → 同步 Transform → 更新排序
            bi.gridPosition = targetLocalPos;
            bi.rotation = newRot;
            bi.hostFloorTileInstanceId = targetTile.instanceId;
            targetTile.Interior.RegisterBuilding(bi, newFp, targetLocalPos);

            bi.transform.SetParent(targetTile.InteriorContainer, worldPositionStays: false);
            bi.transform.SetPositionAndRotation(
                targetTile.Interior.LocalToWorld(targetLocalPos),
                Quaternion.Euler(0, 0, newRot * 90));
            bi.UpdateSortingOrder();
            return true;
        }

        // ── 建筑移除 ─────────────────────────────────────────────

        /// <summary>
        /// 从 interior 取消注册建筑，可选退还金钱。
        /// 注意：不负责 Destroy GameObject，调用方自行处理。
        /// </summary>
        public void RemoveBuilding(BuildingInstance bi, bool refundMoney = false)
        {
            if (Interior == null) return;

            Interior.UnregisterBuilding(bi);

            if (refundMoney && bi.archetype != null)
            {
                int refundAmount = Mathf.RoundToInt(bi.archetype.buildCost * bi.archetype.destroyRefundRate);
                if (refundAmount > 0)
                    ResourceManager.Instance.RefundMoney(refundAmount);
            }
        }

        // ── 查询方法 ─────────────────────────────────────────────

        /// <summary> interior 内是否存在任何建筑 </summary>
        public bool HasBuildingsInInterior()
        {
            if (Interior == null) return false;
            foreach (var _ in Interior.AllBuildings()) return true;
            return false;
        }

        /// <summary> 获取 interior 内所有建筑实例列表 </summary>
        public List<BuildingInstance> GetBuildingsInInterior()
        {
            var result = new List<BuildingInstance>();
            if (Interior == null) return result;
            foreach (var b in Interior.AllBuildings())
                result.Add(b);
            return result;
        }

        // ── 存档重建 ─────────────────────────────────────────────

        /// <summary>
        /// 场景/存档重建时：扫描 InteriorContainer 下的建筑，注册到 InteriorGrid。
        /// </summary>
        public void RegisterExistingBuildingsInInterior()
        {
            if (Interior == null) return;

            foreach (var bi in GetComponentsInChildren<BuildingInstance>(true))
            {
                if (bi == this) continue;
                if (bi is FloorTileInstance) continue;
                if (bi.archetype == null) continue;

                var localPos = Interior.WorldToLocal(bi.transform.position);
                bi.gridPosition = localPos;
                bi.hostFloorTileInstanceId = instanceId;

                if (string.IsNullOrEmpty(bi.instanceId))
                    bi.instanceId = System.Guid.NewGuid().ToString();

                var fp = bi.archetype.GetRotatedFootprint(bi.rotation);
                Interior.RegisterBuilding(bi, fp, localPos);
            }
        }
    }
}
