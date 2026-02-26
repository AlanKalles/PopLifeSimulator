using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.Runtime;
using PopLife.Manager;

namespace PopLife.UI
{
    /// <summary>
    /// Shelf list panel with ProductCategory filtering
    /// Top bar: ProductCategory filter buttons (sorted A-Z)
    /// Center: Scrollable grid of shelf items
    /// </summary>
    public class ShelfListPanel : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button openButton; // Optional

        [Header("Scroll View")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform contentContainer; // Grid content
        [SerializeField] private GameObject shelfItemPrefab; // ShelfListItem prefab

        [Header("Filter UI")]
        [SerializeField] private Transform categoryButtonContainer; // Top bar
        [SerializeField] private GameObject categoryButtonPrefab; // FilterToggleButton prefab (traditional Button)

        [Header("Tooltip")]
        [SerializeField] private ShelfTooltip tooltip;

        [Header("Shelf Data")]
        [SerializeField] private List<ShelfArchetype> availableShelves = new List<ShelfArchetype>();
        [SerializeField] private bool loadShelvesFromResources = true;
        [SerializeField] private string shelfResourcePath = "ScriptableObjects/BuildingArchetype";

        [Header("References")]
        [SerializeField] private ConstructionManager constructionManager;

        // Filter state
        private FilterToggleGroup categoryToggleGroup;
        private ProductCategory? selectedCategory = null; // null = All

        // Item instances
        private List<ShelfListItem> itemInstances = new List<ShelfListItem>();
        private bool isInitialized = false; // Track initialization state

        private void Awake()
        {
            // Register button events
            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            if (openButton != null)
                openButton.onClick.AddListener(OpenPanel);

            // Initial state: closed
            if (panelRoot != null)
                panelRoot.SetActive(false);

            // Auto-find ConstructionManager
            if (constructionManager == null)
                constructionManager = FindFirstObjectByType<ConstructionManager>();

            // Ensure tooltip exists
            if (tooltip == null)
            {
                tooltip = GetComponentInChildren<ShelfTooltip>(true);
                if (tooltip == null)
                {
                    Debug.LogWarning("ShelfListPanel: No ShelfTooltip found. Please add one as a child object.");
                }
            }

            // Initialize toggle group
            InitializeFilterGroup();

            // 订阅蓝图解锁事件
            BlueprintManager.OnShelfUnlocked += OnShelfBlueprintUnlocked;
        }

        private void Start()
        {
            // Don't initialize on Start - wait for first OpenPanel call
            // This prevents UI event system issues on first click
        }

        /// <summary>
        /// Initialize panel content (called on first open)
        /// </summary>
        private void InitializePanel()
        {
            if (isInitialized) return;

            // Load shelf data from Resources
            if (loadShelvesFromResources)
            {
                LoadShelvesFromResources();
            }

            // Initialize UI
            InitializeCategoryButtons();
            InitializeShelfItems();

            isInitialized = true;
        }

        #region Data Loading

        private void LoadShelvesFromResources()
        {
            // Load all BuildingArchetype and filter for ShelfArchetype only
            BuildingArchetype[] allBuildings = Resources.LoadAll<BuildingArchetype>(shelfResourcePath);

            availableShelves.Clear();

            // 获取已解锁的货架ID列表
            var unlockedShelfIds = BlueprintManager.Instance?.GetUnlockedShelfIds();

            if (unlockedShelfIds == null || unlockedShelfIds.Count == 0)
            {
                Debug.LogWarning("ShelfListPanel: No unlocked shelves found in BlueprintManager. Make sure BlueprintProfile.json is configured.");
                return;
            }

            foreach (var building in allBuildings)
            {
                if (building is ShelfArchetype shelf)
                {
                    // 只加载已解锁的货架
                    if (unlockedShelfIds.Contains(shelf.archetypeId))
                    {
                        availableShelves.Add(shelf);
                    }
                }
            }

            Debug.Log($"ShelfListPanel: Loaded {availableShelves.Count} unlocked shelves from Resources/{shelfResourcePath} (Total unlocked: {unlockedShelfIds.Count})");
        }

        #endregion

        #region Filter Group Initialization

        private void InitializeFilterGroup()
        {
            // Create Category toggle group
            if (categoryButtonContainer != null)
            {
                categoryToggleGroup = categoryButtonContainer.gameObject.AddComponent<FilterToggleGroup>();
                categoryToggleGroup.OnSelectionChanged += OnCategoryChanged;
            }
        }

        private void InitializeCategoryButtons()
        {
            if (categoryButtonContainer == null || categoryButtonPrefab == null)
            {
                Debug.LogWarning("ShelfListPanel: Category button container or prefab not set.");
                return;
            }

            // Create "All" button
            CreateCategoryButton(null, "All");

            // 只为有货架的类别创建按钮
            var sortedCategories = availableShelves
                .Where(s => s != null)
                .Select(s => s.category)
                .Distinct()
                .OrderBy(c => c.ToString())
                .ToList();

            foreach (var category in sortedCategories)
            {
                CreateCategoryButton(category, category.ToString());
            }

            // Select "All" by default
            categoryToggleGroup.SelectAll();
        }

        /// <summary>
        /// 重建类别按钮（解锁/移除货架导致类别变化时调用）
        /// </summary>
        private void RebuildCategoryButtons()
        {
            if (categoryToggleGroup == null) return;

            // 记住当前选择
            var previousSelection = selectedCategory;

            // 清理已有按钮
            categoryToggleGroup.Clear();
            for (int i = categoryButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(categoryButtonContainer.GetChild(i).gameObject);

            // 重建
            InitializeCategoryButtons();

            // 尝试恢复之前的选择（类别仍存在时）
            if (previousSelection != null &&
                availableShelves.Any(s => s != null && s.category == previousSelection.Value))
            {
                categoryToggleGroup.SelectByValue(previousSelection.Value);
            }

            ApplyFilters();
        }

        /// <summary>
        /// Create a Category button (uses FilterToggleButton with traditional Button)
        /// </summary>
        private void CreateCategoryButton(object filterValue, string displayText)
        {
            GameObject buttonObj = Instantiate(categoryButtonPrefab, categoryButtonContainer);
            FilterToggleButton toggleButton = buttonObj.GetComponent<FilterToggleButton>();

            if (toggleButton != null)
            {
                toggleButton.Initialize(filterValue, displayText, (clickedToggle) =>
                {
                    categoryToggleGroup.OnToggleClicked(clickedToggle);
                });

                categoryToggleGroup.RegisterToggle(toggleButton);
            }
            else
            {
                Debug.LogWarning($"ShelfListPanel: FilterToggleButton component not found on category prefab for '{displayText}'.");
            }
        }

        #endregion

        #region Shelf Item Initialization

        private void InitializeShelfItems()
        {
            ClearItemList();

            foreach (var shelf in availableShelves)
            {
                if (shelf == null) continue;

                // Create shelf item
                GameObject itemObj = Instantiate(shelfItemPrefab, contentContainer);
                ShelfListItem item = itemObj.GetComponent<ShelfListItem>();

                if (item != null)
                {
                    item.Initialize(shelf, OnShelfSelected, tooltip);
                    itemInstances.Add(item);
                }
            }

            // Apply initial filter (show all)
            ApplyFilters();
        }

        private void ClearItemList()
        {
            foreach (var item in itemInstances)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            itemInstances.Clear();
        }

        #endregion

        #region Filter Logic

        private void OnCategoryChanged(object filterValue)
        {
            selectedCategory = filterValue as ProductCategory?;
            ApplyFilters();
        }

        /// <summary>
        /// Apply ProductCategory filter
        /// </summary>
        private void ApplyFilters()
        {
            foreach (var item in itemInstances)
            {
                ShelfArchetype shelf = item.GetShelf();
                bool shouldShow = ShouldShowShelf(shelf);
                item.gameObject.SetActive(shouldShow);
            }
        }

        /// <summary>
        /// Determine if a shelf should be shown based on current category filter
        /// </summary>
        private bool ShouldShowShelf(ShelfArchetype shelf)
        {
            if (shelf == null) return false;

            // Filter by ProductCategory
            if (selectedCategory != null) // Not "All"
            {
                if (shelf.category != selectedCategory.Value)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Panel Control

        public void OpenPanel()
        {
            // Initialize on first open
            InitializePanel();

            // 确保 tooltip 从干净状态开始，防止闪现旧位置
            if (tooltip != null)
            {
                tooltip.HideImmediate();
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // Play open sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.UI_BUILD_PANEL_OPEN);
            }

            // Refresh item displays (money may have changed)
            RefreshItemDisplays();

            // 订阅金钱变化事件，实时刷新价格颜色
            SubscribeMoneyChanged();

            // Notify GameStateManager on first open
            // This will trigger the FirstBuildPhaseEntered marker for D002 dialogue
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.NotifyBuildModeFirstEntered();
            }
        }

        public void ClosePanel()
        {
            // 取消订阅金钱变化事件
            UnsubscribeMoneyChanged();

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // Hide tooltip if visible
            if (tooltip != null)
            {
                tooltip.HideImmediate();
            }
        }

        public void TogglePanel()
        {
            if (panelRoot != null)
            {
                if (panelRoot.activeSelf)
                    ClosePanel();
                else
                    OpenPanel();
            }
        }

        public bool IsOpen()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        #endregion

        #region Shelf Selection

        private void OnShelfSelected(ShelfArchetype shelf)
        {
            // Notify ConstructionManager to enter placement mode
            if (constructionManager != null)
            {
                constructionManager.SelectArchetypeForPlacement(shelf);
            }

            Debug.Log($"Selected shelf: {shelf.displayName}");

            // Optional: Close panel after selection
            // ClosePanel();
        }

        #endregion

        private void OnDestroy()
        {
            UnsubscribeMoneyChanged();
            BlueprintManager.OnShelfUnlocked -= OnShelfBlueprintUnlocked;
        }

        private void SubscribeMoneyChanged()
        {
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnMoneyChanged += OnMoneyChanged;
            ConstructionManager.OnBuildingPlacedOrDestroyed += OnBuildingChanged;
        }

        private void UnsubscribeMoneyChanged()
        {
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnMoneyChanged -= OnMoneyChanged;
            ConstructionManager.OnBuildingPlacedOrDestroyed -= OnBuildingChanged;
        }

        private void OnMoneyChanged(int currentMoney)
        {
            // 面板打开时才刷新
            if (IsOpen())
                RefreshItemDisplays();
        }

        private void OnBuildingChanged()
        {
            if (IsOpen())
                RefreshItemDisplays();
        }

        #region Utility

        /// <summary>
        /// 蓝图解锁时回调，面板已初始化才刷新（未初始化时首次打开会自动加载）
        /// </summary>
        private void OnShelfBlueprintUnlocked(ShelfArchetype shelf)
        {
            if (!isInitialized) return;
            AddShelf(shelf);
        }

        public void RefreshItemDisplays()
        {
            var placedArchetypes = GetPlacedShelfArchetypes();
            foreach (var item in itemInstances)
            {
                item.UpdateCostDisplay();
                item.SetPlacementDisabled(placedArchetypes.Contains(item.GetShelf()));
            }
        }

        /// <summary>
        /// 查询所有楼层中已放置的货架archetype（SO引用）
        /// </summary>
        private HashSet<ShelfArchetype> GetPlacedShelfArchetypes()
        {
            var placed = new HashSet<ShelfArchetype>();
            if (constructionManager == null || constructionManager.floorManager == null)
                return placed;
            foreach (var floor in constructionManager.floorManager.GetAllActiveFloors())
                foreach (var shelf in floor.AllShelves())
                    if (shelf.archetype is ShelfArchetype sa)
                        placed.Add(sa);
            return placed;
        }

        public void AddShelf(ShelfArchetype shelf)
        {
            if (shelf == null || availableShelves.Contains(shelf))
                return;

            // 检查是否引入了新类别
            bool isNewCategory = !availableShelves.Any(s => s != null && s.category == shelf.category);

            availableShelves.Add(shelf);

            // Create new item
            GameObject itemObj = Instantiate(shelfItemPrefab, contentContainer);
            ShelfListItem item = itemObj.GetComponent<ShelfListItem>();

            if (item != null)
            {
                item.Initialize(shelf, OnShelfSelected, tooltip);
                itemInstances.Add(item);

                // Apply filter to new item
                bool shouldShow = ShouldShowShelf(shelf);
                item.gameObject.SetActive(shouldShow);

                // 检查是否已放置
                var placedArchetypes = GetPlacedShelfArchetypes();
                item.SetPlacementDisabled(placedArchetypes.Contains(shelf));
            }

            // 新类别出现时重建类别按钮
            if (isNewCategory)
                RebuildCategoryButtons();
        }

        public void RemoveShelf(ShelfArchetype shelf)
        {
            if (shelf == null) return;

            var removedCategory = shelf.category;

            availableShelves.Remove(shelf);

            ShelfListItem itemToRemove = itemInstances.Find(item => item.GetShelf() == shelf);
            if (itemToRemove != null)
            {
                itemInstances.Remove(itemToRemove);
                Destroy(itemToRemove.gameObject);
            }

            // 该类别已无货架时重建类别按钮
            bool categoryEmpty = !availableShelves.Any(s => s != null && s.category == removedCategory);
            if (categoryEmpty)
                RebuildCategoryButtons();
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Refresh Shelf List")]
        private void EditorRefreshShelfList()
        {
            LoadShelvesFromResources();
            InitializeShelfItems();
        }
#endif

        #endregion
    }
}
