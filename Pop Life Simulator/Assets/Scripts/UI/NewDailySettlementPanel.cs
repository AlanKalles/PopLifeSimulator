using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Customers.Runtime;
using PopLife.Data;
using PopLife.Manager;
using PopLife.UI.Settlement;
using XCharts.Runtime;

namespace PopLife.UI
{
    /// <summary>
    /// 全新每日结算面板 — 替代旧的 DailySettlementPanel
    /// 功能：XChart折线图/饼图、数字滚动动画、每日评级、对比箭头、热销商品、升级列表
    /// </summary>
    public class NewDailySettlementPanel : MonoBehaviour
    {
        #region SerializeField

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private Image gradeBadgeBackground;

        [Header("Pages")]
        [SerializeField] private GameObject page1Root;
        [SerializeField] private GameObject page2Root;
        [SerializeField] private Button page1TabButton;
        [SerializeField] private Button page2TabButton;

        [Header("Tab Button Text Colors")]
        [SerializeField] private TextMeshProUGUI page1TabLabel;
        [SerializeField] private TextMeshProUGUI page2TabLabel;
        [SerializeField] private Color tabActiveTextColor = Color.white;
        [SerializeField] private Color tabInactiveTextColor = new Color(0.5f, 0.5f, 0.5f);

        [Header("Key Metrics — Page 1 (6 cards)")]
        [SerializeField] private TextMeshProUGUI revenueValueText;
        [SerializeField] private TextMeshProUGUI revenueComparisonText;
        [SerializeField] private TextMeshProUGUI expensesValueText;
        [SerializeField] private TextMeshProUGUI expensesComparisonText;
        [SerializeField] private TextMeshProUGUI netProfitValueText;
        [SerializeField] private TextMeshProUGUI netProfitComparisonText;
        [SerializeField] private TextMeshProUGUI customersEnteredValueText;
        [SerializeField] private TextMeshProUGUI customersEnteredComparisonText;
        [SerializeField] private TextMeshProUGUI avgSpendValueText;
        [SerializeField] private TextMeshProUGUI avgSpendComparisonText;
        [SerializeField] private TextMeshProUGUI fameValueText;
        [SerializeField] private TextMeshProUGUI fameComparisonText;

        [Header("Net Profit Icon Swap")]
        [SerializeField] private Image netProfitIconImage;
        [SerializeField] private Sprite netProfitPositiveIcon;
        [SerializeField] private Sprite netProfitNegativeIcon;

        [Header("Today's Highlights (P1, placeholder)")]
        [SerializeField] private GameObject todayHighlightsContainer;

        [Header("Charts")]
        [SerializeField] private LineChart lineChartPrefab;  // XCharts 预制体（在 Inspector 中拖拽赋值）
        [SerializeField] private PieChart pieChartPrefab;    // XCharts 预制体（在 Inspector 中拖拽赋值）
        [SerializeField] private RectTransform lineChartContainer;
        [SerializeField] private RectTransform pieChartContainer;
        [SerializeField] private TMP_FontAsset chartFont;  // 可选：图表自定义TMP字体，留空则使用主题默认

        [Header("Hot Sellers")]
        [SerializeField] private GameObject hotSellerItemPrefab;
        [SerializeField] private Transform hotSellersContainer;
        [SerializeField] private TextMeshProUGUI noHotSellerText;

        [Header("Level Up Section")]
        [SerializeField] private GameObject levelUpContainer;
        [SerializeField] private GameObject levelUpItemPrefab;
        [SerializeField] private TextMeshProUGUI noLevelUpText;

        [Header("Lifetime Stats (P1, 4 items)")]
        [SerializeField] private TextMeshProUGUI lifetimeIncomeText;
        [SerializeField] private TextMeshProUGUI lifetimeExpenseText;
        [SerializeField] private TextMeshProUGUI totalCustomersText;       // 现复用为 Lifetime Customers
        [SerializeField] private TextMeshProUGUI lifetimeFameText;         // 累计 fame

        // ─── Page 2 ───
        [Header("P2 Revenue Breakdown")]
        [SerializeField] private TextMeshProUGUI saleAmountText, salePctText;
        [SerializeField] private TextMeshProUGUI lotteryAmountText, lotteryPctText;
        [SerializeField] private TextMeshProUGUI questAmountText, questPctText;
        [SerializeField] private TextMeshProUGUI refundAmountText, refundPctText;
        [SerializeField] private TextMeshProUGUI sponsorshipAmountText, sponsorshipPctText;
        [SerializeField] private Image saleBar, lotteryBar, questBar, refundBar, sponsorshipBar;
        [SerializeField] private TextMeshProUGUI totalRevenueText;

