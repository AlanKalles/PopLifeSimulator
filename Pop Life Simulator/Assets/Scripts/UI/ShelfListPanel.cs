using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;
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

        [Header("Tab 切换")]
        [SerializeField] private Button shelvesTabButton;
        [SerializeField] private Button floorTabButton;
        [SerializeField] private RectMask2D shelvesTabMask;
        [SerializeField] private RectMask2D floorTabMask;

        [Header("Floor 类别区")]
        [SerializeField] private Transform floorCategoryContainer;
        [SerializeField] private GameObject floorTileItemPrefab;

        [Header("Animation Settings")]
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float slideDistance = 600f;

        private RectTransform panelRectTransform;
        private bool isOpen = false;
        private Tween slideTween;

        // Tab 状态
        private enum PanelTab { Shelves, Floor }
        private PanelTab currentTab = PanelTab.Shelves;

        // Filter state
        private FilterToggleGroup categoryToggleGroup;
        private ProductCategory? selectedCategory = null; // null = All

        // Shelf items
        private List<ShelfListItem> itemInstances = new List<ShelfListItem>();
        private bool isInitialized = false;

        // Floor tab
        private FilterToggleGroup floorCategoryToggleGroup;
        private List<FloorTileListItem> floorItemInstances = new();
        private List<FloorTileArchetype> availableFloorTiles = new();
        private string selectedFloorCategory; // "Floor" or "Elevator"

        private void Awake()
        {
            // Register button events
            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            if (openButton != null)
                openButton.onClick.AddListener(TogglePanel);

            // 缓存 RectTransform 并设置初始位置（屏幕下方隐藏）
            if (panelRoot != null)
            {
                panelRectTransform = panelRoot.GetComponent<RectTransform>();
                panelRectTransform.anchoredPosition = new Vector2(
                    panelRectTransform.anchoredPosition.x, -slideDistance);
                panelRoot.SetActive(false);
            }

            // Tab 按钮
            if (shelvesTabButton != null)
                shelvesTabButton.onClick.AddListener(() => SwitchTab(PanelTab.Shelves));
            if (floorTabButton != null)
                floorTabButton.onClick.AddListener(() => SwitchTab(PanelTab.Floor));

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
            BlueprintManager.OnFloorTileUnlocked += OnFloorTileBlueprintUnlocked;

            // 初始Tab遮罩状态（默认Shelves选中）
            UpdateTabMasks();
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
                LoadFloorTilesFromResources();
            }

            // Initialize UI
            InitializeCategoryButtons();
            InitializeShelfItems();
            InitializeFloorCategoryButtons();

            // 默认：Shelf类别区显示，Floor类别区隐藏
            if (categoryButtonContainer != null)
                categoryButtonContainer.gameObject.SetActive(true);
            if (floorCategoryContainer != null)
                floorCategoryContainer.gameObject.SetActive(false);

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

        #region Floor Tile Data & Items

        private void LoadFloorTilesFromResources()
        {
            var allBuildings = Resources.LoadAll<BuildingArchetype>(shelfResourcePath);
            availableFloorTiles.Clear();

            var unlockedIds = BlueprintManager.Instance?.GetUnlockedFloorTileIds();
            if (unlockedIds == null || unlockedIds.Count == 0) return;

            foreach (var building in allBuildings)
            {
                if (building is FloorTileArchetype fta && unlockedIds.Contains(fta.archetypeId))
                    availableFloorTiles.Add(fta);
            }

            // 按面积排序
            availableFloorTiles.Sort((a, b) =>
                (a.TileSize.x * a.TileSize.y).CompareTo(b.TileSize.x * b.TileSize.y));
        }

        private void InitializeFloorCategoryButtons()
        {
            if (floorCategoryContainer == null || categoryButtonPrefab == null) return;

            floorCategoryToggleGroup = floorCategoryContainer.gameObject.AddComponent<FilterToggleGroup>();
            floorCategoryToggleGroup.OnSelectionChanged += OnFloorCategoryChanged;

            // Floor 类别按钮（有解锁地板时才显示）
            CreateFloorCategoryButton("Floor", "Floor");

            // Elevator 类别按钮（需要蓝图解锁）
            var unlockedFloorIds = BlueprintManager.Instance?.GetUnlockedFloorTileIds();
            if (unlockedFloorIds != null && unlockedFloorIds.Contains("Elevator"))
            {
                CreateFloorCategoryButton("Elevator", "Elevator");
            }

            floorCategoryToggleGroup.SelectByValue("Floor"); // 默认选中 Floor
        }

        private void CreateFloorCategoryButton(object filterValue, string displayText)
        {
            var buttonObj = Instantiate(categoryButtonPrefab, floorCategoryContainer);
            var toggleButton = buttonObj.GetComponent<FilterToggleButton>();
            if (toggleButton != null)
            {
                toggleButton.Initialize(filterValue, displayText, (clickedToggle) =>
                {
                    floorCategoryToggleGroup.OnToggleClicked(clickedToggle);
                });
                floorCategoryToggleGroup.RegisterToggle(toggleButton);
            }
        }

        private void OnFloorCategoryChanged(object filterValue)
        {
            selectedFloorCategory = filterValue as string;
            RebuildFloorContent();
        }

        private void InitializeFloorItems()
        {
            ClearFloorItems();

            if (selectedFloorCategory == "Elevator")
            {
                // 电梯: 创建一个"Place Elevator"按钮
                if (floorTileItemPrefab != null)
                {
                    var itemObj = Instantiate(floorTileItemPrefab, contentContainer);
                    var item = itemObj.GetComponent<FloorTileListItem>();
                    if (item != null)
                    {
                        // 用 null archetype 初始化，特殊处理为电梯按钮
                        item.InitializeAsElevator(OnElevatorSelected);
                        floorItemInstances.Add(item);
                    }
                }
            }
            else // "Floor" 或默认
            {
                foreach (var fta in availableFloorTiles)
                {
                    if (fta == null || floorTileItemPrefab == null) continue;
                    var itemObj = Instantiate(floorTileItemPrefab, contentContainer);
                    var item = itemObj.GetComponent<FloorTileListItem>();
                    if (item != null)
                    {
                        item.Initialize(fta, OnFloorTileSelected);
                        floorItemInstances.Add(item);
                    }
                }
            }
        }

        private void ClearFloorItems()
        {
            foreach (var item in floorItemInstances)
            {
                if (item != null) Destroy(item.gameObject);
            }
            floorItemInstances.Clear();
        }

        private void RebuildFloorContent()
        {
            if (currentTab != PanelTab.Floor) return;
            ClearFloorItems();
            InitializeFloorItems();
        }

        private void OnFloorTileSelected(FloorTileArchetype fta)
        {
            if (constructionManager != null)
                constructionManager.SelectArchetypeForPlacement(fta);
            ClosePanel();
        }

        private void OnElevatorSelected()
        {
            if (constructionManager != null)
                constructionManager.BeginPlaceElevator();
            ClosePanel();
        }

        #endregion

        #region Tab 切换

        private void SwitchTab(PanelTab tab)
        {
            if (currentTab == tab) return;
            currentTab = tab;

            if (tab == PanelTab.Shelves)
            {
                // 显示 Shelf 类别区，隐藏 Floor 类别区
                if (categoryButtonContainer != null)
                    categoryButtonContainer.gameObject.SetActive(true);
                if (floorCategoryContainer != null)
                    floorCategoryContainer.gameObject.SetActive(false);

                // 清除 Floor 内容，重建 Shelf 内容
                ClearFloorItems();
                ClearItemList();
                InitializeShelfItems();
            }
            else
            {
                // 显示 Floor 类别区，隐藏 Shelf 类别区
                if (categoryButtonContainer != null)
                    categoryButtonContainer.gameObject.SetActive(false);
                if (floorCategoryContainer != null)
                    floorCategoryContainer.gameObject.SetActive(true);

                // 清除 Shelf 内容，重建 Floor 内容
                ClearItemList();
                ClearFloorItems();
                InitializeFloorItems();
            }

            // Tab 按钮高亮 + RectMask2D 切换
            UpdateTabButtonVisuals();
            UpdateTabMasks();
        }

        private void UpdateTabButtonVisuals()
        {
            // 简单处理：选中的 Tab 按钮 interactable = false（视觉上表示当前选中）
            if (shelvesTabButton != null)
                shelvesTabButton.interactable = currentTab != PanelTab.Shelves;
            if (floorTabButton != null)
                floorTabButton.interactable = currentTab != PanelTab.Floor;
        }

        /// <summary>
        /// 选中的Tab关闭RectMask2D（完整显示），未选中的开启（遮罩裁剪）
        /// </summary>
        private void UpdateTabMasks()
        {
            if (shelvesTabMask != null)
                shelvesTabMask.enabled = currentTab != PanelTab.Shelves;
            if (floorTabMask != null)
                floorTabMask.enabled = currentTab != PanelTab.Floor;
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
            if (isOpen) return;
            isOpen = true;

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

            // 从下往上滑入动画
            slideTween.Stop();
            slideTween = Tween.Custom(this, -slideDistance, 0f, slideDuration,
                (target, val) => target.panelRectTransform.anchoredPosition = new Vector2(
                    target.panelRectTransform.anchoredPosition.x, val),
                Ease.OutCubic, useUnscaledTime: true);

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
            if (!isOpen) return;
            isOpen = false;

            // 取消订阅金钱变化事件
            UnsubscribeMoneyChanged();

            // Hide tooltip if visible
            if (tooltip != null)
            {
                tooltip.HideImmediate();
            }

            // 从上往下滑出动画
            slideTween.Stop();
            slideTween = Tween.Custom(this, panelRectTransform.anchoredPosition.y, -slideDistance, slideDuration,
                (target, val) => target.panelRectTransform.anchoredPosition = new Vector2(
                    target.panelRectTransform.anchoredPosition.x, val),
                Ease.InCubic, useUnscaledTime: true)
                .OnComplete(this, target =>
                {
                    if (target.panelRoot != null)
                        target.panelRoot.SetActive(false);
                });
        }

        public void TogglePanel()
        {
            if (isOpen)
                ClosePanel();
            else
                OpenPanel();
        }

        public bool IsOpen()
        {
            return isOpen;
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
            BlueprintManager.OnFloorTileUnlocked -= OnFloorTileBlueprintUnlocked;
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

        private void OnFloorTileBlueprintUnlocked(FloorTileArchetype fta)
        {
            if (!isInitialized) return;
            if (fta == null || availableFloorTiles.Contains(fta)) return;

            availableFloorTiles.Add(fta);
            availableFloorTiles.Sort((a, b) =>
                (a.TileSize.x * a.TileSize.y).CompareTo(b.TileSize.x * b.TileSize.y));

            // 当前在 Floor Tab 时立即刷新内容
            if (currentTab == PanelTab.Floor)
                RebuildFloorContent();
        }

        public void RefreshItemDisplays()
        {
            // Shelf items
            var placedArchetypes = GetPlacedShelfArchetypes();
            foreach (var item in itemInstances)
            {
                item.UpdateCostDisplay();
                item.SetPlacementDisabled(placedArchetypes.Contains(item.GetShelf()));
            }

            // Floor items
            foreach (var item in floorItemInstances)
            {
                if (item != null)
                    item.UpdateCostDisplay();
            }
        }

        /// <summary>
        /// 查询所有楼层中已放置的货架archetype（SO引用）
        /// </summary>
        private HashSet<ShelfArchetype> GetPlacedShelfArchetypes()
        {
            var placed = new HashSet<ShelfArchetype>();
            var wg = WorldGrid.Instance;
            if (wg == null) return placed;
            foreach (var shelf in wg.AllShelves())
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
