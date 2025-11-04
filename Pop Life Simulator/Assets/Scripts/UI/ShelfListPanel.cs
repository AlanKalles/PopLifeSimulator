using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife.UI
{
    /// <summary>
    /// Shelf list panel with dual filtering system
    /// Left sidebar: SelectPage filter buttons
    /// Top bar: ProductCategory filter buttons
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
        [SerializeField] private Transform selectPageButtonContainer; // Left sidebar
        [SerializeField] private Transform categoryButtonContainer; // Top bar
        [SerializeField] private GameObject filterButtonPrefab; // FilterToggleButton prefab

        [Header("SelectPage Icons")]
        [SerializeField] private Sprite bodyIcon;
        [SerializeField] private Sprite headIcon;
        [SerializeField] private Sprite frontIcon;
        [SerializeField] private Sprite backIcon;
        [SerializeField] private Sprite medicineBoxIcon;
        [SerializeField] private Sprite kinkyIcon;
        [SerializeField] private Sprite allPagesIcon; // Icon for "All" button

        [Header("Tooltip")]
        [SerializeField] private ShelfTooltip tooltip;

        [Header("Shelf Data")]
        [SerializeField] private List<ShelfArchetype> availableShelves = new List<ShelfArchetype>();
        [SerializeField] private bool loadShelvesFromResources = true;
        [SerializeField] private string shelfResourcePath = "ScriptableObjects/BuildingArchetype";

        [Header("References")]
        [SerializeField] private ConstructionManager constructionManager;

        // Filter state
        private FilterToggleGroup selectPageToggleGroup;
        private FilterToggleGroup categoryToggleGroup;
        private SelectPage? selectedSelectPage = null; // null = All
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

            // Initialize toggle groups
            InitializeFilterGroups();
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
            InitializeSelectPageButtons();
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
            foreach (var building in allBuildings)
            {
                if (building is ShelfArchetype shelf)
                {
                    availableShelves.Add(shelf);
                }
            }

            Debug.Log($"ShelfListPanel: Loaded {availableShelves.Count} shelves from Resources/{shelfResourcePath}");
        }

        #endregion

        #region Filter Group Initialization

        private void InitializeFilterGroups()
        {
            // Create SelectPage toggle group
            if (selectPageButtonContainer != null)
            {
                selectPageToggleGroup = selectPageButtonContainer.gameObject.AddComponent<FilterToggleGroup>();
                selectPageToggleGroup.OnSelectionChanged += OnSelectPageChanged;
            }

            // Create Category toggle group
            if (categoryButtonContainer != null)
            {
                categoryToggleGroup = categoryButtonContainer.gameObject.AddComponent<FilterToggleGroup>();
                categoryToggleGroup.OnSelectionChanged += OnCategoryChanged;
            }
        }

        private void InitializeSelectPageButtons()
        {
            if (selectPageButtonContainer == null || filterButtonPrefab == null)
            {
                Debug.LogWarning("ShelfListPanel: SelectPage button container or prefab not set.");
                return;
            }

            // Create "All" button with custom icon
            CreateSelectPageButton(null, "All", allPagesIcon);

            // Create button for each SelectPage enum value with custom icons
            CreateSelectPageButton(SelectPage.Body, "Body", bodyIcon);
            CreateSelectPageButton(SelectPage.Head, "Head", headIcon);
            CreateSelectPageButton(SelectPage.Front, "Front", frontIcon);
            CreateSelectPageButton(SelectPage.Back, "Back", backIcon);
            CreateSelectPageButton(SelectPage.MedicineBox, "Medicine", medicineBoxIcon);
            CreateSelectPageButton(SelectPage.Kinky, "Kinky", kinkyIcon);

            // Select "All" by default
            selectPageToggleGroup.SelectAll();
        }

        /// <summary>
        /// Create a SelectPage button with custom icon
        /// </summary>
        private void CreateSelectPageButton(SelectPage? page, string displayText, Sprite icon)
        {
            GameObject buttonObj = Instantiate(filterButtonPrefab, selectPageButtonContainer);
            FilterToggleButton toggleButton = buttonObj.GetComponent<FilterToggleButton>();

            if (toggleButton != null)
            {
                toggleButton.Initialize(page, displayText, (clickedToggle) =>
                {
                    selectPageToggleGroup.OnToggleClicked(clickedToggle);
                }, icon); // Pass custom icon

                selectPageToggleGroup.RegisterToggle(toggleButton);
            }
        }

        private void InitializeCategoryButtons()
        {
            if (categoryButtonContainer == null || filterButtonPrefab == null)
            {
                Debug.LogWarning("ShelfListPanel: Category button container or prefab not set.");
                return;
            }

            // Create "All" button
            CreateFilterButton(null, "All", categoryButtonContainer, categoryToggleGroup);

            // Create button for each ProductCategory enum value
            foreach (ProductCategory category in System.Enum.GetValues(typeof(ProductCategory)))
            {
                CreateFilterButton(category, category.ToString(), categoryButtonContainer, categoryToggleGroup);
            }

            // Select "All" by default
            categoryToggleGroup.SelectAll();
        }

        private void CreateFilterButton(object filterValue, string displayText, Transform parent, FilterToggleGroup group)
        {
            GameObject buttonObj = Instantiate(filterButtonPrefab, parent);
            FilterToggleButton toggleButton = buttonObj.GetComponent<FilterToggleButton>();

            if (toggleButton != null)
            {
                toggleButton.Initialize(filterValue, displayText, (clickedToggle) =>
                {
                    group.OnToggleClicked(clickedToggle);
                });

                group.RegisterToggle(toggleButton);
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

        private void OnSelectPageChanged(object filterValue)
        {
            selectedSelectPage = filterValue as SelectPage?;
            ApplyFilters();
        }

        private void OnCategoryChanged(object filterValue)
        {
            selectedCategory = filterValue as ProductCategory?;
            ApplyFilters();
        }

        /// <summary>
        /// Apply dual filtering: SelectPage first, then ProductCategory
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
        /// Determine if a shelf should be shown based on current filters
        /// </summary>
        private bool ShouldShowShelf(ShelfArchetype shelf)
        {
            if (shelf == null) return false;

            // First filter: SelectPage
            if (selectedSelectPage != null) // Not "All"
            {
                // Shelf must have the selected SelectPage in its selectPages array
                if (shelf.selectPages == null || !shelf.selectPages.Contains(selectedSelectPage.Value))
                {
                    return false;
                }
            }

            // Second filter: ProductCategory
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

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // Refresh item displays (money may have changed)
            RefreshItemDisplays();
        }

        public void ClosePanel()
        {
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

        #region Utility

        public void RefreshItemDisplays()
        {
            foreach (var item in itemInstances)
            {
                item.UpdateCostDisplay();
            }
        }

        public void AddShelf(ShelfArchetype shelf)
        {
            if (shelf == null || availableShelves.Contains(shelf))
                return;

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
            }
        }

        public void RemoveShelf(ShelfArchetype shelf)
        {
            if (shelf == null) return;

            availableShelves.Remove(shelf);

            ShelfListItem itemToRemove = itemInstances.Find(item => item.GetShelf() == shelf);
            if (itemToRemove != null)
            {
                itemInstances.Remove(itemToRemove);
                Destroy(itemToRemove.gameObject);
            }
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