        [Header("P2 Expense Breakdown")]
        [SerializeField] private TextMeshProUGUI restockAmountText, restockPctText;
        [SerializeField] private TextMeshProUGUI constructionAmountText, constructionPctText;
        [SerializeField] private TextMeshProUGUI maintenanceAmountText, maintenancePctText;
        [SerializeField] private Image restockBar, constructionBar, maintenanceBar;
        [SerializeField] private TextMeshProUGUI totalExpenseText;

        [Header("P2 Customer Analysis (icon+title+content prefab)")]
        [SerializeField] private TextMeshProUGUI newCustomersValue;
        [SerializeField] private TextMeshProUGUI returningCustomersValue;
        [SerializeField] private TextMeshProUGUI avgProductsValue;
        [SerializeField] private TextMeshProUGUI avgShelvesValue;
        [SerializeField] private TextMeshProUGUI mostShelfValue;
        [SerializeField] private TextMeshProUGUI mostCategoryValue;
        [SerializeField] private TextMeshProUGUI highestShelfSpendValue;

        [Header("Control")]
        [SerializeField] private Button continueButton;

        [Header("Animation Settings")]
        [SerializeField] private float countDuration = 1.2f;
        [SerializeField] private float tickInterval = 0.05f;
        [SerializeField] private float sectionStaggerDelay = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color greenColor = new Color(0f, 0.69f, 0.11f);
        [SerializeField] private Color redColor = new Color(1f, 0.27f, 0.27f);
        [SerializeField] private Color greyColor = new Color(0.53f, 0.53f, 0.53f);

        #endregion

        #region 私有变量

        private LineChart lineChart;
        private PieChart pieChart;
        private bool chartsInitialized;
        private Coroutine animationCoroutine;

        // 评级颜色映射
        private static readonly Dictionary<string, Color> gradeColors = new Dictionary<string, Color>
        {
            { "S+", new Color(1f, 0.4f, 0.8f) },      // 粉紫色 #FF66CC（罕见，挑战目标）
            { "S", new Color(1f, 0.84f, 0f) },       // 金色 #FFD700
            { "A", new Color(0f, 0.69f, 0.11f) },     // 绿色 #00B11D
            { "B", new Color(0.27f, 0.53f, 1f) },     // 蓝色 #4488FF
            { "C", new Color(1f, 0.67f, 0f) },        // 黄色 #FFAA00
            { "D", new Color(1f, 0.27f, 0.27f) }      // 红色 #FF4444
        };

        #endregion

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            // Tab buttons
            if (page1TabButton != null)
                page1TabButton.onClick.AddListener(() => ShowPage(0));
            if (page2TabButton != null)
                page2TabButton.onClick.AddListener(() => ShowPage(1));

            // Today's Highlights 当前为空占位，整块隐藏
            if (todayHighlightsContainer != null)
                todayHighlightsContainer.SetActive(false);
        }

        /// <summary>
        /// 切换页面：根据 idx 设置 page root active / tab 按钮 interactable / tab label 颜色
        /// 0 = Summary, 1 = Details
        /// </summary>
        private void ShowPage(int idx)
        {
            if (page1Root != null) page1Root.SetActive(idx == 0);
            if (page2Root != null) page2Root.SetActive(idx == 1);

            bool page1IsActive = idx == 0;
            if (page1TabButton != null) page1TabButton.interactable = !page1IsActive;
            if (page2TabButton != null) page2TabButton.interactable = page1IsActive;

            // disabled（即当前页）用 active 色，可点击的用 inactive 色
            if (page1TabLabel != null) page1TabLabel.color = page1IsActive ? tabActiveTextColor : tabInactiveTextColor;
            if (page2TabLabel != null) page2TabLabel.color = page1IsActive ? tabInactiveTextColor : tabActiveTextColor;
        }

