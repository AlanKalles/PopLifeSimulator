using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Manager;
using PopLife.Data;
using PopLife.Customers.Services;

namespace PopLife.UI.Stats
{
    /// <summary>
    /// Products 面板 - 显示所有货架统计（可筛选和排序）
    /// </summary>
    public class ProductsPanelView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentContainer; // VerticalLayoutGroup
        [SerializeField] private GameObject shelfItemPrefab; // ShelfStatsItem 预制体

        [Header("Filter & Sort")]
        [SerializeField] private TMP_Dropdown categoryDropdown;
        [SerializeField] private TMP_Dropdown sortDropdown;

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 1f; // 更新间隔

        // 排序选项
        private enum SortType { Revenue, Level, UnitPrice }
        private SortType currentSortType = SortType.Revenue;

        // 类别过滤
        private ProductCategory? currentCategoryFilter = null; // null = All

        // 当前显示的列表项
        private List<ShelfStatsItemUI> shelfItems = new List<ShelfStatsItemUI>();

        // 上次建筑数量（用于检测建筑变化）
        private int lastBuildingCount = -1;
        private float lastUpdateTime;

        void OnEnable()
        {
            // 初始化下拉菜单
            InitializeDropdowns();

            // 监听顾客购买事件（营业阶段实时更新）
            CustomerEventBus.OnPurchased += OnCustomerPurchased;

            // 立即刷新
            RefreshShelfList();
            lastUpdateTime = Time.time;
        }

        void OnDisable()
        {
            CustomerEventBus.OnPurchased -= OnCustomerPurchased;
        }

        void Update()
        {
            // 定时检测建筑变化（建造阶段）
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                CheckBuildingChanges();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// 初始化下拉菜单
        /// </summary>
        private void InitializeDropdowns()
        {
            // Category Dropdown
            if (categoryDropdown != null)
            {
                categoryDropdown.ClearOptions();
                var categoryOptions = new List<string> { "All" };
                categoryOptions.AddRange(System.Enum.GetNames(typeof(ProductCategory)));
                categoryDropdown.AddOptions(categoryOptions);
                categoryDropdown.value = 0;
                categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
            }

            // Sort Dropdown
            if (sortDropdown != null)
            {
                sortDropdown.ClearOptions();
                sortDropdown.AddOptions(new List<string> { "Revenue", "Level", "Unit Price" });
                sortDropdown.value = 0; // 默认按收入排序
                sortDropdown.onValueChanged.AddListener(OnSortChanged);
            }
        }

        /// <summary>
        /// 类别筛选变化
        /// </summary>
        private void OnCategoryChanged(int index)
        {
            if (index == 0)
            {
                currentCategoryFilter = null; // All
            }
            else
            {
                currentCategoryFilter = (ProductCategory)(index - 1);
            }

            RefreshShelfList();
        }

        /// <summary>
        /// 排序方式变化
        /// </summary>
        private void OnSortChanged(int index)
        {
            currentSortType = (SortType)index;
            RefreshShelfList();
        }

        /// <summary>
        /// 顾客购买时触发（营业阶段实时更新）
        /// </summary>
        private void OnCustomerPurchased(PopLife.Customers.Runtime.CustomerAgent agent, PopLife.Runtime.ShelfInstance shelf, int qty, int price)
        {
            // 刷新列表（会重新排序）
            RefreshShelfList();
        }

        /// <summary>
        /// 检测建筑数量变化（建造阶段）
        /// </summary>
        private void CheckBuildingChanges()
        {
            if (StatsDataManager.Instance == null) return;

            var currentShelves = StatsDataManager.Instance.GetAllShelfStats();
            int currentCount = currentShelves.Count;

            if (currentCount != lastBuildingCount)
            {
                lastBuildingCount = currentCount;
                RefreshShelfList();
            }
        }

        /// <summary>
        /// 刷新货架列表
        /// </summary>
        private void RefreshShelfList()
        {
            if (StatsDataManager.Instance == null || contentContainer == null) return;

            // 获取所有货架数据
            var allShelves = StatsDataManager.Instance.GetAllShelfStats();

            // 应用类别筛选
            if (currentCategoryFilter.HasValue)
            {
                allShelves = allShelves.Where(s => s.category == currentCategoryFilter.Value.ToString()).ToList();
            }

            // 应用排序
            allShelves = SortShelves(allShelves);

            // 清空旧列表项
            ClearShelfItems();

            // 生成新列表项
            foreach (var shelfData in allShelves)
            {
                CreateShelfItem(shelfData);
            }

            // 更新建筑计数
            lastBuildingCount = StatsDataManager.Instance.GetAllShelfStats().Count;
        }

        /// <summary>
        /// 排序货架列表
        /// </summary>
        private List<ShelfStatsData> SortShelves(List<ShelfStatsData> shelves)
        {
            // 检查是否处于建造阶段且所有 revenue 都为 0
            bool isBuildPhase = DayLoopManager.Instance != null &&
                                DayLoopManager.Instance.currentPhase == PopLife.GamePhase.BuildPhase;
            bool allRevenueZero = shelves.All(s => s.todayRevenue == 0);

            if (isBuildPhase && allRevenueZero && currentSortType == SortType.Revenue)
            {
                // 建造阶段按加载顺序排列（不排序）
                return shelves;
            }

            // 营业阶段按选定方式排序
            switch (currentSortType)
            {
                case SortType.Revenue:
                    return shelves.OrderByDescending(s => s.todayRevenue).ToList();
                case SortType.Level:
                    return shelves.OrderByDescending(s => s.level).ToList();
                case SortType.UnitPrice:
                    return shelves.OrderByDescending(s => s.unitPrice).ToList();
                default:
                    return shelves;
            }
        }

        /// <summary>
        /// 创建货架列表项
        /// </summary>
        private void CreateShelfItem(ShelfStatsData shelfData)
        {
            if (shelfItemPrefab == null) return;

            GameObject itemObj = Instantiate(shelfItemPrefab, contentContainer);
            var itemUI = itemObj.GetComponent<ShelfStatsItemUI>();

            if (itemUI != null)
            {
                itemUI.SetData(shelfData);
                shelfItems.Add(itemUI);
            }
        }

        /// <summary>
        /// 清空所有列表项
        /// </summary>
        private void ClearShelfItems()
        {
            foreach (var item in shelfItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
            shelfItems.Clear();
        }
    }
}
