using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Runtime;
using PopLife.Data;

namespace PopLife.Manager
{
    /// <summary>
    /// 统计数据管理器 - 追踪所有货架和顾客的实时数据
    /// </summary>
    public class StatsDataManager : MonoBehaviour
    {
        public static StatsDataManager Instance { get; private set; }

        // 货架收入追踪 (shelfId -> 今日收入)
        private Dictionary<string, int> shelfRevenueTracker = new Dictionary<string, int>();

        // 顾客消费追踪
        private List<CustomerStatsData> customerStatsTracker = new List<CustomerStatsData>();

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnEnable()
        {
            // 监听顾客事件
            CustomerEventBus.OnPurchased += OnCustomerPurchased;
            CustomerEventBus.OnSpawned += OnCustomerSpawned;
            CustomerEventBus.OnReachedCashier += OnCustomerReachedCashier;
            CustomerEventBus.OnCustomerDestroyed += OnCustomerDestroyed;

            // 监听游戏循环事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
            }
        }

        void OnDisable()
        {
            // 取消事件监听
            CustomerEventBus.OnPurchased -= OnCustomerPurchased;
            CustomerEventBus.OnSpawned -= OnCustomerSpawned;
            CustomerEventBus.OnReachedCashier -= OnCustomerReachedCashier;
            CustomerEventBus.OnCustomerDestroyed -= OnCustomerDestroyed;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
            }
        }

        #region 事件处理

        /// <summary>
        /// 顾客购买时更新货架收入
        /// </summary>
        private void OnCustomerPurchased(CustomerAgent agent, ShelfInstance shelf, int quantity, int price)
        {
            if (shelf == null) return;
            var hostTile = WorldGrid.ResolveHostTile(shelf);
            if (hostTile == null || !ConstructionGuards.IsStoreOwnedByPlayer(hostTile.StoreId)) return;

            string shelfId = shelf.instanceId;
            int revenue = quantity * price;

            if (shelfRevenueTracker.ContainsKey(shelfId))
            {
                shelfRevenueTracker[shelfId] += revenue;
            }
            else
            {
                shelfRevenueTracker[shelfId] = revenue;
            }
        }

        /// <summary>
        /// 顾客生成时添加追踪记录
        /// </summary>
        private void OnCustomerSpawned(CustomerAgent agent)
        {
            if (agent == null) return;

            // 从 CustomerBlackboardAdapter 获取顾客信息（已在初始化时注入）
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter != null && adapter.visitPurpose != CustomerVisitPurpose.PlayerStore)
            {
                return;
            }

            var statsData = new CustomerStatsData
            {
                customerId = agent.customerID,
                name = adapter?.customerName ?? "Unknown",
                loyaltyLevel = adapter?.loyaltyLevel ?? 0,
                totalSpent = 0,
                sprite = CustomerPortraitLoader.LoadPortrait(agent.customerID, agent.cachedArchetype),
                hasLeft = false
            };

            customerStatsTracker.Add(statsData);
        }

        /// <summary>
        /// 顾客到达收银台时锁定消费金额（防止结账后归零）
        /// </summary>
        private void OnCustomerReachedCashier(CustomerAgent agent, FacilityInstance cashier)
        {
            if (agent == null || agent.currentSession == null) return;

            var statsData = customerStatsTracker.FirstOrDefault(c => c.customerId == agent.customerID);
            if (statsData != null)
            {
                // 从 CustomerBlackboardAdapter 获取 pendingPayment（待结账金额）
                var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
                if (adapter != null)
                {
                    statsData.totalSpent = adapter.pendingPayment;
                }
            }
        }

        /// <summary>
        /// 顾客离店时标记为已离开
        /// </summary>
        private void OnCustomerDestroyed(CustomerAgent agent)
        {
            if (agent == null) return;

            var statsData = customerStatsTracker.FirstOrDefault(c => c.customerId == agent.customerID);
            if (statsData != null)
            {
                statsData.hasLeft = true;
            }
        }

        /// <summary>
        /// 建造阶段开始时清空数据
        /// </summary>
        private void OnBuildPhaseStart()
        {
            shelfRevenueTracker.Clear();
            customerStatsTracker.Clear();
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 获取所有货架统计数据
        /// </summary>
        public List<ShelfStatsData> GetAllShelfStats()
        {
            var shelfStatsList = new List<ShelfStatsData>();

            // 遍历所有货架
            var wg = WorldGrid.Instance;
            if (wg != null)
            {
                foreach (var shelf in wg.AllShelves())
                {
                    var hostTile = WorldGrid.ResolveHostTile(shelf);
                    if (hostTile == null || !ConstructionGuards.IsStoreOwnedByPlayer(hostTile.StoreId))
                        continue;

                    // 获取货架今日收入
                    int todayRevenue = shelfRevenueTracker.ContainsKey(shelf.instanceId)
                        ? shelfRevenueTracker[shelf.instanceId]
                        : 0;

                    // 获取货架原型数据
                    var archetype = shelf.archetype as ShelfArchetype;
                    if (archetype == null) continue;

                    var statsData = new ShelfStatsData
                    {
                        shelfId = shelf.instanceId,
                        name = archetype.displayName,
                        category = archetype.category.ToString(),
                        level = shelf.currentLevel,
                        todayRevenue = todayRevenue,
                        unitPrice = shelf.currentPrice,
                        sprite = archetype.icon
                    };

                    shelfStatsList.Add(statsData);
                }
            }

            return shelfStatsList;
        }

        /// <summary>
        /// 获取所有顾客统计数据（按生成顺序倒序）
        /// </summary>
        public List<CustomerStatsData> GetAllCustomerStats()
        {
            // 返回副本并倒序（最新的在前）
            var result = new List<CustomerStatsData>(customerStatsTracker);
            result.Reverse();
            return result;
        }

        /// <summary>
        /// 获取今日成功购买的顾客数量（totalSpent > 0）
        /// 用于计算转化率：purchased / totalCustomers
        /// </summary>
        public int GetPurchasedCustomerCount()
        {
            int count = 0;
            foreach (var stat in customerStatsTracker)
            {
                if (stat != null && stat.totalSpent > 0)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取当前店内顾客数量
        /// </summary>
        public int GetCurrentCustomerCount()
        {
            if (CustomerPresenceService.Instance != null)
            {
                return CustomerPresenceService.Instance.CurrentInsideCount;
            }

            var customers = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var customer in customers)
            {
                var adapter = customer.GetComponent<CustomerBlackboardAdapter>();
                if (adapter != null
                    && adapter.hasEnteredStore
                    && adapter.visitPurpose == CustomerVisitPurpose.PlayerStore)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 获取维护费总额
        /// </summary>
        public float GetTotalMaintenanceFee()
        {
            float total = 0f;

            var wg = WorldGrid.Instance;
            if (wg != null)
            {
                foreach (var building in wg.AllBuildings())
                {
                    var hostTile = WorldGrid.ResolveHostTile(building);
                    if (hostTile == null || !ConstructionGuards.IsStoreOwnedByPlayer(hostTile.StoreId))
                        continue;

                    total += building.GetMaintenanceFee();
                }
            }

            return total;
        }

        /// <summary>
        /// 获取今日各品类收入明细
        /// </summary>
        public Dictionary<ProductCategory, float> GetCategoryRevenueBreakdown()
        {
            var breakdown = new Dictionary<ProductCategory, float>();
            foreach (ProductCategory cat in Enum.GetValues(typeof(ProductCategory)))
                breakdown[cat] = 0f;

            var wg = WorldGrid.Instance;
            if (wg != null)
            {
                foreach (var shelf in wg.AllShelves())
                {
                    var hostTile = WorldGrid.ResolveHostTile(shelf);
                    if (hostTile == null || !ConstructionGuards.IsStoreOwnedByPlayer(hostTile.StoreId))
                        continue;

                    var archetype = shelf.archetype as ShelfArchetype;
                    if (archetype == null) continue;

                    int revenue = shelfRevenueTracker.ContainsKey(shelf.instanceId)
                        ? shelfRevenueTracker[shelf.instanceId] : 0;
                    breakdown[archetype.category] += revenue;
                }
            }

            return breakdown;
        }

        #endregion
    }

    #region 数据结构

    /// <summary>
    /// 货架统计数据
    /// </summary>
    [Serializable]
    public class ShelfStatsData
    {
        public string shelfId;
        public string name;
        public string category;
        public int level;
        public int todayRevenue;    // 今日收入
        public int unitPrice;       // 单价
        public Sprite sprite;
    }

    /// <summary>
    /// 顾客统计数据
    /// </summary>
    [Serializable]
    public class CustomerStatsData
    {
        public string customerId;
        public string name;
        public int loyaltyLevel;
        public int totalSpent;      // 总消费
        public Sprite sprite;
        public bool hasLeft;        // 是否已离店
    }

    #endregion
}
