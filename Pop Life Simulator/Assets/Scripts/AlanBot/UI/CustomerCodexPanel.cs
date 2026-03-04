using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Customers.Data;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// Customer Codex 完整面板
    /// Header：标题 + 实时在店顾客计数
    /// 左侧：搜索框 + 排序 + 顾客网格 + Mark All As Seen
    /// 右侧：顾客详情（肖像、Bio、忠诚度、XP进度条、偏好/统计切换）
    /// </summary>
    public class CustomerCodexPanel : MonoBehaviour
    {
        // ── 排序模式 ──
        public enum SortMode
        {
            IdAsc, IdDesc,
            AlphaAsc, AlphaDesc,
            LoyaltyAsc, LoyaltyDesc
        }

        [Header("面板控制")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;

        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text customersInStoreText;

        [Header("Left Panel - Search & Sort")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button sortButton;
        [SerializeField] private GameObject sortDropdown;
        [SerializeField] private Button sortIdAscButton;
        [SerializeField] private Button sortIdDescButton;
        [SerializeField] private Button sortAlphaAscButton;
        [SerializeField] private Button sortAlphaDescButton;
        [SerializeField] private Button sortLoyaltyAscButton;
        [SerializeField] private Button sortLoyaltyDescButton;

        [Header("Left Panel - Grid")]
        [SerializeField] private ScrollRect gridScrollRect;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private CodexCustomerEntry entryPrefab;
        [SerializeField] private Button markAllSeenButton;

        [Header("Right Panel")]
        [SerializeField] private GameObject detailContent;
        [SerializeField] private GameObject detailEmptyState;

        [Header("Right Panel - Customer Info")]
        [SerializeField] private Image customerPortraitLarge;
        [SerializeField] private TMP_Text customerNameText;
        [SerializeField] private TMP_Text bioText;
        [SerializeField] private Slider xpProgressBar;
        [SerializeField] private TMP_Text xpProgressText;

        [Header("Right Panel - Loyalty Stars")]
        [SerializeField] private Transform loyaltyStarContainer;
        [SerializeField] private Sprite filledStarSprite;
        [SerializeField] private Sprite emptyStarSprite;
        [SerializeField] private float starSize = 24f;

        [Header("Right Panel - Page Toggle")]
        [SerializeField] private Button shelfInterestsTabButton;
        [SerializeField] private Button lifetimeStatsTabButton;
        [SerializeField] private GameObject shelfInterestsPage;
        [SerializeField] private GameObject lifetimeStatsPage;
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = new Color(1f, 1f, 1f, 0.4f);

        [Header("Right Panel - Shelf Interests Page")]
        [SerializeField] private Transform shelfInterestListContainer;
        [SerializeField] private TMP_Text shelfInterestEntryPrefab;

        [Header("Right Panel - Lifetime Stats Page")]
        [SerializeField] private TMP_Text visitCountText;
        [SerializeField] private TMP_Text lifetimeSpentText;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        // ── 内部状态 ──
        private List<CustomerRecord> allRecords = new();
        private HashSet<string> unlockedIds = new();
        private SortMode currentSort = SortMode.IdAsc;
        private string searchQuery = "";
        private readonly List<CodexCustomerEntry> spawnedEntries = new();
        private CodexCustomerEntry selectedEntry;
        private Coroutine fadeCoroutine;
        private bool isInitialized;
        private bool showingStats; // false=Shelf Interests, true=Lifetime Stats
        private const int MaxLoyaltyLevel = 5;
        private Image[] loyaltyStarImages;

        // ── 关闭回调 ──
        private Action onCloseCallback;

        // ── 实时计数 ──
        private int cachedInStoreCount = -1;

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
                titleText.text = "CUSTOMER CODEX";

            // 绑定按钮
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            // 搜索框
            if (searchInput != null)
                searchInput.onValueChanged.AddListener(OnSearchChanged);

            // 排序按钮
            if (sortButton != null)
                sortButton.onClick.AddListener(ToggleSortDropdown);

            BindSortOptions();

            // 排序下拉初始隐藏
            if (sortDropdown != null)
                sortDropdown.SetActive(false);

            // Mark All As Seen
            if (markAllSeenButton != null)
                markAllSeenButton.onClick.AddListener(OnMarkAllSeenClicked);

            // 页面切换按钮
            if (shelfInterestsTabButton != null)
                shelfInterestsTabButton.onClick.AddListener(SwitchToShelfInterestsPage);
            if (lifetimeStatsTabButton != null)
                lifetimeStatsTabButton.onClick.AddListener(SwitchToLifetimeStatsPage);
        }

        private void OnEnable()
        {
            // 订阅图鉴状态事件
            if (CustomerCodexStateManager.Instance != null)
            {
                CustomerCodexStateManager.Instance.OnCustomerSeen += OnCustomerSeenChanged;
            }
        }

        private void OnDisable()
        {
            if (CustomerCodexStateManager.Instance != null)
            {
                CustomerCodexStateManager.Instance.OnCustomerSeen -= OnCustomerSeenChanged;
            }
        }

        private void Update()
        {
            // 面板可见时每30帧更新一次在店顾客计数
            if (panelRoot != null && panelRoot.activeSelf && Time.frameCount % 30 == 0)
            {
                UpdateInStoreCounter();
            }
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

            // 如果没有选中条目，选中第一个
            if (selectedEntry == null)
                SelectFirstEntry();

            // 更新在店计数
            UpdateInStoreCounter();

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
            // 显示空状态
            ShowEmptyState();

            // 初始化页面切换按钮状态（默认 Shelf Interests 页）
            ApplyPageToggle();

            isInitialized = true;
        }

        #endregion

        #region 数据加载

        /// <summary>
        /// 从 CustomerRepository 加载所有顾客记录，并缓存解锁ID集合
        /// </summary>
        private void LoadAllRecords()
        {
            allRecords.Clear();
            unlockedIds.Clear();

            if (CustomerRepository.Instance == null) return;

            // 缓存解锁ID
            var profile = SpawnerProfile.Load();
            if (profile != null)
                unlockedIds = new HashSet<string>(profile.unlockedCustomerIds);

            // 加载所有顾客记录
            foreach (var record in CustomerRepository.Instance.All())
            {
                if (record != null)
                    allRecords.Add(record);
            }
        }

        #endregion

        #region 排序

        private void BindSortOptions()
        {
            if (sortIdAscButton != null)
                sortIdAscButton.onClick.AddListener(() => SetSortMode(SortMode.IdAsc));
            if (sortIdDescButton != null)
                sortIdDescButton.onClick.AddListener(() => SetSortMode(SortMode.IdDesc));
            if (sortAlphaAscButton != null)
                sortAlphaAscButton.onClick.AddListener(() => SetSortMode(SortMode.AlphaAsc));
            if (sortAlphaDescButton != null)
                sortAlphaDescButton.onClick.AddListener(() => SetSortMode(SortMode.AlphaDesc));
            if (sortLoyaltyAscButton != null)
                sortLoyaltyAscButton.onClick.AddListener(() => SetSortMode(SortMode.LoyaltyAsc));
            if (sortLoyaltyDescButton != null)
                sortLoyaltyDescButton.onClick.AddListener(() => SetSortMode(SortMode.LoyaltyDesc));
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

        #region 条目列表

        /// <summary>
        /// 刷新所有条目（清除并重建）
        /// </summary>
        private void RefreshAllEntries()
        {
            ClearEntries();
            LoadAllRecords();

            var filtered = GetFilteredAndSortedRecords();

            foreach (var record in filtered)
            {
                var entryObj = Instantiate(entryPrefab, gridContainer);
                var entry = entryObj.GetComponent<CodexCustomerEntry>();

                if (entry == null) continue;

                // 加载肖像
                var archetype = LoadArchetype(record.archetypeId);
                Sprite portrait = CustomerPortraitLoader.LoadPortrait(record.customerId, archetype);

                // 确定状态
                var state = DetermineEntryState(record.customerId);
                entry.Initialize(record, portrait, state, OnEntryClicked);
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
        /// 获取经过过滤和排序的顾客记录列表
        /// 过滤链：Search → Sort
        /// </summary>
        private List<CustomerRecord> GetFilteredAndSortedRecords()
        {
            IEnumerable<CustomerRecord> result = allRecords;

            // 搜索过滤（大小写不敏感，子串匹配，仅搜索已解锁顾客名字）
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string query = searchQuery.ToLowerInvariant();
                result = result.Where(r =>
                    (unlockedIds.Contains(r.customerId) &&
                     r.name != null &&
                     r.name.ToLowerInvariant().Contains(query)) ||
                    !unlockedIds.Contains(r.customerId));
            }

            // 排序
            result = currentSort switch
            {
                SortMode.IdAsc => result.OrderBy(r => r.customerId),
                SortMode.IdDesc => result.OrderByDescending(r => r.customerId),
                SortMode.AlphaAsc => result.OrderBy(r => r.name),
                SortMode.AlphaDesc => result.OrderByDescending(r => r.name),
                SortMode.LoyaltyAsc => result.OrderBy(r => r.loyaltyLevel).ThenBy(r => r.customerId),
                SortMode.LoyaltyDesc => result.OrderByDescending(r => r.loyaltyLevel).ThenBy(r => r.customerId),
                _ => result
            };

            return result.ToList();
        }

        /// <summary>
        /// 确定条目应显示的状态：Locked / New / Unselected
        /// </summary>
        private CodexCustomerEntry.EntryState DetermineEntryState(string customerId)
        {
            if (!unlockedIds.Contains(customerId))
                return CodexCustomerEntry.EntryState.Locked;

            bool seen = CustomerCodexStateManager.Instance != null &&
                        CustomerCodexStateManager.Instance.IsCustomerSeen(customerId);

            return seen ? CodexCustomerEntry.EntryState.Unselected : CodexCustomerEntry.EntryState.New;
        }

        /// <summary>
        /// 选中第一个条目
        /// </summary>
        private void SelectFirstEntry()
        {
            var firstEntry = spawnedEntries.FirstOrDefault(
                e => e.CurrentState != CodexCustomerEntry.EntryState.Locked);

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
            if (selectedEntry != null && selectedEntry.Record != null)
            {
                // 找到刷新后对应的条目
                var match = spawnedEntries.FirstOrDefault(
                    e => e.Record != null && e.Record.customerId == selectedEntry.Record.customerId);

                if (match != null)
                {
                    selectedEntry = match;
                    match.SetState(CodexCustomerEntry.EntryState.Selected);
                    ShowDetailForCustomer(match.Record);
                    return;
                }
            }

            // 之前的选中条目不在新列表中，选中第一个
            selectedEntry = null;
            SelectFirstEntry();
        }

        #endregion

        #region 条目点击

        private void OnEntryClicked(CodexCustomerEntry entry)
        {
            if (entry == null) return;
            if (entry.CurrentState == CodexCustomerEntry.EntryState.Locked) return;

            // 取消之前的选中
            if (selectedEntry != null && selectedEntry != entry)
            {
                selectedEntry.SetState(CodexCustomerEntry.EntryState.Unselected);
            }

            // 如果是 New 状态，标记为已查看
            if (entry.CurrentState == CodexCustomerEntry.EntryState.New)
            {
                if (CustomerCodexStateManager.Instance != null)
                    CustomerCodexStateManager.Instance.MarkCustomerAsSeen(entry.Record.customerId);
            }

            // 设为选中
            entry.SetState(CodexCustomerEntry.EntryState.Selected);
            selectedEntry = entry;

            // 更新右侧详情
            ShowDetailForCustomer(entry.Record);
        }

        #endregion

        #region 右侧详情面板

        private void ShowDetailForCustomer(CustomerRecord record)
        {
            if (record == null) return;

            if (detailEmptyState != null)
                detailEmptyState.SetActive(false);
            if (detailContent != null)
                detailContent.SetActive(true);

            // 加载 archetype
            var archetype = LoadArchetype(record.archetypeId);

            // 全尺寸肖像
            Sprite portrait = CustomerPortraitLoader.LoadPortrait(record.customerId, archetype);
            if (customerPortraitLarge != null)
            {
                customerPortraitLarge.sprite = portrait;
                customerPortraitLarge.preserveAspect = true;
            }

            // 名字
            if (customerNameText != null)
                customerNameText.text = record.name ?? "";

            // Bio
            if (bioText != null)
                bioText.text = record.bio ?? "";

            // 忠诚度星星
            UpdateLoyaltyStars(record.loyaltyLevel);

            // XP 进度条
            UpdateXpProgressBar(record, archetype);

            // 偏好货架列表
            PopulateShelfInterests(record);

            // 生涯统计
            if (visitCountText != null)
                visitCountText.text = record.visitCount.ToString();

            if (lifetimeSpentText != null)
                lifetimeSpentText.text = $"${record.lifetimeSpent}";

            // 恢复当前页面显隐
            ApplyPageToggle();
        }

        private void UpdateXpProgressBar(CustomerRecord record, CustomerArchetype archetype)
        {
            if (xpProgressBar == null) return;

            if (archetype == null || archetype.levelUpThresholds == null ||
                archetype.levelUpThresholds.Length == 0)
            {
                xpProgressBar.value = 0;
                if (xpProgressText != null)
                    xpProgressText.text = "MAX";
                return;
            }

            int currentXp = record.xp;
            int level = record.loyaltyLevel;

            // levelUpThresholds 是累计阈值
            // loyaltyLevel 起始1，升到 level+1 需要 thresholds[level-1]
            int thresholdIndex = level - 1;
            if (thresholdIndex >= archetype.levelUpThresholds.Length)
            {
                // 已满级
                xpProgressBar.value = 1f;
                if (xpProgressText != null)
                    xpProgressText.text = "MAX";
                return;
            }

            int nextThreshold = archetype.levelUpThresholds[thresholdIndex];
            int prevThreshold = thresholdIndex > 0 ? archetype.levelUpThresholds[thresholdIndex - 1] : 0;
            int xpInLevel = currentXp - prevThreshold;
            int xpNeeded = nextThreshold - prevThreshold;

            xpProgressBar.value = xpNeeded > 0 ? Mathf.Clamp01((float)xpInLevel / xpNeeded) : 1f;

            if (xpProgressText != null)
                xpProgressText.text = $"{currentXp}/{nextThreshold}";
        }

        private void PopulateShelfInterests(CustomerRecord record)
        {
            if (shelfInterestListContainer == null) return;

            // 清除已有条目
            foreach (Transform child in shelfInterestListContainer)
                Destroy(child.gameObject);

            if (record.favoriteShelfIds == null || shelfInterestEntryPrefab == null) return;

            foreach (var shelfId in record.favoriteShelfIds)
            {
                if (string.IsNullOrEmpty(shelfId)) continue;

                var textObj = Instantiate(shelfInterestEntryPrefab, shelfInterestListContainer);
                textObj.text = shelfId;
            }
        }

        private void ShowEmptyState()
        {
            if (detailContent != null)
                detailContent.SetActive(false);
            if (detailEmptyState != null)
                detailEmptyState.SetActive(true);
        }

        #endregion

        #region 页面切换

        private void SwitchToShelfInterestsPage()
        {
            showingStats = false;
            ApplyPageToggle();
        }

        private void SwitchToLifetimeStatsPage()
        {
            showingStats = true;
            ApplyPageToggle();
        }

        /// <summary>
        /// 根据 showingStats 切换页面显隐和按钮视觉
        /// </summary>
        private void ApplyPageToggle()
        {
            if (shelfInterestsPage != null)
                shelfInterestsPage.SetActive(!showingStats);
            if (lifetimeStatsPage != null)
                lifetimeStatsPage.SetActive(showingStats);

            // 更新按钮颜色
            SetTabButtonColor(shelfInterestsTabButton, !showingStats);
            SetTabButtonColor(lifetimeStatsTabButton, showingStats);
        }

        private void SetTabButtonColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = active ? activeTabColor : inactiveTabColor;
        }

        #endregion

        #region 实时在店计数

        private void UpdateInStoreCounter()
        {
            if (customersInStoreText == null) return;

            int count = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None).Length;
            if (count != cachedInStoreCount)
            {
                cachedInStoreCount = count;
                customersInStoreText.text = $"Customers in Store: {count}";
            }
        }

        #endregion

        #region Mark All As Seen

        private void OnMarkAllSeenClicked()
        {
            if (CustomerCodexStateManager.Instance != null)
            {
                CustomerCodexStateManager.Instance.MarkAllAsSeen();
                RefreshAllEntries();
                RestoreSelection();
            }
        }

        #endregion

        #region 事件回调

        /// <summary>
        /// 某顾客被标记为已查看时的回调
        /// </summary>
        private void OnCustomerSeenChanged(string customerId)
        {
            // 如果面板不可见，无需更新UI
            if (panelRoot == null || !panelRoot.activeSelf) return;

            // 找到对应条目，如果它不是当前选中的，更新为Unselected
            var entry = spawnedEntries.FirstOrDefault(
                e => e.Record != null && e.Record.customerId == customerId);

            if (entry != null && entry != selectedEntry &&
                entry.CurrentState == CodexCustomerEntry.EntryState.New)
            {
                entry.SetState(CodexCustomerEntry.EntryState.Unselected);
            }
        }

        #endregion

        #region 忠诚度星星

        /// <summary>
        /// 懒初始化星星Image（在loyaltyStarContainer下动态创建5个Image子物体）
        /// </summary>
        private void EnsureLoyaltyStars()
        {
            if (loyaltyStarImages != null) return;
            if (loyaltyStarContainer == null) return;

            loyaltyStarImages = new Image[MaxLoyaltyLevel];
            for (int i = 0; i < MaxLoyaltyLevel; i++)
            {
                var go = new GameObject($"Star_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(loyaltyStarContainer, false);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(starSize, starSize);
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                loyaltyStarImages[i] = img;
            }
        }

        /// <summary>
        /// 根据忠诚度等级更新星星显示（亮星=已达成，灰星=未达成）
        /// </summary>
        private void UpdateLoyaltyStars(int loyaltyLevel)
        {
            EnsureLoyaltyStars();
            if (loyaltyStarImages == null) return;

            for (int i = 0; i < MaxLoyaltyLevel; i++)
            {
                loyaltyStarImages[i].sprite = (i < loyaltyLevel) ? filledStarSprite : emptyStarSprite;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 加载顾客 Archetype SO
        /// </summary>
        private CustomerArchetype LoadArchetype(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return null;
            return Resources.Load<CustomerArchetype>($"ScriptableObjects/CustomerArchetypes/{archetypeId}");
        }

        #endregion
    }
}
