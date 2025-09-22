using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
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
}