        /// <summary>
        /// 显示结算面板 — 由 UIManager.OnDailySettlement 调用
        /// </summary>
        public void ShowSettlement(DailySettlementData data)
        {
            // 停止之前未完成的动画
            if (animationCoroutine != null)
                StopCoroutine(animationCoroutine);

            // 每次显示默认显示 Page 1
            ShowPage(0);

            // 首次初始化图表（异步）
            if (!chartsInitialized)
            {
                StartCoroutine(InitChartsAsync(data));
                return;
            }

            // 收集扩展数据
            var categoryRevenue = StatsDataManager.Instance?.GetCategoryRevenueBreakdown();
            var shelfStats = StatsDataManager.Instance?.GetAllShelfStats();
            var topSellers = shelfStats?
                .Where(s => s.todayRevenue > 0)
                .OrderByDescending(s => s.todayRevenue)
                .Take(3)
                .ToList();

            // 构建历史记录条目并保存
            var historyEntry = BuildHistoryEntry(data, categoryRevenue, topSellers);
            SettlementHistoryManager.Instance?.RecordDay(historyEntry);

            // 获取历史数据
            var history = SettlementHistoryManager.Instance?.GetHistory(7);
            var yesterday = SettlementHistoryManager.Instance?.GetYesterday();

            // 应用 Fame 奖励（与旧面板相同逻辑）
            ResourceManager.Instance?.AddFame(data.fameEarned);

            // 启动分段动画序列
            animationCoroutine = StartCoroutine(
                AnimateSettlement(data, history, yesterday, categoryRevenue, topSellers));
        }

        /// <summary>
        /// 异步初始化图表 — 等待下一帧以确保 Awake() 完成
        /// </summary>
        private IEnumerator InitChartsAsync(DailySettlementData data)
        {
            // 创建图表对象
            CreateLineChartObject();
            CreatePieChartObject();

            // 立即清空预制体自带的演示数据，防止它在等待帧中被渲染出来
            if (lineChart != null) lineChart.ClearData();
            if (pieChart != null) pieChart.ClearData();

            // 等待一帧，让 Unity 调用 Awake() 完成初始化
            yield return null;

            // 配置图表属性
            ConfigureLineChart();
            ConfigurePieChart();

            chartsInitialized = true;

            // 重新调用 ShowSettlement（此时图表已初始化）
            ShowSettlement(data);
        }

        #region 动画序列

        private IEnumerator AnimateSettlement(
            DailySettlementData data,
            SettlementHistoryEntry[] history,
            SettlementHistoryEntry yesterday,
            Dictionary<ProductCategory, float> categoryRevenue,
            List<ShelfStatsData> topSellers)
        {
            // --- 隐藏 Continue 按钮，等全部数据展示完毕后再显示 ---
            if (continueButton != null)
                continueButton.gameObject.SetActive(false);

            // --- 清空所有文本（准备动画） ---
            ClearAllTexts();

            // --- Header ---
            if (dayText != null)
                dayText.text = DayLoopManager.Instance != null
                    ? CalendarUtils.FormatFullDate(data.day, DayLoopManager.Instance.StartingYear)
                    : $"Day {data.day}";

            string grade = CalculateDailyGrade(data);
            SetGrade(grade);

            yield return new WaitForSecondsRealtime(0.2f);

            // --- 计算 Settlement v2 派生指标 ---
            float totalRevenue = data.totalSale + data.lotteryWinnings + data.sponsorshipAmount
                               + data.questRewardIncome + data.refundIncome;
            float totalExpense = data.restockExpense + data.constructionExpense + data.maintenanceExpense;
            // 旧 dailyExpenses 字段当 fallback（兼容旧维护费独立扣的逻辑）
            if (totalExpense <= 0) totalExpense = data.dailyExpenses;
            float netProfit = totalRevenue - totalExpense;
            float avgSpend = data.customersEntered > 0
                ? data.totalSale / (float)data.customersEntered : 0f;

            // --- Net Profit 图标 sprite 切换（颜色仍走动画 finalColor）---
            SetNetProfitIcon(netProfit);

            // --- 数字滚动动画（6 个并行） ---
            StartCoroutine(NumberCountAnimator.AnimateValue(
                revenueValueText, 0, totalRevenue, countDuration, "${0:F0}", tickInterval));

            StartCoroutine(NumberCountAnimator.AnimateValue(
                expensesValueText, 0, totalExpense, countDuration, "${0:F0}", tickInterval));

            StartCoroutine(NumberCountAnimator.AnimateValue(
                netProfitValueText, 0, netProfit, countDuration, "${0:F0}", tickInterval,
                netProfit >= 0 ? greenColor : redColor));

            StartCoroutine(NumberCountAnimator.AnimateValue(
                customersEnteredValueText, 0, data.customersEntered, countDuration, "{0:F0}", tickInterval));

            StartCoroutine(NumberCountAnimator.AnimateValue(
                avgSpendValueText, 0, avgSpend, countDuration, "${0:F1}", tickInterval));

            StartCoroutine(NumberCountAnimator.AnimateValue(
                fameValueText, 0, data.fameEarned, countDuration, "+{0:F0}", tickInterval));

            // 等待数字动画完成
            yield return new WaitForSecondsRealtime(countDuration + 0.1f);

            // --- 对比箭头 ---
            SetComparisonArrows(data, yesterday);

            yield return new WaitForSecondsRealtime(sectionStaggerDelay);

            // --- 填充图表 ---
            if (history != null && history.Length > 0)
                PopulateLineChart(history);

            if (categoryRevenue != null)
                PopulatePieChart(categoryRevenue);

            yield return new WaitForSecondsRealtime(sectionStaggerDelay);

            // --- 热销商品 ---
            PopulateHotSellers(topSellers);

            // --- 升级列表（移到 Page 2 Customer Upgrade 区域，Inspector 内把 container re-parent 到 page2Root 下）---
            PopulateLevelUpList(data.levelUps);

            // --- 生涯统计（4 项：含全部进账 / 全部支出 / 累计入店顾客 / 累计 Fame）---
            if (lifetimeIncomeText != null)
                lifetimeIncomeText.text = $"${data.lifetimeAllIncome:F0}";
            if (lifetimeExpenseText != null)
                lifetimeExpenseText.text = $"${data.lifetimeAllExpenses:F0}";
            if (totalCustomersText != null)
                totalCustomersText.text = $"{data.lifetimeCustomers}";
            if (lifetimeFameText != null)
                lifetimeFameText.text = $"+{data.lifetimeFameGained}";

            // --- Page 2 内容立即填充（不做数字动画，避免重复滚动）---
            PopulateRevenueBreakdown(data, totalRevenue);
            PopulateExpenseBreakdown(data, totalExpense);
            PopulateCustomerAnalysis(data);

            // --- 所有数据展示完毕，显示 Continue 按钮 ---
            if (continueButton != null)
                continueButton.gameObject.SetActive(true);
        }

