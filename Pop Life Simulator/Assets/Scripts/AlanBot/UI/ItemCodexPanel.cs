using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.UI;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// Item Codex 完整面板
    /// Header：标题 + 类别横向滚动标签
    /// 左侧：搜索框 + 排序 + 货架列表 + Mark All As Seen
    /// 右侧：货架详情（图片、信息、数值）
    /// </summary>
    public class ItemCodexPanel : MonoBehaviour
    {
        // ── 排序模式 ──
        public enum SortMode
        {
            CodexAsc, CodexDesc,
            AlphaAsc, AlphaDesc,
            CostAsc, CostDesc
        }

        [Header("面板控制")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;

        [Header("Header")]
        [SerializeField] private TMP_Text titleText;

        [Header("Category Tabs")]
        [SerializeField] private Transform categoryTabContainer;
        [SerializeField] private GameObject categoryTabPrefab;
        [SerializeField] private Button scrollLeftButton;
        [SerializeField] private Button scrollRightButton;
        [SerializeField] private ScrollRect categoryScrollRect;
        [SerializeField] private float categoryScrollStep = 0.15f;

        [Header("Left Panel - Search & Sort")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button sortButton;
        [SerializeField] private GameObject sortDropdown;
        [SerializeField] private Button sortCodexAscButton;
        [SerializeField] private Button sortCodexDescButton;
        [SerializeField] private Button sortAlphaAscButton;
        [SerializeField] private Button sortAlphaDescButton;
        [SerializeField] private Button sortCostAscButton;
        [SerializeField] private Button sortCostDescButton;

        [Header("Left Panel - List")]
        [SerializeField] private ScrollRect listScrollRect;
        [SerializeField] private Transform listContainer;
        [SerializeField] private CodexShelfEntry entryPrefab;
        [SerializeField] private Button markAllSeenButton;

        [Header("Right Panel")]
        [SerializeField] private GameObject detailContent;
        [SerializeField] private GameObject detailEmptyState;
        [SerializeField] private Image shelfImage;
        [SerializeField] private TMP_Text shelfNameText;
        [SerializeField] private TMP_Text brandText;
        [SerializeField] private TMP_Text categoryText;

        [Header("Right Panel - Page Toggle")]
        [SerializeField] private Button detailTabButton;
        [SerializeField] private Button statsTabButton;
        [SerializeField] private GameObject detailPage;
        [SerializeField] private GameObject statsPage;
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Right Panel - Detail Page")]
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text usageInstructionText;

        [Header("Right Panel - Stats Page")]
        [SerializeField] private TMP_Text buildCostText;
        [SerializeField] private TMP_Text maintenanceFeeText;
        [SerializeField] private TMP_Text sellPriceText;
        [SerializeField] private TMP_Text stockFeeText; // 新增：每件补货成本
        [SerializeField] private TMP_Text profitText;   // 新增：每件利润
        [SerializeField] private TMP_Text stockText;
        [SerializeField] private TMP_Text appealText;

        [Header("Data")]
        [SerializeField] private CodexMasterList masterList;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        // ── 内部状态 ──
        private ShelfArchetype[] allShelves;
        private FilterToggleGroup categoryToggleGroup;
        private readonly List<FilterToggleButton> categoryTabs = new();
        private ProductCategory? selectedCategory;
        private SortMode currentSort = SortMode.CodexAsc;
        private string searchQuery = "";
        private readonly List<CodexShelfEntry> spawnedEntries = new();
        private CodexShelfEntry selectedEntry;
        private Coroutine fadeCoroutine;
        private bool isInitialized;
        private bool showingStats; // false=Detail页, true=Stats页

        // ── 关闭回调 ──
        private Action onCloseCallback;

        #region Unity 生命周期

        private void Awake()
        {
            // 初始隐藏
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (titleText != null)
                titleText.text = "ITEM CODEX";

            // 绑定按钮
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (scrollLeftButton != null)
                scrollLeftButton.onClick.AddListener(OnScrollCategoryLeft);

            if (scrollRightButton != null)
                scrollRightButton.onClick.AddListener(OnScrollCategoryRight);

            // 搜索框
            if (searchInput != null)
                searchInput.onValueChanged.AddListener(OnSearchChanged);

            // 排序按钮
            if (sortButton != null)
                sortButton.onClick.AddListener(ToggleSortDropdown);

            BindSortOptions();

            // Mark All As Seen
            if (markAllSeenButton != null)
                markAllSeenButton.onClick.AddListener(OnMarkAllSeenClicked);

            // Detail/Stats 页面切换按钮
            if (detailTabButton != null)
                detailTabButton.onClick.AddListener(SwitchToDetailPage);
            if (statsTabButton != null)
                statsTabButton.onClick.AddListener(SwitchToStatsPage);

            // 排序下拉初始隐藏
            if (sortDropdown != null)
                sortDropdown.SetActive(false);
        }

        private void OnEnable()
        {
            // 订阅图鉴状态事件
            if (CodexStateManager.Instance != null)
            {
                CodexStateManager.Instance.OnShelfSeen += OnShelfSeenChanged;
                CodexStateManager.Instance.OnNewShelfAvailable += OnNewShelfAvailable;
            }

            // 订阅蓝图解锁事件（用于刷新类别锁定状态）
            BlueprintManager.OnShelfUnlocked += OnBlueprintUnlocked;
        }

        private void OnDisable()
        {
            if (CodexStateManager.Instance != null)
            {
                CodexStateManager.Instance.OnShelfSeen -= OnShelfSeenChanged;
                CodexStateManager.Instance.OnNewShelfAvailable -= OnNewShelfAvailable;
            }

            BlueprintManager.OnShelfUnlocked -= OnBlueprintUnlocked;
        }

        #endregion

        #region 面板 Show/Hide

        /// <summary>
        /// 显示面板，可传入关闭时的回调（如返回AlanBot选择面板）
        /// </summary>
        public void Show(Action closeCallback = null)
        {
            onCloseCallback = closeCallback;

            if (!isInitialized)
                InitializePanel();

            if (panelRoot != null)
                panelRoot.SetActive(true);

            // 刷新条目列表
            RefreshAllEntries();

            // 如果没有选中条目，选中第一个可用的
            if (selectedEntry == null)
                SelectFirstUnlockedEntry();

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration, true, true));
        }

        public void Hide()
        {
            // 关闭排序下拉
            if (sortDropdown != null)
                sortDropdown.SetActive(false);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeAndHide());
        }

        private IEnumerator FadeAndHide()
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutDuration, false, false);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            // 触发关闭回调（返回上级面板）
            var callback = onCloseCallback;
            onCloseCallback = null;
            callback?.Invoke();
        }

        private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration,
            bool interactable, bool blocksRaycasts)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        #endregion

        #region 初始化

        private void InitializePanel()
        {
            if (masterList == null)
            {
                Debug.LogError("[ItemCodexPanel] CodexMasterList 未赋值！");
                return;
            }

            // 加载主列表数据
            allShelves = masterList.Shelves.Where(s => s != null).ToArray();

            // 初始化类别标签
            InitializeCategoryTabs();

            // 显示空状态
            ShowEmptyState();

            // 初始化页面切换按钮状态（默认Detail页）
            ApplyPageToggle();

            isInitialized = true;
        }

        private void InitializeCategoryTabs()
        {
            if (categoryTabContainer == null || categoryTabPrefab == null) return;

            // 创建 FilterToggleGroup
            categoryToggleGroup = categoryTabContainer.GetComponent<FilterToggleGroup>();
            if (categoryToggleGroup == null)
                categoryToggleGroup = categoryTabContainer.gameObject.AddComponent<FilterToggleGroup>();

            categoryToggleGroup.OnSelectionChanged += OnCategorySelectionChanged;

            // 清除已有标签
            foreach (Transform child in categoryTabContainer)
                Destroy(child.gameObject);
            categoryTabs.Clear();

            // "All" 标签
            CreateCategoryTab(null, "ALL");

            // 11 个 ProductCategory 按字母排序
            var categories = Enum.GetValues(typeof(ProductCategory))
                .Cast<ProductCategory>()
                .OrderBy(c => c.ToString());

            foreach (var cat in categories)
            {
                CreateCategoryTab(cat, cat.ToString().ToUpper());
            }

            // 默认选中 "All"
            categoryToggleGroup.SelectAll();
        }

        private void CreateCategoryTab(ProductCategory? category, string displayText)
        {
            var tabObj = Instantiate(categoryTabPrefab, categoryTabContainer);
            var toggleBtn = tabObj.GetComponent<FilterToggleButton>();

            if (toggleBtn == null)
            {
                Debug.LogWarning("[ItemCodexPanel] 类别标签预制体缺少 FilterToggleButton 组件");
                return;
            }

            // 使用 IFilterButton 接口初始化
            object filterValue = category.HasValue ? (object)category.Value : null;
            toggleBtn.Initialize(filterValue, displayText, (IFilterButton btn) =>
            {
                categoryToggleGroup.OnToggleClicked(btn);
            });

            categoryToggleGroup.RegisterToggle(toggleBtn);
            categoryTabs.Add(toggleBtn);
        }

        #endregion

        #region 排序

        private void BindSortOptions()
        {
            if (sortCodexAscButton != null)
                sortCodexAscButton.onClick.AddListener(() => SetSortMode(SortMode.CodexAsc));
            if (sortCodexDescButton != null)
                sortCodexDescButton.onClick.AddListener(() => SetSortMode(SortMode.CodexDesc));
            if (sortAlphaAscButton != null)
                sortAlphaAscButton.onClick.AddListener(() => SetSortMode(SortMode.AlphaAsc));
            if (sortAlphaDescButton != null)
                sortAlphaDescButton.onClick.AddListener(() => SetSortMode(SortMode.AlphaDesc));
            if (sortCostAscButton != null)
                sortCostAscButton.onClick.AddListener(() => SetSortMode(SortMode.CostAsc));
            if (sortCostDescButton != null)
                sortCostDescButton.onClick.AddListener(() => SetSortMode(SortMode.CostDesc));
        }

        private void ToggleSortDropdown()
        {
            if (sortDropdown != null)
                sortDropdown.SetActive(!sortDropdown.activeSelf);
        }

        private void SetSortMode(SortMode mode)
        {
            currentSort = mode;

            // 关闭下拉
            if (sortDropdown != null)
                sortDropdown.SetActive(false);

            RefreshAllEntries();
            RestoreSelection();
        }

        #endregion

        #region 搜索

        private void OnSearchChanged(string query)
        {
            searchQuery = query ?? "";
            RefreshAllEntries();
            RestoreSelection();
        }

        #endregion

        #region 类别选择

        private void OnCategorySelectionChanged(object filterValue)
        {
            if (filterValue == null)
            {
                selectedCategory = null;
            }
            else if (filterValue is ProductCategory cat)
            {
                selectedCategory = cat;
            }

            RefreshAllEntries();
            RestoreSelection();
        }

        #endregion

        #region 条目列表

        /// <summary>
        /// 刷新所有条目（清除并重建）
        /// </summary>
        private void RefreshAllEntries()
        {
            ClearEntries();

            var filtered = GetFilteredAndSortedShelves();

            foreach (var shelf in filtered)
            {
                var entryObj = Instantiate(entryPrefab, listContainer);
                var entry = entryObj.GetComponent<CodexShelfEntry>();

                if (entry == null) continue;

                var state = DetermineEntryState(shelf);
                entry.Initialize(shelf, state, OnEntryClicked);
                spawnedEntries.Add(entry);
            }
        }

        private void ClearEntries()
        {
            foreach (var entry in spawnedEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            spawnedEntries.Clear();
        }

        /// <summary>
        /// 获取经过过滤和排序的货架列表
        /// 过滤链：Category → Search → Sort
        /// </summary>
        private List<ShelfArchetype> GetFilteredAndSortedShelves()
        {
            IEnumerable<ShelfArchetype> result = allShelves;

            // 1. 类别过滤
            if (selectedCategory.HasValue)
                result = result.Where(s => s.category == selectedCategory.Value);

            // 2. 搜索过滤（大小写不敏感，子串匹配）
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string query = searchQuery.ToLowerInvariant();
                result = result.Where(s =>
                    s.displayName != null &&
                    s.displayName.ToLowerInvariant().Contains(query));
            }

            // 3. 排序
            result = currentSort switch
            {
                SortMode.CodexAsc => result.OrderBy(s => masterList.GetSortIndex(s)),
                SortMode.CodexDesc => result.OrderByDescending(s => masterList.GetSortIndex(s)),
                SortMode.AlphaAsc => result.OrderBy(s => s.displayName),
                SortMode.AlphaDesc => result.OrderByDescending(s => s.displayName),
                SortMode.CostAsc => result.OrderBy(s => s.buildCost),
                SortMode.CostDesc => result.OrderByDescending(s => s.buildCost),
                _ => result
            };

            return result.ToList();
        }

        /// <summary>
        /// 根据蓝图解锁状态和查看状态，确定条目应显示的状态
        /// </summary>
        private CodexShelfEntry.EntryState DetermineEntryState(ShelfArchetype shelf)
        {
            bool unlocked = BlueprintManager.Instance != null &&
                            BlueprintManager.Instance.HasShelfBlueprint(shelf.archetypeId);

            if (!unlocked)
                return CodexShelfEntry.EntryState.Locked;

            bool seen = CodexStateManager.Instance != null &&
                        CodexStateManager.Instance.IsShelfSeen(shelf.archetypeId);

            return seen ? CodexShelfEntry.EntryState.Unselected : CodexShelfEntry.EntryState.New;
        }

        /// <summary>
        /// 选中第一个可用（非Locked）的条目
        /// </summary>
        private void SelectFirstUnlockedEntry()
        {
            var firstEntry = spawnedEntries.FirstOrDefault(
                e => e.CurrentState != CodexShelfEntry.EntryState.Locked);

            if (firstEntry != null)
            {
                OnEntryClicked(firstEntry);
            }
            else
            {
                ShowEmptyState();
            }
        }

        /// <summary>
        /// 尝试恢复之前的选中条目
        /// </summary>
        private void RestoreSelection()
        {
            if (selectedEntry != null && selectedEntry.Shelf != null)
            {
                // 找到刷新后对应的条目
                var match = spawnedEntries.FirstOrDefault(
                    e => e.Shelf == selectedEntry.Shelf);

                if (match != null)
                {
                    // 保持选中
                    selectedEntry = match;
                    match.SetState(CodexShelfEntry.EntryState.Selected);
                    ShowDetailForShelf(match.Shelf);
                    return;
                }
            }

            // 之前的选中条目不在新列表中，选中第一个
            selectedEntry = null;
            SelectFirstUnlockedEntry();
        }

        #endregion

        #region 条目点击

        private void OnEntryClicked(CodexShelfEntry entry)
        {
            if (entry == null) return;
            if (entry.CurrentState == CodexShelfEntry.EntryState.Locked) return;

            // 取消之前的选中
            if (selectedEntry != null && selectedEntry != entry)
            {
                selectedEntry.SetState(CodexShelfEntry.EntryState.Unselected);
            }

            // 如果是 New 状态，标记为已查看
            if (entry.CurrentState == CodexShelfEntry.EntryState.New)
            {
                if (CodexStateManager.Instance != null)
                    CodexStateManager.Instance.MarkShelfAsSeen(entry.Shelf.archetypeId);
            }

            // 设为选中
            entry.SetState(CodexShelfEntry.EntryState.Selected);
            selectedEntry = entry;

            // 更新右侧详情
            ShowDetailForShelf(entry.Shelf);
        }

        #endregion

        #region 右侧详情面板

        private void ShowDetailForShelf(ShelfArchetype shelf)
        {
            if (shelf == null) return;

            if (detailEmptyState != null)
                detailEmptyState.SetActive(false);
            if (detailContent != null)
                detailContent.SetActive(true);

            // 图片（保持宽高比）
            if (shelfImage != null)
            {
                shelfImage.sprite = shelf.icon;
                shelfImage.preserveAspect = true;
            }

            // 基本信息（始终显示）
            if (shelfNameText != null)
                shelfNameText.text = shelf.displayName ?? "";

            if (brandText != null)
                brandText.text = shelf.brand != null ? shelf.brand.displayName : "";

            if (categoryText != null)
                categoryText.text = shelf.category.ToString();

            // Detail 页内容
            if (descriptionText != null)
                descriptionText.text = shelf.description ?? "";

            if (usageInstructionText != null)
                usageInstructionText.text = shelf.usageInstruction ?? "";

            // Stats 页内容（Level 1 基础数值）
            if (buildCostText != null)
                buildCostText.text = $"${shelf.buildCost}";

            if (maintenanceFeeText != null)
                maintenanceFeeText.text = $"${shelf.GetMaintenanceFee(1)}";

            if (sellPriceText != null)
                sellPriceText.text = $"${shelf.GetPrice(1)}";

            if (stockFeeText != null)
                stockFeeText.text = $"${shelf.GetStockFee(1)}";

            if (profitText != null)
                profitText.text = $"${shelf.GetProfit(1)}";

            if (stockText != null)
                stockText.text = shelf.GetStock(1).ToString();

            if (appealText != null)
                appealText.text = shelf.GetAppeal(1).ToString("F0");

            // 恢复当前页面显隐
            ApplyPageToggle();
        }

        private void ShowEmptyState()
        {
            if (detailContent != null)
                detailContent.SetActive(false);
            if (detailEmptyState != null)
                detailEmptyState.SetActive(true);
        }

        private void SwitchToDetailPage()
        {
            showingStats = false;
            ApplyPageToggle();
        }

        private void SwitchToStatsPage()
        {
            showingStats = true;
            ApplyPageToggle();
        }

        /// <summary>
        /// 根据 showingStats 切换 detailPage / statsPage 显隐和按钮视觉
        /// </summary>
        private void ApplyPageToggle()
        {
            if (detailPage != null)
                detailPage.SetActive(!showingStats);
            if (statsPage != null)
                statsPage.SetActive(showingStats);

            // 更新按钮颜色
            SetTabButtonColor(detailTabButton, !showingStats);
            SetTabButtonColor(statsTabButton, showingStats);
        }

        private void SetTabButtonColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = active ? activeTabColor : inactiveTabColor;
        }

        #endregion

        #region Category 横向滚动

        private void OnScrollCategoryLeft()
        {
            if (categoryScrollRect != null)
            {
                categoryScrollRect.horizontalNormalizedPosition =
                    Mathf.Clamp01(categoryScrollRect.horizontalNormalizedPosition - categoryScrollStep);
            }
        }

        private void OnScrollCategoryRight()
        {
            if (categoryScrollRect != null)
            {
                categoryScrollRect.horizontalNormalizedPosition =
                    Mathf.Clamp01(categoryScrollRect.horizontalNormalizedPosition + categoryScrollStep);
            }
        }

        #endregion

        #region Mark All As Seen

        private void OnMarkAllSeenClicked()
        {
            if (CodexStateManager.Instance != null)
            {
                CodexStateManager.Instance.MarkAllAsSeen();
                RefreshAllEntries();
                RestoreSelection();
            }
        }

        #endregion

        #region 事件回调

        /// <summary>
        /// 某条目被标记为已查看时的回调
        /// </summary>
        private void OnShelfSeenChanged(string shelfId)
        {
            // 如果面板不可见，无需更新UI
            if (panelRoot == null || !panelRoot.activeSelf) return;

            // 找到对应条目，如果它不是当前选中的，更新为Unselected
            var entry = spawnedEntries.FirstOrDefault(
                e => e.Shelf != null && e.Shelf.archetypeId == shelfId);

            if (entry != null && entry != selectedEntry &&
                entry.CurrentState == CodexShelfEntry.EntryState.New)
            {
                entry.SetState(CodexShelfEntry.EntryState.Unselected);
            }
        }

        /// <summary>
        /// 新蓝图解锁（尚未查看）时的回调
        /// </summary>
        private void OnNewShelfAvailable(string shelfId)
        {
            // 如果面板不可见，无需更新UI（下次Show时会刷新）
            if (panelRoot == null || !panelRoot.activeSelf) return;

            // 刷新条目列表
            RefreshAllEntries();
            RestoreSelection();
        }

        /// <summary>
        /// 蓝图解锁事件回调
        /// </summary>
        private void OnBlueprintUnlocked(ShelfArchetype shelf)
        {
            // CodexStateManager 会处理 New 状态广播
            // 面板可见时刷新条目列表
            if (panelRoot == null || !panelRoot.activeSelf) return;

            RefreshAllEntries();
            RestoreSelection();
        }

        #endregion
    }
}
