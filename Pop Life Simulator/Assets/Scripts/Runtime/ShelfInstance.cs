using System;
using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    public class ShelfInstance : BuildingInstance
    {
        [Header("货架状态")] public int currentStock;
        public int maxStock;
        public int todaySales;
        public int currentPrice;

        private ShelfArchetype SA => (ShelfArchetype)archetype;

        protected override void OnInitialized()
        {
            var lv = SA.GetShelfLevel(1);
            maxStock = lv.maxStock;
            currentStock = maxStock;
            currentPrice = SA.basePrice;
        }

        protected override void OnUpgraded()
        {
            var lv = SA.GetShelfLevel(currentLevel);
            maxStock = lv.maxStock;
            currentStock = maxStock; // 可改为不自动补满
        }

        public float GetAttractiveness()
        {
            var lv = SA.GetShelfLevel(currentLevel);
            float catMul = CategoryManager.Instance.GetCategoryMultiplier(SA.category);
            return lv.attractiveness * catMul;
        }

        public bool TrySellOne()
        {
            if (!isOperational || currentStock <= 0) return false;
            currentStock--;
            todaySales++;
            ResourceManager.Instance.AddMoney(currentPrice);
            return true;
        }

        public void Restock()
        {
            currentStock = maxStock;
            todaySales = 0;
        }

        public override BuildingSaveData GetSaveData()
        {
            var dto = base.GetSaveData();
            dto.payloadJson = JsonUtility.ToJson(new Payload
                { currentStock = currentStock, todaySales = todaySales, price = currentPrice });
            return dto;
        }

        public override void LoadFromSaveData(BuildingSaveData data)
        {
            base.LoadFromSaveData(data);
            if (!string.IsNullOrEmpty(data.payloadJson))
            {
                var p = JsonUtility.FromJson<Payload>(data.payloadJson);
                currentStock = p.currentStock;
                todaySales = p.todaySales;
                currentPrice = p.price;
            }
        }

        [Serializable]
        private struct Payload
        {
            public int currentStock, todaySales, price;
        }
    }
}