        private void ClearAllTexts()
        {
            // 清空数值文本（Page 1 6 个指标）
            if (revenueValueText != null) revenueValueText.text = "$0";
            if (expensesValueText != null) expensesValueText.text = "$0";
            if (netProfitValueText != null) { netProfitValueText.text = "$0"; netProfitValueText.color = Color.white; }
            if (customersEnteredValueText != null) customersEnteredValueText.text = "0";
            if (avgSpendValueText != null) avgSpendValueText.text = "$0";
            if (fameValueText != null) fameValueText.text = "+0";

            // 清空对比箭头
            if (revenueComparisonText != null) revenueComparisonText.text = "";
            if (expensesComparisonText != null) expensesComparisonText.text = "";
            if (netProfitComparisonText != null) netProfitComparisonText.text = "";
            if (customersEnteredComparisonText != null) customersEnteredComparisonText.text = "";
            if (avgSpendComparisonText != null) avgSpendComparisonText.text = "";
            if (fameComparisonText != null) fameComparisonText.text = "";

            // 清空生涯统计
            if (lifetimeIncomeText != null) lifetimeIncomeText.text = "";
            if (lifetimeExpenseText != null) lifetimeExpenseText.text = "";
            if (totalCustomersText != null) totalCustomersText.text = "";
            if (lifetimeFameText != null) lifetimeFameText.text = "";
        }

        /// <summary>
        /// Net Profit 图标 sprite 切换（正/负）
        /// </summary>
        private void SetNetProfitIcon(float netProfit)
        {
            if (netProfitIconImage == null) return;
            if (netProfit >= 0 && netProfitPositiveIcon != null)
                netProfitIconImage.sprite = netProfitPositiveIcon;
            else if (netProfit < 0 && netProfitNegativeIcon != null)
                netProfitIconImage.sprite = netProfitNegativeIcon;
        }

        #endregion

        #region 评级系统

        /// <summary>
        /// 计算每日评级 (S+/S/A/B/C/D)
        /// 公式实现移到 DailyGradeEvaluator (Scripts/UI/Settlement/DailyGradeEvaluator.cs)
        /// 3 维度 100 分制：盈利能力 35 + 成长性 35 + Fame 30
        /// Tier 按天数自动切换（每 8 天升一档，共 4 档）
        /// </summary>
        private string CalculateDailyGrade(DailySettlementData data)
        {
            var tier = GradeTier.From(data.day);
            var (grade, _) = DailyGradeEvaluator.Evaluate(data, tier);
            return grade;
        }

