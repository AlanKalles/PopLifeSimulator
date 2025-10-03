using System;
using UnityEngine;
using PopLife.Data;
using PopLife.Customers.Services;

namespace PopLife.Runtime
{
    public class ShelfInstance : BuildingInstance
    {
        [Header("货架状态")]
        public int currentStock;
        public int maxStock;
        public int todaySales;
        public int currentPrice;

        private ShelfArchetype SA => (ShelfArchetype)archetype;
        private ShelfQueueController queueController;

        protected override void Awake()
        {
            base.Awake();
            queueController = GetComponent<ShelfQueueController>();
        }

        protected override void OnInitialized()
        {
            var lv = SA.GetShelfLevel(1);
            maxStock = lv.maxStock;
            currentStock = maxStock;
            currentPrice = lv.price;
        }

        protected override void OnUpgraded()
        {
            var lv = SA.GetShelfLevel(currentLevel);
            maxStock = lv.maxStock;
            currentStock = maxStock; // 可改为不自动补满
            currentPrice = lv.price;
        }

        public float GetAttractiveness()
        {
            var lv = SA.GetShelfLevel(currentLevel);
            float catMul = CategoryManager.Instance.GetCategoryMultiplier(SA.category);
            return lv.attractiveness * catMul;
        }

        /// <summary>
        /// 顾客取货 - 只扣库存，不增加玩家金钱（金钱在收银台结算）
        /// </summary>
        public bool TryTakeOne()
        {
            if (!isOperational || currentStock <= 0) return false;
            currentStock--;
            todaySales++;
            return true;
        }

        /// <summary>
        /// 直接售卖（旧方法，保留用于其他可能的直接销售场景）
        /// </summary>
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

        /// <summary>
        /// 获取交互锚点（供顾客寻路使用）
        /// </summary>
        public Transform GetInteractionPoint()
        {
            if (queueController != null)
            {
                return queueController.GetInteractionPoint();
            }
            // 降级：返回货架自身Transform
            return transform;
        }

        /// <summary>
        /// 获取队列长度（供策略系统查询）
        /// </summary>
        public int GetQueueLength()
        {
            if (queueController != null)
            {
                return queueController.GetQueueLength();
            }
            return 0;
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