using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;
using PopLife.Data;
using PopLife.Manager;
using PopLife.Runtime;

namespace PopLife.UI.Restock
{
    /// <summary>
    /// 补货面板主控——单一状态源，所有选中/补货数量/过滤状态都在这里，
    /// Cell 和 CheckoutItem 只是无状态视图。
    /// </summary>
    public class RestockPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject fullScreenOverlay;
        [SerializeField] private CanvasGroup overlayCanvasGroup;
        [SerializeField] private Button closeButton;
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float panelOpenDelay = 0.05f;
        [SerializeField] private float slidePadding = 0f;   // 额外藏入屏幕上方的距离，面板边缘露出时可调

        [Header("Left: Grid")]
        [SerializeField] private ScrollRect gridScrollRect;
        [SerializeField] private Transform gridContentContainer;    // VerticalLayoutGroup 容器，存放多个 FloorSection
        [SerializeField] private GameObject floorSectionPrefab;     // 含 RestockFloorSection 组件
        [SerializeField] private GameObject shelfCellPrefab;        // 含 RestockShelfCell 组件

        [Header("Left: Filter")]
        [SerializeField] private Button filterToggleButton;
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private GameObject categoryScrollViewRoot;
        [SerializeField] private Transform categoryButtonContainer;  // Scroll View Content
        [SerializeField] private GameObject categoryButtonPrefab;

        [Header("Left: Bulk Buttons")]
        [SerializeField] private Button selectAllButton;
        [SerializeField] private Button deselectAllButton;
        [SerializeField] private Button toMaxStockButton;
        [SerializeField] private Button toHalfStockButton;
        [SerializeField] private Button toZeroStockButton;     // 批量把所有已选 shelf 的待补货量清零

        [Header("Right: Checkout")]
        [SerializeField] private ScrollRect checkoutScrollRect;
        [SerializeField] private Transform checkoutItemContainer;
        [SerializeField] private GameObject checkoutItemPrefab;
        [SerializeField] private TMP_Text totalCostText;
        [SerializeField] private TMP_Text moneyFormulaText;         // "$56000 - $500 = $55500"
        [SerializeField] private Button restockButton;
        [SerializeField] private Button skipRestockButton;          // 跳过补货：关闭面板并允许当日开店

        [Header("Not Restocked Prompt")]
        [SerializeField] private NotRestockedPrompt notRestockedPrompt;

        [Header("Success Prompt")]
        [SerializeField] private RestockSuccessPrompt successPrompt;

        // ================================================================
        //  单一状态源
        // ================================================================
        private readonly HashSet<string> selectedShelfIds = new();
        private readonly Dictionary<string, int> restockAmounts = new();
        private string searchText = "";
        private ProductCategory? selectedCategory = null;  // null = All
        // 当日玩家已经"操作过"任一 cell（选中/改数量），未补货提示从此不再出现，OnBuildPhaseStart 重置
        private bool notReadyHintDismissedThisSession;

        // 运行时缓存
        private readonly Dictionary<string, RestockShelfCell> cellsByShelfId = new();
        private readonly Dictionary<int, RestockFloorSection> sectionsByFloor = new();
        private readonly Dictionary<string, RestockCheckoutItem> checkoutItemsByShelfId = new();
        private readonly Dictionary<string, (int level, int floorLevel)> shelfStateCache = new();
        private HashSet<string> prevShelfIds = new();

        // Filter 组件
        private FilterToggleGroup categoryToggleGroup;

        // 动画
        private RectTransform panelRectTransform;
        private float slideDistance;
        private Tween slideTween;
        private Coroutine openSlideCoroutine;
        private bool isOpen;

        // ================================================================
        //  生命周期
        // ================================================================

        private void Awake()
        {
            if (panelRoot != null)
            {
                panelRectTransform = panelRoot.GetComponent<RectTransform>();
                slideDistance = ComputeSlideDistance();
                if (panelRectTransform != null)
                {
                    // 初始藏在屏幕上方（Y 为正 = 向上）
                    panelRectTransform.anchoredPosition = new Vector2(
                        panelRectTransform.anchoredPosition.x, slideDistance);
                }
                panelRoot.SetActive(false);
            }
            SetOverlayVisible(false);

            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
            if (selectAllButton != null) selectAllButton.onClick.AddListener(OnSelectAllClicked);
            if (deselectAllButton != null) deselectAllButton.onClick.AddListener(OnDeselectAllClicked);
            if (toMaxStockButton != null) toMaxStockButton.onClick.AddListener(OnToMaxStockClicked);
            if (toHalfStockButton != null) toHalfStockButton.onClick.AddListener(OnToHalfStockClicked);
            if (toZeroStockButton != null) toZeroStockButton.onClick.AddListener(OnToZeroStockClicked);
            if (restockButton != null) restockButton.onClick.AddListener(OnRestockClicked);
            if (skipRestockButton != null) skipRestockButton.onClick.AddListener(OnSkipRestockClicked);
            if (filterToggleButton != null) filterToggleButton.onClick.AddListener(ToggleCategoryButtons);

            if (searchInput != null)
            {
                searchInput.onValueChanged.AddListener(OnSearchChanged);
            }

            SetCategoryButtonsVisible(false);

            // Category filter buttons
            if (categoryButtonContainer != null && categoryButtonPrefab != null)
            {
                SetupCategoryFilterButtons();
            }
        }

