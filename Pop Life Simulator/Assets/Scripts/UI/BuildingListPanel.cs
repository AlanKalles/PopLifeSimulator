using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife.UI
{
    /// <summary>
    /// Building list panel with scroll view
    /// Displays all available buildings for construction
    /// </summary>
    public class BuildingListPanel : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot; // 整个面板的根对象
        [SerializeField] private Button closeButton; // 关闭按钮
        [SerializeField] private Button openButton; // 打开面板的按钮（可选，也可以从外部调用）

        [Header("Scroll View")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentContainer; // ScrollView的Content容器
        [SerializeField] private GameObject itemPrefab; // BuildingListItem预制体

        [Header("Category Filter (Optional)")]
        [SerializeField] private TMP_Dropdown categoryDropdown; // 可选的类别筛选下拉框
        [SerializeField] private bool enableCategoryFilter = false;

        [Header("Building Data")]
        [SerializeField] private List<BuildingArchetype> availableBuildings = new List<BuildingArchetype>();
        [SerializeField] private bool loadBuildingsFromResources = true; // 是否从Resources自动加载
        [SerializeField] private string buildingResourcePath = "ScriptableObjects/BuildingArchetype"; // Resources路径

        [Header("References")]
        [SerializeField] private ConstructionManager constructionManager;

        private List<BuildingListItem> itemInstances = new List<BuildingListItem>();
        private BuildingArchetype currentSelectedArchetype;

        private void Awake()
        {
            // 注册按钮事件
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
            }

            if (openButton != null)
            {
                openButton.onClick.AddListener(OpenPanel);
            }

            // 初始状态关闭面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // 自动查找ConstructionManager
            if (constructionManager == null)
            {
                constructionManager = FindObjectOfType<ConstructionManager>();
            }
        }

        private void Start()
        {
            // 从Resources加载建筑数据
            if (loadBuildingsFromResources)
            {
                LoadBuildingsFromResources();
            }

            // 初始化面板
            InitializePanel();
        }

        /// <summary>
        /// Load all BuildingArchetype assets from Resources folder
        /// </summary>
        private void LoadBuildingsFromResources()
        {
            // 加载所有建筑原型
            BuildingArchetype[] loadedBuildings = Resources.LoadAll<BuildingArchetype>(buildingResourcePath);

            if (loadedBuildings != null && loadedBuildings.Length > 0)
            {
                availableBuildings.Clear();
                availableBuildings.AddRange(loadedBuildings);
                Debug.Log($"BuildingListPanel: Loaded {loadedBuildings.Length} buildings from Resources/{buildingResourcePath}");
            }
            else
            {
                Debug.LogWarning($"BuildingListPanel: No buildings found in Resources/{buildingResourcePath}");
            }
        }

        /// <summary>
        /// Initialize panel UI and populate building list
        /// </summary>
        private void InitializePanel()
        {
            // 清空现有列表
            ClearItemList();

            // 创建列表项
            foreach (var building in availableBuildings)
            {
                if (building == null) continue;

                // 实例化列表项
                GameObject itemObj = Instantiate(itemPrefab, contentContainer);
                BuildingListItem item = itemObj.GetComponent<BuildingListItem>();

                if (item != null)
                {
                    item.Initialize(building, OnBuildingSelected);
                    itemInstances.Add(item);
                }
            }

            // 初始化类别筛选（如果启用）
            if (enableCategoryFilter && categoryDropdown != null)
            {
                InitializeCategoryFilter();
            }
        }

        /// <summary>
        /// Initialize category dropdown filter
        /// </summary>
        private void InitializeCategoryFilter()
        {
            categoryDropdown.ClearOptions();

            List<string> options = new List<string> { "All" };
            // 可以根据需要添加类别选项（Shelf, Facility等）
            options.Add("Shelf");
            options.Add("Facility");

            categoryDropdown.AddOptions(options);
            categoryDropdown.onValueChanged.AddListener(OnCategoryFilterChanged);
        }

        /// <summary>
        /// Handle category filter change
        /// </summary>
        private void OnCategoryFilterChanged(int index)
        {
            // 根据选择的类别过滤显示的建筑
            string category = categoryDropdown.options[index].text;

            foreach (var item in itemInstances)
            {
                if (category == "All")
                {
                    item.gameObject.SetActive(true);
                }
                else if (category == "Shelf")
                {
                    item.gameObject.SetActive(item.GetArchetype() is ShelfArchetype);
                }
                else if (category == "Facility")
                {
                    item.gameObject.SetActive(item.GetArchetype() is FacilityArchetype);
                }
            }
        }

        /// <summary>
        /// Clear all item instances
        /// </summary>
        private void ClearItemList()
        {
            foreach (var item in itemInstances)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
            itemInstances.Clear();
        }

        /// <summary>
        /// Callback when a building is selected
        /// </summary>
        private void OnBuildingSelected(BuildingArchetype archetype)
        {
            // 取消之前的选中状态
            foreach (var item in itemInstances)
            {
                item.SetSelected(false);
            }

            // 设置新的选中状态
            currentSelectedArchetype = archetype;
            BuildingListItem selectedItem = itemInstances.Find(item => item.GetArchetype() == archetype);
            if (selectedItem != null)
            {
                selectedItem.SetSelected(true);
            }

            // 通知ConstructionManager进入放置模式
            if (constructionManager != null)
            {
                constructionManager.SelectArchetypeForPlacement(archetype);
            }

            Debug.Log($"Selected building: {archetype.displayName}");

            // 可以选择在选择建筑后自动关闭面板
            // ClosePanel();
        }

        /// <summary>
        /// Open the building list panel
        /// </summary>
        public void OpenPanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // 刷新所有项的价格显示（金钱可能变化了）
            RefreshItemDisplays();

            // 播放打开音效（如果有AudioManager）
            // AudioManager.Instance?.PlayUISound("panel_open");
        }

        /// <summary>
        /// Close the building list panel
        /// </summary>
        public void ClosePanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // 取消选中状态
            foreach (var item in itemInstances)
            {
                item.SetSelected(false);
            }
            currentSelectedArchetype = null;

            // 播放关闭音效
            // AudioManager.Instance?.PlayUISound("panel_close");
        }

        /// <summary>
        /// Toggle panel visibility
        /// </summary>
        public void TogglePanel()
        {
            if (panelRoot != null)
            {
                if (panelRoot.activeSelf)
                {
                    ClosePanel();
                }
                else
                {
                    OpenPanel();
                }
            }
        }

        /// <summary>
        /// Refresh all item displays (e.g., after money changes)
        /// </summary>
        public void RefreshItemDisplays()
        {
            foreach (var item in itemInstances)
            {
                item.UpdateCostDisplay();
            }
        }

        /// <summary>
        /// Add a building to the available list dynamically
        /// </summary>
        public void AddBuilding(BuildingArchetype archetype)
        {
            if (archetype == null || availableBuildings.Contains(archetype))
                return;

            availableBuildings.Add(archetype);

            // 创建新的列表项
            GameObject itemObj = Instantiate(itemPrefab, contentContainer);
            BuildingListItem item = itemObj.GetComponent<BuildingListItem>();

            if (item != null)
            {
                item.Initialize(archetype, OnBuildingSelected);
                itemInstances.Add(item);
            }
        }

        /// <summary>
        /// Remove a building from the list
        /// </summary>
        public void RemoveBuilding(BuildingArchetype archetype)
        {
            if (archetype == null) return;

            availableBuildings.Remove(archetype);

            // 移除对应的UI项
            BuildingListItem itemToRemove = itemInstances.Find(item => item.GetArchetype() == archetype);
            if (itemToRemove != null)
            {
                itemInstances.Remove(itemToRemove);
                Destroy(itemToRemove.gameObject);
            }
        }

        /// <summary>
        /// Check if panel is currently open
        /// </summary>
        public bool IsOpen()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Refresh Building List")]
        private void EditorRefreshBuildingList()
        {
            LoadBuildingsFromResources();
            InitializePanel();
        }
#endif

        #endregion
    }
}
