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

        // 顾客会话快照（销毁前 snapshot），用于 P2 Customer Analysis
        private List<CustomerSession> todaySessions = new List<CustomerSession>();

        // Hot Seller / 货架图标 sprite 缓存：优先用 prefab 的 SpriteRenderer.sprite，archetype.icon 兜底
        private static readonly Dictionary<string, Sprite> shelfPrefabSpriteCache = new Dictionary<string, Sprite>();
        private static Sprite GetShelfPrefabSprite(ShelfArchetype sa)
        {
            if (sa == null || sa.prefab == null) return null;
            string key = sa.archetypeId ?? sa.name;
            if (shelfPrefabSpriteCache.TryGetValue(key, out var cached)) return cached;
            var sr = sa.prefab.GetComponent<SpriteRenderer>();
            var sprite = sr != null ? sr.sprite : null;
            shelfPrefabSpriteCache[key] = sprite;
            return sprite;
        }

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

            // snapshot record.everEnteredStore 在入店前的状态，用于判定 New vs Returning
            bool preEntered = false;
            if (CustomerRepository.Instance != null)
            {
                var record = CustomerRepository.Instance.Get(agent.customerID);
                if (record != null) preEntered = record.everEnteredStore;
            }

            var statsData = new CustomerStatsData
            {
                customerId = agent.customerID,
                name = adapter?.customerName ?? "Unknown",
                loyaltyLevel = adapter?.loyaltyLevel ?? 0,
                totalSpent = 0,
                sprite = CustomerPortraitLoader.LoadPortrait(agent.customerID, agent.cachedArchetype),
                hasLeft = false,
                preEntered = preEntered
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
        /// 顾客离店时标记为已离开，并 snapshot session（用于 P2 Customer Analysis）
        /// </summary>
        private void OnCustomerDestroyed(CustomerAgent agent)
        {
            if (agent == null) return;

            var statsData = customerStatsTracker.FirstOrDefault(c => c.customerId == agent.customerID);
            if (statsData != null)
            {
                statsData.hasLeft = true;
            }

            // Snapshot session for analysis（仅 PlayerStore 顾客）
            var adapter = agent.GetComponent<CustomerBlackboardAdapter>();
            if (adapter != null
                && adapter.visitPurpose == CustomerVisitPurpose.PlayerStore
                && agent.currentSession != null)
            {
                todaySessions.Add(agent.currentSession);
            }
        }

        /// <summary>
        /// 建造阶段开始时清空数据
        /// </summary>
        private void OnBuildPhaseStart()
        {
            shelfRevenueTracker.Clear();
            customerStatsTracker.Clear();
            todaySessions.Clear();
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
                        // prefab 的 SpriteRenderer.sprite 优先（与场上货架美术一致），icon 字段兜底
                        sprite = GetShelfPrefabSprite(archetype) ?? archetype.icon
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

        /// <summary>
        /// 获取今日新顾客 / 回头客拆分（基于 record.everEnteredStore 入店前 snapshot）
        /// 注：分母用 customerStatsTracker.Count（spawned 计数），不是真实 entered count；
        /// 仅 PlayerStore 顾客（OnCustomerSpawned 已过滤）
        /// </summary>
        public (int newCount, int returningCount) GetNewVsReturningSplit()
        {
            int newCount = 0, returningCount = 0;
            foreach (var stat in customerStatsTracker)
            {
                if (stat == null) continue;
                if (stat.preEntered) returningCount++;
                else newCount++;
            }
            return (newCount, returningCount);
        }

        /// <summary>
        /// 今日平均每位入店顾客购买的件数
        /// 分母：CustomerPresenceService.EnteredTodayCount（含未购买进店顾客）
        /// 分子：所有 ShelfVisit.boughtQty 求和
        /// </summary>
        public float GetAvgProductsPerCustomer()
        {
            int enteredCount = CustomerPresenceService.Instance?.EnteredTodayCount ?? 0;
            if (enteredCount <= 0) return 0f;

            int totalBought = 0;
            foreach (var session in todaySessions)
            {
                if (session?.visitedShelves == null) continue;
                foreach (var visit in session.visitedShelves)
                    totalBought += visit.boughtQty;
            }
            return (float)totalBought / enteredCount;
        }

        /// <summary>
        /// 今日平均每位入店顾客购买过的不同货架数
        /// 分母：EnteredTodayCount，分子：每个 session 的 distinct shelfId（boughtQty>0）求和
        /// </summary>
        public float GetAvgShelvesPurchasedPerCustomer()
        {
            int enteredCount = CustomerPresenceService.Instance?.EnteredTodayCount ?? 0;
            if (enteredCount <= 0) return 0f;

            int totalDistinctShelves = 0;
            foreach (var session in todaySessions)
            {
                if (session?.visitedShelves == null) continue;
                var seen = new HashSet<string>();
                foreach (var visit in session.visitedShelves)
                {
                    if (visit.boughtQty > 0 && !string.IsNullOrEmpty(visit.shelfId))
                        seen.Add(visit.shelfId);
                }
                totalDistinctShelves += seen.Count;
            }
            return (float)totalDistinctShelves / enteredCount;
        }

        /// <summary>
        /// 今日销售额最高的货架名称（基于已存在的 shelfRevenueTracker）
        /// </summary>
        public string GetMostPurchasedShelfName()
        {
            if (shelfRevenueTracker.Count == 0) return null;

            string topShelfId = null;
            int topRevenue = 0;
            foreach (var kv in shelfRevenueTracker)
            {
                if (kv.Value > topRevenue)
                {
                    topRevenue = kv.Value;
                    topShelfId = kv.Key;
                }
            }
            if (string.IsNullOrEmpty(topShelfId)) return null;

            // 查 WorldGrid 找到对应 shelf 的 displayName
            var wg = WorldGrid.Instance;
            if (wg != null)
            {
                foreach (var shelf in wg.AllShelves())
                {
                    if (shelf.instanceId != topShelfId) continue;
                    var archetype = shelf.archetype as ShelfArchetype;
                    return archetype != null ? archetype.displayName : null;
                }
            }
            return null;
        }

        /// <summary>
        /// 今日销售额最高的品类
        /// </summary>
        public ProductCategory? GetMostPurchasedCategory()
        {
            var breakdown = GetCategoryRevenueBreakdown();
            if (breakdown.Count == 0) return null;

            ProductCategory? top = null;
            float topRevenue = 0f;
            foreach (var kv in breakdown)
            {
                if (kv.Value > topRevenue)
                {
                    topRevenue = kv.Value;
                    top = kv.Key;
                }
            }
            return top;
        }

        /// <summary>
        /// 单货架最高消费：同一会话同一货架的 spending 总和的最大值
        /// （顾客在一个货架上的最高单笔总消费）
        /// </summary>
        public int GetHighestSingleShelfSpend()
        {
            int maxSpend = 0;
            foreach (var session in todaySessions)
            {
                if (session?.visitedShelves == null) continue;
                // 同一会话内按 shelfId 聚合 spending
                var spendByShelf = new Dictionary<string, int>();
                foreach (var visit in session.visitedShelves)
                {
                    if (string.IsNullOrEmpty(visit.shelfId)) continue;
                    if (!spendByShelf.ContainsKey(visit.shelfId))
                        spendByShelf[visit.shelfId] = 0;
                    spendByShelf[visit.shelfId] += visit.spending;
                }
                foreach (var v in spendByShelf.Values)
                {
                    if (v > maxSpend) maxSpend = v;
                }
            }
            return maxSpend;
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
    /// preEntered: 入店前是否曾经进过店（基于 record.everEnteredStore snapshot）
    ///             true = 回头客, false = 新客
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
        public bool preEntered;     // 入店前是否曾经进过店（用于新客/回头客判定）
    }

    #endregion
}
