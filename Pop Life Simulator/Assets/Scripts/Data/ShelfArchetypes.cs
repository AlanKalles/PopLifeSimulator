using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Data
{
// 货架原型
    [CreateAssetMenu(menuName = "PopLife/Buildings/ShelfArchetype")]
    public class ShelfArchetype : BuildingArchetype
    {
        [Header("货架属性")]
        public ProductCategory category;

        [Serializable]
        public class ShelfLevelData : BuildingLevelData
        {
            public int price = 100;
            public int maxStock = 10;
            public float attractiveness = 1f;
        }

        [SerializeField] private ShelfLevelData[] shelfLevels;
        public override BuildingLevelData GetLevel(int lvl) => shelfLevels[Mathf.Clamp(lvl-1,0,shelfLevels.Length-1)];
        public ShelfLevelData GetShelfLevel(int lvl) => (ShelfLevelData)GetLevel(lvl);
        public override int MaxLevel => shelfLevels?.Length ?? 0;
    }
}