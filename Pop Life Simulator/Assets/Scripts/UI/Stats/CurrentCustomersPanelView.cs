using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Manager;
using PopLife.Customers.Services;

namespace PopLife.UI.Stats
{
    /// <summary>
    /// Current Customers 面板 - 显示当日来访的所有顾客
    /// </summary>
    public class CurrentCustomersPanelView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentContainer; // VerticalLayoutGroup
        [SerializeField] private GameObject customerItemPrefab; // CustomerStatsItem 预制体
        [SerializeField] private TextMeshProUGUI customerCountText; // 底部显示店内人数

        [Header("Sort Controls")]
        [SerializeField] private TMP_Dropdown sortByDropdown; // Loyalty / Total Expense
        [SerializeField] private TMP_Dropdown sortOrderDropdown; // Ascending / Descending

        [Header("Update Settings")]
        [SerializeField] private float updateInterval = 1f; // 更新间隔

        // 当前显示的列表项 (customerId -> UI组件)
        private Dictionary<string, CustomerStatsItemUI> customerItems = new Dictionary<string, CustomerStatsItemUI>();

        private float lastUpdateTime;

        // 排序选项
        private enum SortBy { Loyalty, TotalExpense }
        private enum SortOrder { Ascending, Descending }
        private SortBy currentSortBy = SortBy.Loyalty;
        private SortOrder currentSortOrder = SortOrder.Ascending;

        void OnEnable()
        {
            // 初始化下拉菜单
            InitializeDropdowns();

            // 监听顾客事件
            CustomerEventBus.OnSpawned += OnCustomerSpawned;
            CustomerEventBus.OnCustomerDestroyed += OnCustomerDestroyed;

            // 监听下拉菜单事件
            if (sortByDropdown != null)
                sortByDropdown.onValueChanged.AddListener(OnSortByChanged);

            if (sortOrderDropdown != null)
                sortOrderDropdown.onValueChanged.AddListener(OnSortOrderChanged);

            // 立即刷新
            RefreshCustomerList();
            lastUpdateTime = Time.time;
        }

        void OnDisable()
        {
            CustomerEventBus.OnSpawned -= OnCustomerSpawned;
            CustomerEventBus.OnCustomerDestroyed -= OnCustomerDestroyed;

            // 取消下拉菜单事件
            if (sortByDropdown != null)
                sortByDropdown.onValueChanged.RemoveListener(OnSortByChanged);

            if (sortOrderDropdown != null)
                sortOrderDropdown.onValueChanged.RemoveListener(OnSortOrderChanged);
        }

