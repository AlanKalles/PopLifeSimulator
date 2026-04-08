using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
    [CreateAssetMenu(menuName = "PopLife/Buildings/ElevatorArchetype")]
    public class ElevatorArchetype : BuildingArchetype
    {
        [Header("Elevator Cost")]
        [SerializeField] private int baseCost = 500;
        [SerializeField] private int costPerLevel = 200;

        public int BaseCost => baseCost;
        public int CostPerLevel => costPerLevel;

        // levels 留空 → 基类 MaxLevel => levels?.Length ?? 0 = 0（不可升级）

        /// <summary> 计算放置费用: baseCost + |floorLevel| * costPerLevel </summary>
        public int GetPlacementCost(int floorLevel)
            => baseCost + Mathf.Abs(floorLevel) * costPerLevel;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            canRotate = false;
            // 强制 footprint 为 3×4（横3竖4），pivot 在左下角
            footprintPattern = new List<Vector2Int>();
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 4; y++)
                    footprintPattern.Add(new Vector2Int(x, y));
            // 不可升级
            levels = null;
        }
#endif

        /// <summary> 电梯专属放置校验：标准占位 + 底行必须 walkable </summary>
        public override bool ValidateInteriorPlacement(
            Runtime.InteriorGrid interior, Vector2Int localPos, int rotation)
        {
            if (!interior.CanPlace(GetRotatedFootprint(rotation), localPos)) return false;
            // 3×4 footprint，底行 = (0,0)(1,0)(2,0)
            // 底行所有格子必须在 walkable 行（顾客能走到电梯门前）
            for (int x = 0; x < 3; x++)
            {
                if (!interior.IsWalkable(localPos + new Vector2Int(x, 0))) return false;
            }
            return true;
        }
    }
}