        private void SetGrade(string grade)
        {
            if (gradeText != null && gradeColors.TryGetValue(grade, out Color textColor))
            {
                gradeText.text = grade;
                gradeText.color = textColor;
            }

            if (gradeBadgeBackground != null && gradeColors.TryGetValue(grade, out Color bgColor))
                gradeBadgeBackground.color = bgColor;

            // 播放评级音效
            AudioManager.Instance?.PlaySound(AudioKeys.SETTLEMENT_GRADE);
        }

        #endregion

        #region 对比箭头

        private void SetComparisonArrows(DailySettlementData data, SettlementHistoryEntry yesterday)
        {
            // 计算今日 v2 口径（与 Page 1 显示值一致）
            float todayRevenue = data.totalSale + data.lotteryWinnings + data.sponsorshipAmount
                               + data.questRewardIncome + data.refundIncome;
            float todayExpense = data.restockExpense + data.constructionExpense + data.maintenanceExpense;
            if (todayExpense <= 0) todayExpense = data.dailyExpenses;
            float todayProfit = todayRevenue - todayExpense;
            float todayAvgSpend = data.customersEntered > 0
                ? data.totalSale / (float)data.customersEntered : 0f;

            if (yesterday == null)
            {
                // 第一天没有对比数据
                if (revenueComparisonText != null) revenueComparisonText.text = "";
                if (expensesComparisonText != null) expensesComparisonText.text = "";
                if (netProfitComparisonText != null) netProfitComparisonText.text = "";
                if (customersEnteredComparisonText != null) customersEnteredComparisonText.text = "";
                if (avgSpendComparisonText != null) avgSpendComparisonText.text = "";
                if (fameComparisonText != null) fameComparisonText.text = "";
                return;
            }

            // 昨日 v2 口径（旧记录 totalRevenueV2/totalExpenseV2/netProfitV2 默认 0 → 触发 NEW 状态）
            float yRevenue = yesterday.totalRevenueV2 > 0 ? yesterday.totalRevenueV2 : yesterday.totalSale;
            float yExpense = yesterday.totalExpenseV2 > 0 ? yesterday.totalExpenseV2 : yesterday.dailyExpenses;
            float yProfit = yesterday.totalRevenueV2 > 0 ? yesterday.netProfitV2 : yesterday.dailyIncome;
            float yAvgSpend = yesterday.customersEntered > 0
                ? yesterday.totalSale / (float)yesterday.customersEntered : 0f;

            if (revenueComparisonText != null)
                revenueComparisonText.text = FormatComparison(todayRevenue, yRevenue);
            if (expensesComparisonText != null)
                expensesComparisonText.text = FormatComparison(todayExpense, yExpense, true);
            if (netProfitComparisonText != null)
                netProfitComparisonText.text = FormatComparison(todayProfit, yProfit);
            if (customersEnteredComparisonText != null)
                customersEnteredComparisonText.text = FormatComparison(data.customersEntered, yesterday.customersEntered);
            if (avgSpendComparisonText != null)
                avgSpendComparisonText.text = FormatComparison(todayAvgSpend, yAvgSpend);
            if (fameComparisonText != null)
                fameComparisonText.text = FormatComparison(data.fameEarned, yesterday.fameEarned);
        }

        /// <summary>
        /// 格式化对比百分比（含颜色和箭头）
        /// </summary>
        /// <param name="invertColor">是否反转颜色逻辑（费用增加应为红色）</param>
        private string FormatComparison(float todayValue, float yesterdayValue, bool invertColor = false)
        {
            if (yesterdayValue == 0f)
            {
                if (todayValue > 0)
                    return $"<color=#{ColorToHex(invertColor ? redColor : greenColor)}>NEW</color>";
                return "";
            }

            float percentChange = ((todayValue - yesterdayValue) / Mathf.Abs(yesterdayValue)) * 100f;

            if (percentChange > 0.5f)
            {
                Color c = invertColor ? redColor : greenColor;
                return $"<color=#{ColorToHex(c)}>+{percentChange:F0}% \u25b2</color>";
            }
            else if (percentChange < -0.5f)
            {
                Color c = invertColor ? greenColor : redColor;
                return $"<color=#{ColorToHex(c)}>{percentChange:F0}% \u25bc</color>";
            }
            else
            {
                return $"<color=#{ColorToHex(greyColor)}>0% \u2500</color>";
            }
        }

        private static string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        #endregion

        #region XChart 折线图