        private void OnEnable()
        {
            ConstructionManager.OnBuildingPlacedOrDestroyed += OnConstructionChanged;
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnMoneyChanged += OnMoneyChanged;
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnCalendarDataChanged += HandleCalendarDataChanged;
        }

        private void OnDisable()
        {
            ConstructionManager.OnBuildingPlacedOrDestroyed -= OnConstructionChanged;
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnMoneyChanged -= OnMoneyChanged;
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnCalendarDataChanged -= HandleCalendarDataChanged;
        }

        // Buffer 激活/到期会改变可补货品类列表；面板已打开时立即重建 grid
        // 包一层无参 handler 以匹配 Action 委托签名（RebuildGrid 带 bool 参数）
        private void HandleCalendarDataChanged()
        {
            if (isOpen)
                RebuildGrid(scrollCheckoutToBottom: false);
        }

        // 面板显示时屏幕居中（anchor/pivot=0.5,0.5），藏身时向上平移到整个面板完全超出屏幕上沿。
        // 所需距离 = Canvas 一半 + 面板一半 + padding。任一高度读不到时回退到保守的 1080。
        private float ComputeSlideDistance()
        {
            float panelHalf = 0f;
            if (panelRectTransform != null)
                panelHalf = panelRectTransform.rect.height * 0.5f;

            float canvasHalf = 540f; // 兜底：1080 / 2
            if (panelRectTransform != null)
            {
                var canvas = panelRectTransform.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var canvasRect = canvas.transform as RectTransform;
                    if (canvasRect != null && canvasRect.rect.height > 0.01f)
                        canvasHalf = canvasRect.rect.height * 0.5f;
                }
            }

            // 面板高度还没布局出来时用 canvasHalf 兜底，保证"至少藏到屏幕上方半个 canvas"
            if (panelHalf <= 0.01f) panelHalf = canvasHalf;

