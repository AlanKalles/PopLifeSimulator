using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using PopLife.Data;
using TMPro;


namespace PopLife.Customers.Runtime
{
    [RequireComponent(typeof(CustomerBlackboardAdapter))]
    [RequireComponent(typeof(CustomerAnimationController))]
    public class CustomerAgent : MonoBehaviour
    {
        public CustomerBlackboardAdapter bb;
        public string customerID;

        // 当前访问会话
        public CustomerSession currentSession;

        // 缓存的原型（用于经验计算）
        public CustomerArchetype cachedArchetype;

        // 缓存的 CustomerRecord（用于交互系统获取对话ID）
        private CustomerRecord cachedRecord;

        private TextMeshPro nameText;
        private CustomerAnimationController animationController;

        void Awake()
        {
            if (!bb) bb = GetComponent<CustomerBlackboardAdapter>();
            if (!nameText) nameText = GetComponentInChildren<TextMeshPro>();
            if (!animationController) animationController = GetComponent<CustomerAnimationController>();
        }


// 原型期的最小初始化：由 Spawner 调用
        public void Initialize(CustomerRecord record, CustomerArchetype archetype, int daySeed)
        {
// 0) 设置顾客ID
            customerID = record.customerId;

// 0.1) 缓存 CustomerRecord（用于交互系统获取对话ID）
            cachedRecord = record;

// 1) 使用部件加载器设置外貌
            Sprite[] parts = CustomerPartLoader.LoadParts(customerID);
            if (parts != null && parts.Length >= 6)
            {
                animationController.SetupParts(parts);
                Debug.Log($"[CustomerAgent] {customerID}: 部件精灵设置成功");
            }
            else
            {
                Debug.LogWarning($"[CustomerAgent] {customerID}: 未找到部件资源，顾客将无外观显示");
            }

// 2) 从 record 加载偏好货架，预解析品类和品牌
            string[] favoriteIds = record.favoriteShelfIds ?? new string[4];
            var categories = new HashSet<int>();
            var brands = new HashSet<BrandData>();
            foreach (var id in favoriteIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var so = Resources.Load<ShelfArchetype>($"ScriptableObjects/BuildingArchetype/Shelf/{id}");
                if (so == null)
                {
                    Debug.LogWarning($"[CustomerAgent] {customerID}: 偏好货架 '{id}' 未找到");
                    continue;
                }
                categories.Add((int)so.category);
                if (so.brand != null) brands.Add(so.brand);
            }

// 3) 采样本次钱袋与尴尬上限（无 trait 乘数）
            int walletCap = Mathf.RoundToInt(record.walletCapBase * archetype.walletCapCurve.Eval(record.loyaltyLevel));
            int embarrassmentCap = Mathf.RoundToInt(archetype.embarrassmentCapCurve.Eval(record.loyaltyLevel));
            int queueTolerance = archetype.queueToleranceSeconds;
            float finalMoveSpeed = archetype.moveSpeed;

// 4) 注入黑板
            bb.InjectFromRecord(record, archetype, embarrassmentCap, finalMoveSpeed);
            bb.moneyBag = Random.Range(Mathf.Max(10, walletCap/2), walletCap + 1);
            bb.embarrassment = 0;
            bb.queueToleranceSec = queueTolerance;

            // 注入偏好数据
            bb.favoriteShelfIds = favoriteIds;
            bb.favoriteCategoryIndices = new int[categories.Count];
            categories.CopyTo(bb.favoriteCategoryIndices);
            bb.favoriteBrands = new BrandData[brands.Count];
            brands.CopyTo(bb.favoriteBrands);

// 5) 设置头顶名字显示
            if (nameText != null)
            {
                nameText.text = record.name;
            }

// 6) 创建当前会话
            currentSession = new CustomerSession
            {
                customerId = record.customerId,
                dayId = PopLife.DayLoopManager.Instance?.currentDay.ToString() ?? "0",
                sessionId = System.Guid.NewGuid().ToString(),
                moneyBagStart = bb.moneyBag,
                moneySpent = 0,
                trustDelta = 0,
                visitedShelves = new System.Collections.Generic.List<ShelfVisit>()
            };

// 7) 缓存原型（用于销毁时计算经验）
            cachedArchetype = archetype;

// 8) 设置动画控制器的顾客ID（保留接口）
            if (animationController != null)
            {
                animationController.SetCustomerID(customerID);
            }

            CustomerEventBus.RaiseSpawned(this);
        }

        /// <summary>
        /// 获取缓存的 CustomerRecord
        /// Get cached CustomerRecord
        /// </summary>
        public CustomerRecord GetCustomerRecord()
        {
            return cachedRecord;
        }
    }
}