        /// <summary>
        /// 第一步：实例化 LineChart 预制体
        /// </summary>
        private void CreateLineChartObject()
        {
            if (lineChartContainer == null || lineChartPrefab == null)
            {
                Debug.LogWarning("[NewDailySettlementPanel] LineChart 容器或预制体未配置！");
                return;
            }

            // 实例化预制体
            lineChart = Instantiate(lineChartPrefab, lineChartContainer);

            // 设置 RectTransform 填充父容器
            var rt = lineChart.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// 第二步：配置 LineChart 属性（预制体已有基础配置，这里只做运行时调整）
        /// </summary>
        private void ConfigureLineChart()
        {
            if (lineChart == null) return;

            // 注意：预制体应该已经在 Inspector 中配置了以下内容：
            // - Theme (Dark/Light)
            // - GridCoord
            // - XAxis, YAxis
            // - Tooltip, Legend
            // - Serie (Line × 3)

            // 这里只做必要的运行时调整
            // 如果需要自定义字体，在预制体中配置即可
        }

        private void PopulateLineChart(SettlementHistoryEntry[] history)
        {
            if (lineChart == null || history == null) return;

            lineChart.ClearData();

            foreach (var entry in history)
            {
                lineChart.AddXAxisData($"Day {entry.day}");
                lineChart.AddData("Revenue", entry.totalSale);
                lineChart.AddData("Expenses", entry.dailyExpenses);
                lineChart.AddData("Net Profit", entry.dailyIncome);
            }

            lineChart.AnimationFadeIn();
        }

        #endregion

        #region XChart 饼图

        /// <summary>
        /// 第一步：实例化 PieChart 预制体
        /// </summary>
        private void CreatePieChartObject()
        {
            if (pieChartContainer == null || pieChartPrefab == null)
            {
                Debug.LogWarning("[NewDailySettlementPanel] PieChart 容器或预制体未配置！");
                return;
            }

            // 实例化预制体
            pieChart = Instantiate(pieChartPrefab, pieChartContainer);

            // 设置 RectTransform 填充父容器
            var rt = pieChart.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// 第二步：配置 PieChart 属性（预制体已有基础配置，这里只做运行时调整）
        /// </summary>
        private void ConfigurePieChart()
        {
            if (pieChart == null) return;

            // 注意：预制体应该已经在 Inspector 中配置了以下内容：
            // - Theme (Dark/Light)
            // - Legend, Tooltip
            // - Pie Serie

            // 这里只做必要的运行时调整
        }

        private void PopulatePieChart(Dictionary<ProductCategory, float> categoryRevenue)
        {
            if (pieChart == null || categoryRevenue == null) return;

            pieChart.ClearData();

            foreach (var kvp in categoryRevenue)
            {
                if (kvp.Value > 0)
                {
                    pieChart.AddData(0, kvp.Value, kvp.Key.ToString());
                }
            }

            pieChart.AnimationFadeIn();
        }

        #endregion

        #region 热销商品

        private void PopulateHotSellers(List<ShelfStatsData> topSellers)
        {
            // 清空旧条目
            if (hotSellersContainer != null)
            {
                foreach (Transform child in hotSellersContainer)
                    Destroy(child.gameObject);
            }

            if (topSellers == null || topSellers.Count == 0)
            {
                if (noHotSellerText != null) noHotSellerText.gameObject.SetActive(true);
                return;
            }

            if (noHotSellerText != null) noHotSellerText.gameObject.SetActive(false);

            if (hotSellerItemPrefab == null || hotSellersContainer == null) return;

            for (int i = 0; i < topSellers.Count; i++)
            {
                var seller = topSellers[i];
                var itemObj = Instantiate(hotSellerItemPrefab, hotSellersContainer);

                // 通过组件获取引用（而非通过名称查找）
                var itemComponent = itemObj.GetComponent<HotSellerItem>();
                if (itemComponent != null)
                {
                    itemComponent.SetData(
                        rank: i + 1,
                        productName: seller.name,
                        sprite: seller.sprite,
                        revenue: seller.todayRevenue
                    );
                }
                else
                {
                    Debug.LogWarning("[NewDailySettlementPanel] HotSellerItem 预制体缺少 HotSellerItem 组件！");
                }
            }
        }

        #endregion

        #region 升级列表

        private void PopulateLevelUpList(CustomerLevelUpInfo[] levelUps)
        {
            // 清空旧条目
            if (levelUpContainer != null)
            {
                foreach (Transform child in levelUpContainer.transform)
                    Destroy(child.gameObject);
            }

            if (levelUps == null || levelUps.Length == 0)
            {
                if (noLevelUpText != null) noLevelUpText.gameObject.SetActive(true);
                return;
            }

            if (noLevelUpText != null) noLevelUpText.gameObject.SetActive(false);

            if (levelUpItemPrefab == null || levelUpContainer == null) return;

            foreach (var levelUp in levelUps)
            {
                var item = Instantiate(levelUpItemPrefab, levelUpContainer.transform);

                var itemComponent = item.GetComponent<LevelUpItem>();
                if (itemComponent != null)
                {
                    // 加载完整肖像（Resources/CustomerPortraits/），失败时回退到 archetype.portrait
                    Sprite portraitSprite = CustomerPortraitLoader.LoadPortrait(levelUp.customerId);

                    itemComponent.SetData(portraitSprite, levelUp.customerName,
                        levelUp.oldLevel, levelUp.newLevel, levelUp.xpGained);
                }
                else
                {
                    Debug.LogWarning("[NewDailySettlementPanel] LevelUpItem 预制体缺少 LevelUpItem 组件！");
                }
            }
        }

        #endregion

        #region Page 2 — Revenue Breakdown / Expense Breakdown / Customer Analysis

        /// <summary>
        /// Page 2 Revenue Breakdown — 固定 5 行（sale / lottery / quest / refund / sponsorship），始终显示
        /// 每行只更新 amount/pct/bar，icon 和 title 在预制体中静态设置
        /// </summary>
        private void PopulateRevenueBreakdown(DailySettlementData data, float totalRevenue)
        {
            float divisor = totalRevenue > 0 ? totalRevenue : 1f;

            void Apply(TextMeshProUGUI amountText, TextMeshProUGUI pctText, Image bar, int amount)
            {
                if (amountText != null) amountText.text = $"${amount}";
                float pct = amount / divisor;
                if (pctText != null) pctText.text = $"{Mathf.RoundToInt(pct * 100f)}%";
                if (bar != null) bar.fillAmount = Mathf.Clamp01(pct);
            }

            Apply(saleAmountText, salePctText, saleBar, (int)data.totalSale);
            Apply(lotteryAmountText, lotteryPctText, lotteryBar, data.lotteryWinnings);
            Apply(questAmountText, questPctText, questBar, data.questRewardIncome);
            Apply(refundAmountText, refundPctText, refundBar, data.refundIncome);
            Apply(sponsorshipAmountText, sponsorshipPctText, sponsorshipBar, data.sponsorshipAmount);

            if (totalRevenueText != null)
                totalRevenueText.text = $"${(int)totalRevenue}";
        }

        /// <summary>
        /// Page 2 Expense Breakdown — 3 个固定行（restock / construction / maintenance）+ Total
        /// 用 fillAmount 表达 % 条
        /// </summary>
        private void PopulateExpenseBreakdown(DailySettlementData data, float totalExpense)
        {
            float divisor = totalExpense > 0 ? totalExpense : 1f;

            void Apply(TextMeshProUGUI amountText, TextMeshProUGUI pctText, Image bar, int amount)
            {
                if (amountText != null) amountText.text = $"${amount}";
                float pct = amount / divisor;
                if (pctText != null) pctText.text = $"{Mathf.RoundToInt(pct * 100f)}%";
                if (bar != null) bar.fillAmount = Mathf.Clamp01(pct);
            }

            Apply(restockAmountText, restockPctText, restockBar, data.restockExpense);
            Apply(constructionAmountText, constructionPctText, constructionBar, data.constructionExpense);
            Apply(maintenanceAmountText, maintenancePctText, maintenanceBar, data.maintenanceExpense);

            if (totalExpenseText != null)
                totalExpenseText.text = $"${(int)totalExpense}";
        }

        /// <summary>
        /// Page 2 Customer Analysis — 7 个 value TMP，icon+title 在预制体内静态设置
        /// </summary>
        private void PopulateCustomerAnalysis(DailySettlementData data)
        {
            if (newCustomersValue != null)
                newCustomersValue.text = $"{data.newCustomersCount}";
            if (returningCustomersValue != null)
                returningCustomersValue.text = $"{data.returningCustomersCount}";
            if (avgProductsValue != null)
                avgProductsValue.text = $"{data.avgProductsPerCustomer:F1}";
            if (avgShelvesValue != null)
                avgShelvesValue.text = $"{data.avgShelvesPerCustomer:F1}";
            if (mostShelfValue != null)
                mostShelfValue.text = string.IsNullOrEmpty(data.mostPurchasedShelfName)
                    ? "—" : data.mostPurchasedShelfName;
            if (mostCategoryValue != null)
                mostCategoryValue.text = string.IsNullOrEmpty(data.mostPurchasedCategory)
                    ? "—" : data.mostPurchasedCategory;
            if (highestShelfSpendValue != null)
                highestShelfSpendValue.text = data.highestSingleShelfSpend > 0
                    ? $"${data.highestSingleShelfSpend}" : "—";
        }

        #endregion

        #region 数据构建

        /// <summary>
        /// 从当日数据构建历史记录条目
        /// </summary>
        private SettlementHistoryEntry BuildHistoryEntry(
            DailySettlementData data,
            Dictionary<ProductCategory, float> categoryRevenue,
            List<ShelfStatsData> topSellers)
        {
            // v2 聚合口径（用于"vs 昨天"对比）
            float totalRevenueV2 = data.totalSale + data.lotteryWinnings + data.sponsorshipAmount
                                 + data.questRewardIncome + data.refundIncome;
            float totalExpenseV2 = data.restockExpense + data.constructionExpense + data.maintenanceExpense;
            if (totalExpenseV2 <= 0) totalExpenseV2 = data.dailyExpenses; // 兼容
            float netProfitV2 = totalRevenueV2 - totalExpenseV2;

            var entry = new SettlementHistoryEntry
            {
                day = data.day,
                totalSale = data.totalSale,
                dailyExpenses = data.dailyExpenses,
                dailyIncome = data.dailyIncome,
                totalCustomers = data.totalCustomers,
                fameEarned = data.fameEarned,
                // v2 扩展字段
                customersEntered = data.customersEntered,
                restockExpense = data.restockExpense,
                constructionExpense = data.constructionExpense,
                maintenanceExpense = data.maintenanceExpense,
                // v2 聚合口径
                totalRevenueV2 = totalRevenueV2,
                totalExpenseV2 = totalExpenseV2,
                netProfitV2 = netProfitV2
            };

            // 品类收入数组
            if (categoryRevenue != null)
            {
                var categories = (ProductCategory[])Enum.GetValues(typeof(ProductCategory));
                entry.categoryRevenue = new float[categories.Length];
                for (int i = 0; i < categories.Length; i++)
                {
                    if (categoryRevenue.TryGetValue(categories[i], out float rev))
                        entry.categoryRevenue[i] = rev;
                }
            }

            // 热销商品
            if (topSellers != null && topSellers.Count > 0)
            {
                entry.hotSellers = topSellers.Select(s => new HotSellerEntry
                {
                    shelfName = s.name,
                    category = s.category,
                    revenue = s.todayRevenue,
                    archetypeId = s.shelfId
                }).ToArray();
            }

            return entry;
        }

        #endregion

        #region 图表字体

        /// <summary>
        /// 将TMP字体应用到图表主题的所有子组件（坐标轴、图例、Tooltip等）
        /// 通过 sharedTheme.tmpFont 设置，setter内部自动调用 SyncTMPFontToSubComponent()
        /// 同步到: common, title, subTitle, legend, axis, tooltip, dataZoom, visualMap
        /// </summary>
        private static void ApplyChartFont(BaseChart chart, TMP_FontAsset font)
        {
            var theme = chart.theme.sharedTheme;
            theme.tmpFont = font;  // setter 自动同步到全部子组件主题

            // 显式确保每个子组件主题都被覆盖（防止某些版本同步不完整）
            theme.common.tmpFont = font;
            theme.axis.tmpFont = font;
            theme.legend.tmpFont = font;
            theme.tooltip.tmpFont = font;
            theme.title.tmpFont = font;
            theme.subTitle.tmpFont = font;
            theme.dataZoom.tmpFont = font;
            theme.visualMap.tmpFont = font;
        }

        #endregion

        #region Continue按钮

        private void OnContinueClicked()
        {
            // 停止进行中的动画
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            // 触发首日完成教程标记
            GameStateManager.Instance?.NotifyFirstDayCompleted();

            // 隐藏面板
            gameObject.SetActive(false);

            // 重新加载蓝图配置
            BlueprintManager.Instance?.ReloadProfile();

            // 通知 DayLoopManager 进入下一天的建造阶段
            var dayLoopManager = DayLoopManager.Instance;
            dayLoopManager?.AdvanceToNextDay();

            // 成功进入下一天建造阶段后，再切换回白天BGM并触发背景回白天
            if (dayLoopManager != null && dayLoopManager.currentPhase == GamePhase.BuildPhase)
            {
                BGMController.Instance?.TransitionToDayBGM();
            }
        }

        #endregion
    }
}
