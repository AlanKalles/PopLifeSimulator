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
            currentStock = Mathf.Min(3, maxStock);
            currentPrice = lv.price;
        }

        protected override void OnUpgraded()
        {
            var lv = SA.GetShelfLevel(currentLevel);
            // 升级只提升上限和单价，currentStock 保持不变（由玩家通过补货面板补足）
            maxStock = lv.maxStock;
            currentPrice = lv.price;
            // 保险钳制：上限降低的极端情况
            currentStock = Mathf.Clamp(currentStock, 0, maxStock);
        }

        /// <summary>当前等级的每件补货成本（供 UI / 经济系统查询）</summary>
        public int GetStockFee()
        {
            return SA != null ? SA.GetStockFee(currentLevel) : 0;
        }

        public float GetAppeal()
        {
            var lv = SA.GetShelfLevel(currentLevel);
            float catMul = CategoryManager.Instance.GetCategoryMultiplier(SA.category);
            float baseAppeal = lv.appeal * catMul;
            // 全局修饰器：Appeal 平坦加值（按货架原型 + 按品类，两者叠加）
            int addend = 0;
            if (GlobalModifierManager.Instance != null)
            {
                addend += GlobalModifierManager.Instance.GetShelfAppealAddend(SA);
                addend += GlobalModifierManager.Instance.GetCategoryAppealAddend(SA.category);
            }
            return baseAppeal + addend;
        }

        /// <summary>
        /// 获取修饰后的有效售价（基础价格 × 品类价格乘数）
        /// 不修改 currentPrice 基础值，运行时动态计算
        /// </summary>
        public int GetEffectivePrice()
        {
            if (GlobalModifierManager.Instance == null) return currentPrice;
            float mul = GlobalModifierManager.Instance.GetCategoryPriceMultiplier(SA.category);
            return Mathf.Max(1, Mathf.RoundToInt(currentPrice * mul));
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

        /// <summary>重置到满库存（遗留接口，仅供调试/作弊使用；日循环和升级不再调用）</summary>
        public void Restock()
        {
            currentStock = maxStock;
            todaySales = 0;
        }

        /// <summary>增量补货——玩家通过补货面板调用，自动钳制到 maxStock</summary>
        public void RestockByAmount(int amount)
        {
            if (amount <= 0) return;
            currentStock = Mathf.Min(maxStock, currentStock + amount);
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
            // 1. base 先读，currentLevel 从 data.level 初始化
            base.LoadFromSaveData(data);

            // 2. 从 archetype 重算派生值——公式变更后，旧存档里存的 price/maxStock 可能过时，必须用新公式覆盖
            if (SA != null)
            {
                var lv = SA.GetShelfLevel(currentLevel);
                maxStock = lv.maxStock;
                currentPrice = lv.price;
            }

            // 3. 读取 payload 里的库存相关字段
            if (!string.IsNullOrEmpty(data.payloadJson))
            {
                var p = JsonUtility.FromJson<Payload>(data.payloadJson);
                currentStock = p.currentStock;
                todaySales = p.todaySales;
                // payload.price 故意忽略——以 archetype 新公式为准
            }

            // 4. 钳制：新公式下的 maxStock 可能比旧存档小
            currentStock = Mathf.Clamp(currentStock, 0, maxStock);
        }

        [Serializable]
        private struct Payload
        {
            public int currentStock, todaySales, price;
        }
    }
}