using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
    // 等级数据（基类）
    [Serializable]
    public class BuildingLevelData
    {
        public int level = 1;
        public int upgradeMoneyCost;
        public int upgradeFameCost;
        public int maintenanceFee;
    }

    public enum ProductCategory { Lingerie, Condom, Vibrator, Fleshlights, Lubricant }
    public enum FacilityType { Cashier, AirConditioner, ATM, SecurityCamera, MusicPlayer }
    public enum EffectType { ReduceEmbarrassment, IncreaseAttractiveness, IncreaseCustomerSpeed, RestoreMoney }

    // 原型基类
    public abstract class BuildingArchetype : ScriptableObject
    {
        [Header("基础信息")]
        public string archetypeId;
        public string displayName;
        public Sprite icon;
        public GameObject prefab;

        [Header("建造信息")]
        public int buildCost;
        public int moveCost = 10;
        public bool requiresBlueprint = true;

        [Header("占用空间")]
        [SerializeField] private List<Vector2Int> footprintPattern = new(); // 相对原点
        public bool canRotate = true;

        [Header("等级系统")]
        [SerializeField] protected BuildingLevelData[] levels;
        public int MaxLevel => levels?.Length ?? 0;

        // 统一取级别数据
        public virtual BuildingLevelData GetLevel(int lvl)
        {
            if (levels == null || levels.Length == 0) return null;
            int i = Mathf.Clamp(lvl - 1, 0, levels.Length - 1);
            return levels[i];
        }

        // 校验放置
        public virtual bool ValidatePlacement(PopLife.Runtime.FloorGrid floor, Vector2Int position, int rotation)
        {
            return floor.CanPlaceFootprint(GetRotatedFootprint(rotation), position);
        }

        // 返回旋转后占格（始终返回新列表，避免外部修改污染原型）
        public List<Vector2Int> GetRotatedFootprint(int rotation)
        {
            int r = ((rotation % 4) + 4) % 4;
            var src = footprintPattern;
            var res = new List<Vector2Int>(src.Count);
            if (!canRotate) r = 0;
            foreach (var v in src) res.Add(Rotate(v, r));
            return res;
        }

        private static Vector2Int Rotate(Vector2Int v, int r)
        {
            return r switch
            {
                1 => new Vector2Int(-v.y, v.x),   // 90
                2 => new Vector2Int(-v.x, -v.y),  // 180
                3 => new Vector2Int(v.y, -v.x),   // 270
                _ => v
            };
        }
    }

    // 货架原型
    [CreateAssetMenu(menuName = "PopLife/Archetypes/Shelf")]
    public class ShelfArchetype : BuildingArchetype
    {
        [Header("货架属性")]
        public ProductCategory category;
        public int basePrice = 100;

        [Serializable]
        public class ShelfLevelData : BuildingLevelData
        {
            public int maxStock = 10;
            public float attractiveness = 1f;
        }

        [SerializeField] private ShelfLevelData[] shelfLevels;
        public new int MaxLevel => shelfLevels?.Length ?? 0;

        public override BuildingLevelData GetLevel(int lvl)
        {
            if (shelfLevels == null || shelfLevels.Length == 0) return null;
            int i = Mathf.Clamp(lvl - 1, 0, shelfLevels.Length - 1);
            return shelfLevels[i];
        }

        public ShelfLevelData GetShelfLevel(int lvl) => (ShelfLevelData)GetLevel(lvl);
    }

    // 设施原型
    [CreateAssetMenu(menuName = "PopLife/Archetypes/Facility")]
    public class FacilityArchetype : BuildingArchetype
    {
        [Header("设施属性")]
        public FacilityType facilityType;

        [Serializable]
        public class FacilityEffect
        {
            public EffectType type;
            public float value = 1f;
            public float radius = 3f;
            public bool affectsSameFloor = true;
        }

        public List<FacilityEffect> effects = new();

        public override bool ValidatePlacement(PopLife.Runtime.FloorGrid floor, Vector2Int position, int rotation)
        {
            // 示例：每层仅允许一个收银台
            if (facilityType == FacilityType.Cashier && floor.HasFacilityOfType(FacilityType.Cashier))
                return false;

            return base.ValidatePlacement(floor, position, rotation);
        }
    }
}