        void Update()
        {
            // 定时刷新消费金额和店内人数
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateCustomerData();
                UpdateCustomerCount();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// 顾客生成时添加到列表顶部
        /// </summary>
        private void OnCustomerSpawned(PopLife.Customers.Runtime.CustomerAgent agent)
        {
            if (agent == null || customerItems.ContainsKey(agent.customerID)) return;

            // 从 StatsDataManager 获取顾客数据
            if (StatsDataManager.Instance != null)
            {
                var allCustomers = StatsDataManager.Instance.GetAllCustomerStats();
                var customerData = allCustomers.Find(c => c.customerId == agent.customerID);

                if (customerData != null)
                {
                    CreateCustomerItem(customerData, insertAtTop: true);
                }
            }
        }

        /// <summary>
        /// 顾客销毁时置灰
        /// </summary>
        private void OnCustomerDestroyed(PopLife.Customers.Runtime.CustomerAgent agent)
        {
            if (agent == null || !customerItems.ContainsKey(agent.customerID)) return;

            var itemUI = customerItems[agent.customerID];
            if (itemUI != null)
            {
                itemUI.SetGrayedOut(true);
            }
        }

        /// <summary>
        /// 初始化下拉菜单选项
        /// </summary>
        private void InitializeDropdowns()
        {
            // 初始化排序依据下拉菜单
            if (sortByDropdown != null)
            {
                sortByDropdown.ClearOptions();
                sortByDropdown.AddOptions(new List<string> { "Loyalty", "Total Expense" });
                sortByDropdown.value = (int)currentSortBy;
                sortByDropdown.RefreshShownValue();
            }

            // 初始化排序顺序下拉菜单
            if (sortOrderDropdown != null)
            {
                sortOrderDropdown.ClearOptions();
                sortOrderDropdown.AddOptions(new List<string> { "Ascending", "Descending" });
                sortOrderDropdown.value = (int)currentSortOrder;
                sortOrderDropdown.RefreshShownValue();
            }
        }

        /// <summary>
        /// 排序依据改变时的回调
        /// </summary>
        private void OnSortByChanged(int index)
        {
            currentSortBy = (SortBy)index;
            RefreshCustomerList();
        }

        /// <summary>
        /// 排序顺序改变时的回调
        /// </summary>
        private void OnSortOrderChanged(int index)
        {
            currentSortOrder = (SortOrder)index;
            RefreshCustomerList();
        }

        /// <summary>
        /// 刷新整个顾客列表（从 StatsDataManager 读取并排序）
        /// </summary>
        private void RefreshCustomerList()
        {
            if (StatsDataManager.Instance == null || contentContainer == null) return;

            // 清空旧列表
            ClearCustomerItems();

            // 获取所有顾客数据
            var allCustomers = StatsDataManager.Instance.GetAllCustomerStats();

            // 排序
            allCustomers = SortCustomers(allCustomers);

            // 生成列表项
            foreach (var customerData in allCustomers)
            {
                CreateCustomerItem(customerData, insertAtTop: false);
            }

            // 更新店内人数
            UpdateCustomerCount();
        }

        /// <summary>
        /// 根据当前排序设置对顾客列表排序
        /// </summary>
        private List<CustomerStatsData> SortCustomers(List<CustomerStatsData> customers)
        {
            List<CustomerStatsData> sorted = new List<CustomerStatsData>(customers);

            // 根据排序依据排序
            if (currentSortBy == SortBy.Loyalty)
            {
                sorted.Sort((a, b) => a.loyaltyLevel.CompareTo(b.loyaltyLevel));
            }
            else // TotalExpense
            {
                sorted.Sort((a, b) => a.totalSpent.CompareTo(b.totalSpent));
            }

            // 根据排序顺序反转
            if (currentSortOrder == SortOrder.Descending)
            {
                sorted.Reverse();
            }

            return sorted;
        }

        /// <summary>
        /// 更新所有顾客的消费金额
        /// </summary>
        private void UpdateCustomerData()
        {
            if (StatsDataManager.Instance == null) return;

            var allCustomers = StatsDataManager.Instance.GetAllCustomerStats();

            foreach (var customerData in allCustomers)
            {
                if (customerItems.ContainsKey(customerData.customerId))
                {
                    var itemUI = customerItems[customerData.customerId];
                    if (itemUI != null)
                    {
                        itemUI.UpdateSpent(customerData.totalSpent);
                        itemUI.SetGrayedOut(customerData.hasLeft);
                    }
                }
            }
        }

        /// <summary>
        /// 更新店内人数
        /// </summary>
        private void UpdateCustomerCount()
        {
            if (customerCountText == null) return;

            int currentCount = 0;

            if (StatsDataManager.Instance != null)
            {
                currentCount = StatsDataManager.Instance.GetCurrentCustomerCount();
            }

            customerCountText.text = $"Currently in Store:            {currentCount}";
        }

        /// <summary>
        /// 创建顾客列表项
        /// </summary>
        private void CreateCustomerItem(CustomerStatsData customerData, bool insertAtTop)
        {
            if (customerItemPrefab == null || contentContainer == null) return;

            GameObject itemObj = Instantiate(customerItemPrefab, contentContainer);
            var itemUI = itemObj.GetComponent<CustomerStatsItemUI>();

            if (itemUI != null)
            {
                itemUI.SetData(customerData);
                customerItems[customerData.customerId] = itemUI;

                // 插入到顶部
                if (insertAtTop)
                {
                    itemObj.transform.SetAsFirstSibling();
                }
            }
        }

        /// <summary>
        /// 清空所有列表项
        /// </summary>
        private void ClearCustomerItems()
        {
            foreach (var kvp in customerItems)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            customerItems.Clear();
        }
    }
}