            return canvasHalf + panelHalf + slidePadding;
        }

        private void SetOverlayVisible(bool visible)
        {
            if (fullScreenOverlay != null)
                fullScreenOverlay.SetActive(visible);

            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = visible ? 1f : 0f;
                overlayCanvasGroup.interactable = visible;
                overlayCanvasGroup.blocksRaycasts = visible;
            }
        }

        private IEnumerator PlayOpenSlideAfterDelay()
        {
            if (panelRectTransform != null)
            {
                panelRectTransform.anchoredPosition = new Vector2(
                    panelRectTransform.anchoredPosition.x, slideDistance);
            }

            if (panelOpenDelay > 0f)
                yield return new WaitForSecondsRealtime(panelOpenDelay);

            openSlideCoroutine = null;
            slideTween.Stop();
            slideTween = Tween.Custom(this, slideDistance, 0f, slideDuration,
                (target, val) =>
                {
                    if (target.panelRectTransform != null)
                        target.panelRectTransform.anchoredPosition = new Vector2(
                            target.panelRectTransform.anchoredPosition.x, val);
                }, Ease.OutCubic, useUnscaledTime: true);
        }

        // ================================================================
        //  打开 / 关闭
        // ================================================================

        /// <summary>
        /// 供外部（Restock 按钮 / RestockManager.RequestOpenWithoutRestock）调用。
        /// </summary>
        public void Open(bool showNotReadyPrompt)
        {
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase != GamePhase.BuildPhase)
            {
                Debug.LogWarning("[RestockPanel] 仅允许在 BuildPhase 打开");
                return;
            }

            CloseShelfListPanelIfOpen();

            if (isOpen)
            {
                // 已经打开的话，只更新提示状态和刷新列表
                if (successPrompt != null) successPrompt.Hide();
                UpdateNotRestockedPrompt(showNotReadyPrompt);
                RefreshCloseButtonInteractable();
                RebuildGrid(scrollCheckoutToBottom: false);
                return;
            }

            isOpen = true;
            SetOverlayVisible(true);
            if (panelRoot != null) panelRoot.SetActive(true);
            if (successPrompt != null) successPrompt.Hide();
            SetCategoryButtonsVisible(false);
            RefreshCloseButtonInteractable();

            slideTween.Stop();
            if (openSlideCoroutine != null) StopCoroutine(openSlideCoroutine);
            openSlideCoroutine = StartCoroutine(PlayOpenSlideAfterDelay());

            UpdateNotRestockedPrompt(showNotReadyPrompt);
            RebuildGrid(scrollCheckoutToBottom: true); // 首次打开时右侧 checkout 滚到底
            StartCoroutine(ScrollGridToBottomNextFrame()); // 首次打开时左侧 grid 滚到底（最底 = 1F 可见）
        }

        public void Open() => Open(showNotReadyPrompt: false);

        public void ClosePanel()
        {
            if (!isOpen) return;
            isOpen = false;

            if (notRestockedPrompt != null) notRestockedPrompt.Hide();
            if (successPrompt != null) successPrompt.Hide();

            if (openSlideCoroutine != null)
            {
                StopCoroutine(openSlideCoroutine);
                openSlideCoroutine = null;
            }
            slideTween.Stop();
            // 从当前 Y 滑回屏幕上方（+slideDistance）
            slideTween = Tween.Custom(this,
                panelRectTransform != null ? panelRectTransform.anchoredPosition.y : 0f,
                slideDistance, slideDuration,
                (target, val) =>
                {
                    if (target.panelRectTransform != null)
                        target.panelRectTransform.anchoredPosition = new Vector2(
                            target.panelRectTransform.anchoredPosition.x, val);
                }, Ease.InCubic, useUnscaledTime: true)
                .OnComplete(this, target =>
                {
                    if (target.panelRoot != null) target.panelRoot.SetActive(false);
                    target.SetOverlayVisible(false);
                });
            // 关闭不清理 selectedShelfIds / restockAmounts / filter —— 保留给下次打开
        }

        public bool IsOpen() => isOpen;

        private static void CloseShelfListPanelIfOpen()
        {
            var shelfListPanel = FindFirstObjectByType<PopLife.UI.ShelfListPanel>();
            if (shelfListPanel != null && shelfListPanel.IsOpen())
            {
                shelfListPanel.ClosePanel();
            }
        }

        // ================================================================
        //  Filter
        // ================================================================

        private void ToggleCategoryButtons()
        {
            bool currentlyVisible = categoryScrollViewRoot != null && categoryScrollViewRoot.activeSelf;
            SetCategoryButtonsVisible(!currentlyVisible);
        }

        private void SetCategoryButtonsVisible(bool visible)
        {
            if (categoryScrollViewRoot != null)
                categoryScrollViewRoot.SetActive(visible);
        }

        private void SetupCategoryFilterButtons()
        {
            categoryToggleGroup = categoryButtonContainer.GetComponent<FilterToggleGroup>();
            if (categoryToggleGroup == null)
                categoryToggleGroup = categoryButtonContainer.gameObject.AddComponent<FilterToggleGroup>();

            // All 按钮
            CreateFilterButton(null, "All");
            var cats = System.Enum.GetValues(typeof(ProductCategory))
                .Cast<ProductCategory>().OrderBy(c => c.ToString());
            foreach (var c in cats) CreateFilterButton(c, c.ToString());

            categoryToggleGroup.OnSelectionChanged += OnCategoryChanged;
        }

        private void CreateFilterButton(ProductCategory? cat, string label)
        {
            if (categoryButtonPrefab == null || categoryButtonContainer == null) return;
            var go = Instantiate(categoryButtonPrefab, categoryButtonContainer);
            var btn = go.GetComponent<FilterToggleButton>();
            if (btn == null) return;
            // 装箱 ProductCategory? 到 object：null 表示 All
            object fv = cat.HasValue ? (object)cat.Value : null;
            btn.Initialize(fv, label, (clicked) => categoryToggleGroup.OnToggleClicked(clicked));
            categoryToggleGroup.RegisterToggle(btn);
        }

        private void OnCategoryChanged(object value)
        {
            // value 可能为 null（All）或装箱的 ProductCategory
            if (value is ProductCategory pc) selectedCategory = pc;
            else selectedCategory = null;
            RebuildGrid(scrollCheckoutToBottom: false);
        }

        private void OnSearchChanged(string text)
        {
            searchText = text ?? "";
            RebuildGrid(scrollCheckoutToBottom: false);
        }

        // ================================================================
        //  Grid 构建 & 增量更新
        // ================================================================

        private void OnConstructionChanged()
        {
            if (!isOpen) return;
            RebuildGrid(scrollCheckoutToBottom: false);
        }

        /// <summary>
        /// 全量重建：按楼层分组、排序、应用 filter/search、重用已有 cell、销毁失效的、
        /// 检测 upgrade/move/sell，同步 checkout。
        /// </summary>
        private void RebuildGrid(bool scrollCheckoutToBottom)
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return;

            // 1. 收集当前场上所有 shelf
            var currentShelves = wg.AllShelves().Where(s => s != null).ToList();
            var currentIds = new HashSet<string>(currentShelves.Select(s => s.instanceId));

            // 2. 处理消失的 shelf（被卖掉）：从选中/数量/checkout/grid 全部移除
            var removedIds = prevShelfIds.Where(id => !currentIds.Contains(id)).ToList();
            foreach (var id in removedIds)
            {
                selectedShelfIds.Remove(id);
                restockAmounts.Remove(id);
                shelfStateCache.Remove(id);
                RemoveCheckoutItem(id);
                RemoveCell(id);
            }

            // 3. 对仍存在的 shelf 检测 upgrade：level 变了 → 钳制 pending 到新 room
            foreach (var shelf in currentShelves)
            {
                if (shelfStateCache.TryGetValue(shelf.instanceId, out var prev))
                {
                    if (prev.level != shelf.currentLevel)
                    {
                        int room = Mathf.Max(0, shelf.maxStock - shelf.currentStock);
                        if (restockAmounts.TryGetValue(shelf.instanceId, out var amt))
                            restockAmounts[shelf.instanceId] = Mathf.Clamp(amt, 0, room);
                    }
                }
                // move 不用特殊处理，完整重排序会把 cell 挪到新楼层的 container 下
            }

            // 4. 按楼层分组并排序
            var floorToShelves = new Dictionary<int, List<ShelfInstance>>();
            foreach (var shelf in currentShelves)
            {
                int floorLevel = wg.GetFloorLevel(shelf.hostFloorTileInstanceId) ?? 0;
                if (!floorToShelves.TryGetValue(floorLevel, out var list))
                {
                    list = new List<ShelfInstance>();
                    floorToShelves[floorLevel] = list;
                }
                list.Add(shelf);
            }

            // 楼层内排序：floor-tile 世界锚点 X 升序 → interior gridPosition.x → gridPosition.y
            foreach (var kv in floorToShelves)
            {
                kv.Value.Sort((a, b) =>
                {
                    var tileA = wg.GetFloorTileById(a.hostFloorTileInstanceId);
                    var tileB = wg.GetFloorTileById(b.hostFloorTileInstanceId);
                    float ax = tileA != null ? tileA.GetWorldAnchorPosition().x : 0f;
                    float bx = tileB != null ? tileB.GetWorldAnchorPosition().x : 0f;
                    int cx = ax.CompareTo(bx);
                    if (cx != 0) return cx;
                    cx = a.gridPosition.x.CompareTo(b.gridPosition.x);
                    if (cx != 0) return cx;
                    return a.gridPosition.y.CompareTo(b.gridPosition.y);
                });
            }

            // 5. 应用 filter/search
            var visibleByFloor = new Dictionary<int, List<ShelfInstance>>();
            foreach (var kv in floorToShelves)
            {
                var visible = kv.Value.Where(ShouldShowShelf).ToList();
                if (visible.Count > 0)
                    visibleByFloor[kv.Key] = visible;
            }

            // 6. 管理 FloorSection 生命周期：
            //    section 的存活条件是"这一层有任何 shelf 存在于世界中"——与 filter 无关。
            //    filter 只决定 section/cell 的可见性（SetActive），绝不删除 cell，否则 cellsByShelfId
            //    里的引用会变成已销毁对象。只有当某层所有 shelf 都被卖光时才销毁 section。
            var allFloorsInWorld = new HashSet<int>(floorToShelves.Keys);

            foreach (var floor in sectionsByFloor.Keys.ToList())
            {
                if (!allFloorsInWorld.Contains(floor))
                {
                    Destroy(sectionsByFloor[floor].gameObject);
                    sectionsByFloor.Remove(floor);
                }
            }

            // 楼层按"真实建筑布局"排：高层在顶、地下层在底（降序 + SiblingIndex=0 在 VerticalLayout 顶端）
            var sortedFloors = allFloorsInWorld.OrderByDescending(f => f).ToList();
            int siblingIndex = 0;
            foreach (var floor in sortedFloors)
            {
                if (!sectionsByFloor.TryGetValue(floor, out var section))
                {
                    var go = Instantiate(floorSectionPrefab, gridContentContainer);
                    section = go.GetComponent<RestockFloorSection>();
                    sectionsByFloor[floor] = section;
                }
                section.SetHeader($"FLOOR {floor + 1} STOCK");
                section.transform.SetSiblingIndex(siblingIndex++);

                // 当前 filter 下该层无可见 cell → 隐藏整个 section（含 header），但保留对象与其 cell
                bool hasVisibleOnFloor = visibleByFloor.ContainsKey(floor) && visibleByFloor[floor].Count > 0;
                if (section.gameObject.activeSelf != hasVisibleOnFloor)
                    section.gameObject.SetActive(hasVisibleOnFloor);
            }

            // 7. 对世界里所有 shelf 确保 cell 存在、父节点正确、内容最新；
            //    是否可见由 filter/search 决定（SetActive），不 Destroy。
            foreach (var kv in floorToShelves)
            {
                int floor = kv.Key;
                var section = sectionsByFloor[floor];
                var visibleSet = visibleByFloor.TryGetValue(floor, out var vis)
                    ? new HashSet<ShelfInstance>(vis)
                    : null;

                int cellIndex = 0;
                foreach (var shelf in kv.Value) // 已按规则排过序
                {
                    if (!cellsByShelfId.TryGetValue(shelf.instanceId, out var cell))
                    {
                        var go = Instantiate(shelfCellPrefab, section.CellContainer);
                        cell = go.GetComponent<RestockShelfCell>();
                        WireCellEvents(cell);
                        cellsByShelfId[shelf.instanceId] = cell;
                    }
                    else if (cell != null && cell.transform.parent != section.CellContainer)
                    {
                        // 跨楼层移动：把 cell 挪到新楼层的 section 下
                        cell.transform.SetParent(section.CellContainer, false);
                    }

                    bool isVisible = visibleSet != null && visibleSet.Contains(shelf);
                    if (cell.gameObject.activeSelf != isVisible)
                        cell.gameObject.SetActive(isVisible);

                    // 可见的 cell 按排序决定 sibling index；隐藏的也参与顺序以便恢复时位置稳定
                    cell.transform.SetSiblingIndex(cellIndex++);

                    // 即使被 filter 隐藏也刷新一下 ViewModel——选中状态/数量可能在隐藏期间由其他路径改过
                    cell.Render(BuildCellViewModel(shelf));
                }
            }

            // 9. 更新缓存
            shelfStateCache.Clear();
            foreach (var shelf in currentShelves)
            {
                int fl = wg.GetFloorLevel(shelf.hostFloorTileInstanceId) ?? 0;
                shelfStateCache[shelf.instanceId] = (shelf.currentLevel, fl);
            }
            prevShelfIds = currentIds;

            // 10. 刷新 checkout 面板
            RebuildCheckout(scrollCheckoutToBottom);
            RefreshRestockButtonAndTotals();
        }

        private bool ShouldShowShelf(ShelfInstance shelf)
        {
            if (shelf == null || shelf.archetype == null) return false;
            var sa = shelf.archetype as ShelfArchetype;
            if (sa == null) return false;

            // 全局禁补品类（Robot Strike 等 Buffer）：直接从列表移除，玩家无法选择
            if (GlobalModifierManager.Instance != null
                && GlobalModifierManager.Instance.IsRestockBlocked(sa.category))
                return false;

            if (selectedCategory.HasValue && sa.category != selectedCategory.Value) return false;

            if (!string.IsNullOrEmpty(searchText))
            {
                var dn = sa.displayName ?? sa.name ?? "";
                if (dn.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            return true;
        }

        private void RemoveCell(string id)
        {
            if (cellsByShelfId.TryGetValue(id, out var cell))
            {
                if (cell != null) Destroy(cell.gameObject);
                cellsByShelfId.Remove(id);
            }
        }

        private void WireCellEvents(RestockShelfCell cell)
        {
            cell.OnCellBodyClicked += HandleCellBodyClicked;
            cell.OnSelectCheckboxClicked += HandleSelectCheckboxClicked;
            cell.OnAmountDeltaRequested += HandleAmountDelta;
            cell.OnAmountSetRequested += HandleAmountSet;
        }

        // ================================================================
        //  View-Model 构建
        // ================================================================

        private CellViewModel BuildCellViewModel(ShelfInstance shelf)
        {
            var sa = shelf.archetype as ShelfArchetype;
            int amount = restockAmounts.TryGetValue(shelf.instanceId, out var a) ? a : 0;
            int room = Mathf.Max(0, shelf.maxStock - shelf.currentStock);
            return new CellViewModel(
                instanceId: shelf.instanceId,
                icon: GetShelfDisplaySprite(sa),
                shelfName: sa != null ? (string.IsNullOrEmpty(sa.displayName) ? sa.name : sa.displayName) : shelf.name,
                currentStock: shelf.currentStock,
                maxStock: shelf.maxStock,
                restockAmount: amount,
                isSelected: selectedShelfIds.Contains(shelf.instanceId),
                canDecrement: amount > 0,
                canIncrement: amount < room,
                stockIsLow: shelf.currentStock == 0
            );
        }

        /// <summary>
        /// 直接从 archetype 的 prefab 上读取 SpriteRenderer.sprite——绕过 archetype.icon 字段，
        /// 让补货面板永远显示与场上货架一致的实际美术。
        /// 缓存到 sa.archetypeId 减少每次 GetComponent 开销。
        /// </summary>
        private static readonly Dictionary<string, Sprite> spriteByArchetypeId = new();
        private static Sprite GetShelfDisplaySprite(ShelfArchetype sa)
        {
            if (sa == null || sa.prefab == null) return null;
            string key = sa.archetypeId ?? sa.name;
            if (spriteByArchetypeId.TryGetValue(key, out var cached)) return cached;
            var sr = sa.prefab.GetComponent<SpriteRenderer>();
            var sprite = sr != null ? sr.sprite : null;
            spriteByArchetypeId[key] = sprite;
            return sprite;
        }

        private CheckoutItemViewModel BuildCheckoutViewModel(ShelfInstance shelf)
        {
            var sa = shelf.archetype as ShelfArchetype;
            int amount = restockAmounts.TryGetValue(shelf.instanceId, out var a) ? a : 0;
            int room = Mathf.Max(0, shelf.maxStock - shelf.currentStock);
            int fee = shelf.GetStockFee();
            return new CheckoutItemViewModel(
                instanceId: shelf.instanceId,
                name: sa != null ? (string.IsNullOrEmpty(sa.displayName) ? sa.name : sa.displayName) : shelf.name,
                restockAmount: amount,
                stockFee: fee,
                lineTotal: amount * fee,
                canDecrement: amount > 0,
                canIncrement: amount < room
            );
        }

        // ================================================================
        //  Cell/Checkout 事件处理（Panel 单一状态源）
        // ================================================================

        private void HandleCellBodyClicked(string id) => ToggleSelection(id);
        private void HandleSelectCheckboxClicked(string id) => ToggleSelection(id);

        private void ToggleSelection(string id)
        {
            DismissNotReadyHintIfShown();

            if (selectedShelfIds.Contains(id)) selectedShelfIds.Remove(id);
            else selectedShelfIds.Add(id);

            RefreshCellIfExists(id);
            SyncCheckoutItemForShelf(id);
            RefreshRestockButtonAndTotals();
        }

        private void HandleAmountDelta(string id, int delta) => ApplyAmountChange(id, GetAmount(id) + delta);
        private void HandleAmountSet(string id, int value) => ApplyAmountChange(id, value);

        private void ApplyAmountChange(string id, int desired)
        {
            DismissNotReadyHintIfShown();

            var shelf = FindShelf(id);
            if (shelf == null) return;
            int room = Mathf.Max(0, shelf.maxStock - shelf.currentStock);
            int clamped = Mathf.Clamp(desired, 0, room);

            // 强制选中
            selectedShelfIds.Add(id);

            if (clamped == 0) restockAmounts.Remove(id);
            else restockAmounts[id] = clamped;

            RefreshCellIfExists(id);
            SyncCheckoutItemForShelf(id);
            RefreshRestockButtonAndTotals();
        }

        private int GetAmount(string id) => restockAmounts.TryGetValue(id, out var a) ? a : 0;

        private ShelfInstance FindShelf(string id)
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return null;
            foreach (var s in wg.AllShelves())
                if (s != null && s.instanceId == id) return s;
            return null;
        }

        private void RefreshCellIfExists(string id)
        {
            if (cellsByShelfId.TryGetValue(id, out var cell) && cell != null)
            {
                var shelf = FindShelf(id);
                if (shelf != null) cell.Render(BuildCellViewModel(shelf));
            }
        }

        // ================================================================
        //  Checkout 列表管理（仅按 selectedShelfIds，不受 filter 影响）
        // ================================================================

        private void RebuildCheckout(bool scrollToBottom)
        {
            // 销毁不再选中的
            var toRemove = checkoutItemsByShelfId.Keys.Where(id => !selectedShelfIds.Contains(id)).ToList();
            foreach (var id in toRemove) RemoveCheckoutItem(id);

            // 新增选中但还没 item 的
            bool anyAdded = false;
            foreach (var id in selectedShelfIds)
            {
                if (!checkoutItemsByShelfId.ContainsKey(id))
                {
                    var shelf = FindShelf(id);
                    if (shelf == null) continue;
                    CreateCheckoutItem(shelf);
                    anyAdded = true;
                }
                else
                {
                    SyncCheckoutItemForShelf(id);
                }
            }

            if ((scrollToBottom || anyAdded) && checkoutScrollRect != null)
            {
                StartCoroutine(ScrollToBottomNextFrame());
            }
        }

        private void CreateCheckoutItem(ShelfInstance shelf)
        {
            if (checkoutItemPrefab == null || checkoutItemContainer == null) return;
            var go = Instantiate(checkoutItemPrefab, checkoutItemContainer);
            var item = go.GetComponent<RestockCheckoutItem>();
            if (item != null)
            {
                item.OnAmountDeltaRequested += HandleAmountDelta;
                item.Render(BuildCheckoutViewModel(shelf));
                checkoutItemsByShelfId[shelf.instanceId] = item;
            }
        }

        private void RemoveCheckoutItem(string id)
        {
            if (checkoutItemsByShelfId.TryGetValue(id, out var item))
            {
                if (item != null) Destroy(item.gameObject);
                checkoutItemsByShelfId.Remove(id);
            }
        }

        /// <summary>仅更新某个 shelf 的 checkout item（不重建也不滚动）。</summary>
        private void SyncCheckoutItemForShelf(string id)
        {
            if (!selectedShelfIds.Contains(id))
            {
                // 选中状态变更：从 checkout 移除
                if (checkoutItemsByShelfId.ContainsKey(id)) RemoveCheckoutItem(id);
                return;
            }
            var shelf = FindShelf(id);
            if (shelf == null) return;
            if (!checkoutItemsByShelfId.TryGetValue(id, out var item))
            {
                CreateCheckoutItem(shelf);
                if (checkoutScrollRect != null) StartCoroutine(ScrollToBottomNextFrame());
            }
            else
            {
                item.Render(BuildCheckoutViewModel(shelf));
            }
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            if (checkoutItemContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
            yield return null;
            if (checkoutScrollRect != null)
                checkoutScrollRect.verticalNormalizedPosition = 0f;
        }

        // 打开面板时让左侧 grid 默认滚到底：配合降序排序（顶=高层、底=1F），用户首屏看到的是最低楼层
        private IEnumerator ScrollGridToBottomNextFrame()
        {
            if (gridContentContainer is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
            yield return null;
            if (gridScrollRect != null)
                gridScrollRect.verticalNormalizedPosition = 0f;
        }

        // ================================================================
        //  总价 & Restock 按钮启用条件
        // ================================================================

        private int ComputeTotalCost()
        {
            int total = 0;
            foreach (var id in selectedShelfIds)
            {
                if (!restockAmounts.TryGetValue(id, out var amount) || amount <= 0) continue;
                var shelf = FindShelf(id);
                if (shelf == null) continue;
                total += amount * shelf.GetStockFee();
            }
            return total;
        }

        private void RefreshRestockButtonAndTotals()
        {
            int total = ComputeTotalCost();
            int money = ResourceManager.Instance != null ? ResourceManager.Instance.GetMoney() : 0;

            if (totalCostText != null)
                totalCostText.text = $"Total: ${total}";

            if (moneyFormulaText != null)
                moneyFormulaText.text = $"${money} - ${total} = ${money - total}";

            if (restockButton != null)
            {
                restockButton.interactable =
                    selectedShelfIds.Count > 0 &&
                    total > 0 &&
                    money >= total;
            }
        }

        private void OnMoneyChanged(int _) => RefreshRestockButtonAndTotals();

        // ================================================================
        //  底部 bulk 按钮
        // ================================================================

        private void OnSelectAllClicked()
        {
            // 仅选中"用户当前真正看得到的" cell——用 activeInHierarchy 才能同时考虑 section 被隐藏的情况
            foreach (var kv in cellsByShelfId)
            {
                if (kv.Value != null && kv.Value.gameObject.activeInHierarchy)
                    selectedShelfIds.Add(kv.Key);
            }
            // 重建 checkout（会导致新增，触发滚底）
            RebuildCheckout(scrollToBottom: true);
            RefreshAllCells();
            RefreshRestockButtonAndTotals();
        }

        private void OnDeselectAllClicked()
        {
            // 只清选中，不清 amount
            selectedShelfIds.Clear();
            RebuildCheckout(scrollToBottom: false);
            RefreshAllCells();
            RefreshRestockButtonAndTotals();
        }

        private void OnToMaxStockClicked()
        {
            foreach (var id in selectedShelfIds.ToList())
            {
                var shelf = FindShelf(id);
                if (shelf == null) continue;
                int room = Mathf.Max(0, shelf.maxStock - shelf.currentStock);
                if (room <= 0) restockAmounts.Remove(id);
                else restockAmounts[id] = room;
            }
            RefreshAllCells();
            RebuildCheckout(scrollToBottom: false);
            RefreshRestockButtonAndTotals();
        }

        private void OnToHalfStockClicked()
        {
            foreach (var id in selectedShelfIds.ToList())
            {
                var shelf = FindShelf(id);
                if (shelf == null) continue;
                int target = Mathf.CeilToInt(shelf.maxStock / 2f);
                int amount = Mathf.Max(0, target - shelf.currentStock);
                int room = Mathf.Max(0, shelf.maxStock - shelf.currentStock);
                amount = Mathf.Clamp(amount, 0, room);
                if (amount <= 0) restockAmounts.Remove(id);
                else restockAmounts[id] = amount;
            }
            RefreshAllCells();
            RebuildCheckout(scrollToBottom: false);
            RefreshRestockButtonAndTotals();
        }

        private void OnToZeroStockClicked()
        {
            // 不动 selection，仅清空选中行的待补货量
            foreach (var id in selectedShelfIds.ToList())
                restockAmounts.Remove(id);
            RefreshAllCells();
            RebuildCheckout(scrollToBottom: false);
            RefreshRestockButtonAndTotals();
        }

        private void RefreshAllCells()
        {
            foreach (var kv in cellsByShelfId)
            {
                var shelf = FindShelf(kv.Key);
                if (shelf != null && kv.Value != null)
                    kv.Value.Render(BuildCellViewModel(shelf));
            }
        }

        // ================================================================
        //  Restock 交易
        // ================================================================

        private void OnRestockClicked()
        {
            int total = ComputeTotalCost();
            if (total <= 0 || selectedShelfIds.Count == 0) return;
            // 注：不再检查 CanAfford。负债期间允许玩家继续补货以营业还债，
            // 否则负债玩家会陷入死锁（无库存无法营业，无法营业无法还债）。
            // 累积负债的风险由"4 次还债失败 = Game Over"机制兜底。
            if (ResourceManager.Instance == null) return;

            // 1. 扣钱（一次性）— money 可能因此变更负，这是预期行为
            ResourceManager.Instance.SpendMoney(total);

            // 2. 逐个 shelf 补货
            foreach (var id in selectedShelfIds)
            {
                if (!restockAmounts.TryGetValue(id, out var amount) || amount <= 0) continue;
                var shelf = FindShelf(id);
                if (shelf == null) continue;
                shelf.RestockByAmount(amount);
            }

            // 3. 标记 ready
            if (RestockManager.Instance != null) RestockManager.Instance.MarkRestocked();
            RefreshCloseButtonInteractable();

            // 4. 清理已消耗的状态（保留 filter/search）
            selectedShelfIds.Clear();
            restockAmounts.Clear();

            // Show success prompt first; the prompt click closes the panel.
            if (notRestockedPrompt != null) notRestockedPrompt.Hide();
            RebuildGrid(scrollCheckoutToBottom: false);

            if (successPrompt != null)
            {
                successPrompt.Show(OnSuccessPromptDismissed);
            }
            else
            {
                Debug.LogWarning("[RestockPanel] successPrompt is not assigned; closing after successful restock.");
                ClosePanel();
            }
        }

        private void OnSuccessPromptDismissed()
        {
            // 首次（Restock 或 Skip 任一）补货决定的 success prompt 关闭时 → 触发 Midori/4 教程对话
            // 后续点击不再重复触发：TutorialEventBus 内部 HashSet 自带 dedup
            TutorialEventBus.RaiseMarker(TutorialMarker.FirstRestockDecisionMade);

            if (successPrompt != null) successPrompt.Hide();
            ClosePanel();
        }

        private void OnSkipRestockClicked()
        {
            // 玩家选择当日不补货：标记后弹出复用的 success prompt（覆盖文案），点击关闭面板
            if (RestockManager.Instance != null)
                RestockManager.Instance.MarkSkippedRestock();
            RefreshCloseButtonInteractable();

            if (notRestockedPrompt != null) notRestockedPrompt.Hide();

            if (successPrompt != null)
                successPrompt.ShowSkip(OnSuccessPromptDismissed);
            else
                ClosePanel();
        }

        // 首次打开 RestockPanel（玩家此存档从未做过 restock/skip 决策）时禁用关闭键，
        // 强制玩家通过 Restock 或 Skip Restock 完成首次决策。决策后永久启用。
        private void RefreshCloseButtonInteractable()
        {
            if (closeButton == null) return;
            bool hasMadeDecision = RestockManager.Instance == null
                || RestockManager.Instance.HasMadeFirstRestockDecisionEver;
            closeButton.interactable = hasMadeDecision;
        }

        // ================================================================
        //  新一天重置 & 提示覆盖层
        // ================================================================

        private void OnBuildPhaseStart()
        {
            // 新一天：清理所有 UI 状态
            selectedShelfIds.Clear();
            restockAmounts.Clear();
            searchText = "";
            selectedCategory = null;
            if (searchInput != null) searchInput.SetTextWithoutNotify("");
            if (categoryToggleGroup != null) categoryToggleGroup.SelectAll();
            SetCategoryButtonsVisible(false);

            // 新的一天：未补货提示重新允许显示
            notReadyHintDismissedThisSession = false;

            if (isOpen) RebuildGrid(scrollCheckoutToBottom: false);
        }

        private void UpdateNotRestockedPrompt(bool show)
        {
            if (notRestockedPrompt == null) return;
            // 玩家本日已操作过任意 cell → 即便 caller 请求显示也忽略
            if (show && !notReadyHintDismissedThisSession) notRestockedPrompt.Show();
            else notRestockedPrompt.Hide();
        }

        private void DismissNotReadyHintIfShown()
        {
            if (notReadyHintDismissedThisSession) return;
            notReadyHintDismissedThisSession = true;
            if (notRestockedPrompt != null) notRestockedPrompt.Hide();
        }
    }
}
