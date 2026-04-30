using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PopLife.Customers.Runtime;
using PopLife.Data;
using PopLife.UI;

namespace PopLife
{
    /// <summary>
    /// 游戏阶段枚举
    /// </summary>
    public enum GamePhase
    {
        BuildPhase,  // 建造阶段：早上6:00，时间暂停
        OpenPhase    // 营业阶段：12:00-23:00，时间流动
    }

    [DefaultExecutionOrder(-100)]
    public class DayLoopManager : MonoBehaviour
    {
        public static DayLoopManager Instance;

        [Header("Time Settings")]
        [SerializeField] private float realSecondsPerDay = 30f; // 现实30秒 = 游戏1天
        [SerializeField][Range(0f, 36f)] private float buildPhaseHour = 6f; // 6:00 建造阶段
        [SerializeField][Range(0f, 36f)] private float storeOpenHour = 12f; // 12:00 开店
        [SerializeField][Range(0f, 36f)] private float stopSpawningHour = 22f; // 22:00 停止生成新顾客
        [SerializeField][Range(0f, 36f)] private float storeCloseHour = 23f; // 23:00 强制清场（支持跨日，如27.0表示次日03:00）
        [SerializeField][Range(0f, 36f)] private float nightBGMStartHour = 18f; // 18:00 切换到夜晚BGM

        /// <summary>
        /// 夜晚BGM开始时间（供BGMController读取）
        /// </summary>
        public float NightBGMStartHour => nightBGMStartHour;

        /// <summary> 开店时间（供 LightingManager 等外部系统读取） </summary>
        public float StoreOpenHour => storeOpenHour;

        /// <summary> 关店时间（供 LightingManager 等外部系统读取） </summary>
        public float StoreCloseHour => storeCloseHour;

        [Header("Time Flow")]
        [SerializeField] private float timeScale = 1f; // 时间倍速
        private bool isPaused = false;
        private bool hasStoppedSpawning = false; // 防止重复触发停止生成事件

        [Header("Calendar")]
        [SerializeField] private int startingYear = 2126; // 设计师可在Inspector中调整起始年份
        public int StartingYear => startingYear;

        [Header("Current State")]
        [SerializeField] private int absoluteDay = 1; // absoluteDay 从1开始

        /// <summary>
        /// 当前游戏天数（== absoluteDay，向后兼容）
        /// </summary>
        public int currentDay
        {
            get => absoluteDay;
            set => absoluteDay = value;
        }

        /// <summary>
        /// 当前游戏日期（年/季节/天）
        /// </summary>
        public GameDate CurrentDate => CalendarUtils.AbsoluteDayToDate(absoluteDay, startingYear);

        /// <summary>
        /// 当前季节
        /// </summary>
        public Season CurrentSeason => CurrentDate.season;

        public float currentHour = 6f; // 当前游戏时间（小时）
        public GamePhase currentPhase = GamePhase.BuildPhase;
        public bool isStoreOpen = false;

        [Header("Daily Statistics")]
        public float dailyTotalSale = 0f;
        public float dailyTotalExpenses = 0f;
        public int dailyTotalCustomers = 0;
        public float dailyFameEarned = 0f; // 今日累计获得的Fame
        private int buildPhaseStartMoney = 0; // 建造阶段开始时的金钱快照
        private int lotteryWinnings = 0; // 今日彩票奖金（供结算面板显示）

        [Header("Customer Level Up Tracking")]
        private System.Collections.Generic.List<CustomerLevelUpInfo> todayLevelUps = new System.Collections.Generic.List<CustomerLevelUpInfo>();
        public System.Collections.Generic.IReadOnlyList<CustomerLevelUpInfo> TodayLevelUps => todayLevelUps;

        [Header("UI References")]
        [SerializeField] private Button buildButton; // 建造按钮，营业时禁用
        [SerializeField] private Button openStoreButton; // 开店按钮，营业时禁用
        [SerializeField] private ShelfListPanel buildingListPanel; // 建造面板，开店时需要关闭
        [SerializeField] private StoreToggleAnimator storeToggleAnimator; // 开店/闭店切换动画

        [Header("Gate Settings")]
        [Tooltip("商店大门GameObject（需要包含SpriteRenderer组件）")]
        public GameObject gate;
        [Tooltip("建造阶段的大门sprite")]
        [SerializeField] private Sprite gateBuildPhaseSprite;
        [Tooltip("营业阶段的大门sprite")]
        [SerializeField] private Sprite gateOpenPhaseSprite;
        private SpriteRenderer gateRenderer;

        // Events
        public event Action OnBuildPhaseStart; // 建造阶段开始
        public event Action OnStoreOpen;
        public event Action OnStopSpawning; // 停止生成新顾客
        public event Action OnStoreClose; // 所有顾客离开后触发
        public event Action<DailySettlementData> OnDailySettlement;
        public event Action<int> OnDayChanged;
        public event Action OnBankruptcy; // 破产事件
        public event Action<Season> OnSeasonChanged; // 季节变更事件

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            // 初始化gate的SpriteRenderer
            if (gate != null)
            {
                gateRenderer = gate.GetComponent<SpriteRenderer>();
                if (gateRenderer == null)
                {
                    Debug.LogWarning("[DayLoopManager] Gate GameObject没有SpriteRenderer组件！");
                }
            }
        }

        private void Start()
        {
            // 初始化Unity时间系统
            Time.timeScale = 1f;

            // 游戏开始时进入建造阶段
            currentHour = buildPhaseHour;
            currentPhase = GamePhase.BuildPhase;
            isStoreOpen = false;

            // 记录建造阶段开始时的金钱
            if (ResourceManager.Instance != null)
            {
                buildPhaseStartMoney = ResourceManager.Instance.GetMoney();
            }

            // 设置建造阶段的gate sprite
            SetGateSprite(GamePhase.BuildPhase);

            OnBuildPhaseStart?.Invoke();
        }

        private void Update()
        {
            if (isPaused) return;

            // 建造阶段不计时
            if (currentPhase == GamePhase.BuildPhase)
                return;

            // 营业阶段才计时
            if (currentPhase == GamePhase.OpenPhase)
            {
                // ⚠️ 如果已关店（isStoreOpen == false），停止计时器前进
                if (isStoreOpen)
                {
                    // 计算游戏时间流速
                    float hoursPerSecond = 24f / realSecondsPerDay;
                    currentHour += hoursPerSecond * timeScale * Time.deltaTime;

                    // 检查是否到达停止生成时间
                    if (currentHour >= stopSpawningHour && !hasStoppedSpawning)
                    {
                        hasStoppedSpawning = true;
                        OnStopSpawning?.Invoke();
                        Debug.Log($"[DayLoopManager] 到达停止生成时间 {stopSpawningHour:F1}:00，停止生成新顾客");
                    }

                    // 检查是否到达强制清场时间
                    if (currentHour >= storeCloseHour)
                    {
                        TriggerStoreClose();
                    }
                }
                // 关店后：时间不再前进，但物理时间继续流动（让顾客离开）
            }
        }

        private void TriggerStoreClose()
        {
            isStoreOpen = false;

            // 如果还未触发停止生成，先触发一次
            if (!hasStoppedSpawning)
            {
                hasStoppedSpawning = true;
                OnStopSpawning?.Invoke();
                Debug.Log("[DayLoopManager] 到达关店时间，触发停止生成顾客");
            }

            // ⚠️ 不暂停时间流动，保持当前倍速（让顾客继续移动离开）
            Debug.Log($"[DayLoopManager] 到达关店时间 {storeCloseHour:F1}:00，保持当前时间倍速 {timeScale}x，等待顾客离开");

            // 触发关店事件（禁用时间控制UI）
            OnStoreClose?.Invoke();

            // 等待所有顾客离开后再显示结算界面
            StartCoroutine(WaitForCustomersToLeave());
        }

        /// <summary>
        /// 等待所有顾客离开
        /// </summary>
        private IEnumerator WaitForCustomersToLeave()
        {
            float timeout = 30f; // 超时保护（30秒）
            float elapsed = 0f;

            Debug.Log("[DayLoopManager] 等待所有顾客离开...");

            while (true)
            {
                var remainingCustomers = FindObjectsByType<CustomerAgent>(FindObjectsSortMode.None);
                int count = remainingCustomers.Length;

                if (count == 0)
                {
                    Debug.Log("[DayLoopManager] 所有顾客已离开");
                    break;
                }

                elapsed += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 因为时间已暂停

                if (elapsed >= timeout)
                {
                    Debug.LogWarning($"[DayLoopManager] 顾客清场超时，强制结算（剩余 {count} 个顾客）");

                    // 强制销毁剩余顾客
                    foreach (var agent in remainingCustomers)
                    {
                        Destroy(agent.gameObject);
                    }
                    break;
                }

                yield return new WaitForSecondsRealtime(0.5f); // 使用 Realtime 因为时间已暂停
            }

            // 触发关店事件（所有顾客已离开）
            OnStoreClose?.Invoke();
            Debug.Log("[DayLoopManager] 触发 OnStoreClose 事件");

            // 检查彩票中奖（在结算界面之前）
            yield return StartCoroutine(CheckLotteryWinBeforeSettlement());

            // 所有顾客离开，显示结算界面
            ShowSettlementUI();
        }

        /// <summary>
        /// 在结算面板之前检查彩票开奖结果
        /// 玩家本期买了票就展示面板（中奖/未中奖均展示），等待玩家点击 Continue/Collect 后继续
        /// </summary>
        private IEnumerator CheckLotteryWinBeforeSettlement()
        {
            // 改用 HasPendingDrawResult：玩家本期买了票就要展示结果（无论中没中奖）
            if (LotteryManager.Instance == null || !LotteryManager.Instance.HasPendingDrawResult())
            {
                lotteryWinnings = 0;
                yield break;
            }

            // 重置时间倍速，确保面板动画正常
            SetTimeScale(1f);

            int prize = LotteryManager.Instance.GetPendingPrize();
            int matchCount = LotteryManager.Instance.GetPendingMatchCount();
            int[] drawnNumber = LotteryManager.Instance.GetLastDrawnNumber();
            int[] playerTicket = LotteryManager.Instance.GetPendingPlayerTicket();

            bool collected = false;

            UIManager.Instance?.ShowLotteryWinPanel(
                drawnNumber, playerTicket, matchCount, prize,
                onCollected: () => { collected = true; }
            );

            // 等待玩家点击 Collect 按钮
            while (!collected)
                yield return null;

            lotteryWinnings = prize;
            Debug.Log($"[DayLoopManager] 彩票开奖结果已确认（奖金 ${prize}，将在结算中显示）");
        }

        /// <summary>
        /// 显示结算界面
        /// </summary>
        private void ShowSettlementUI()
        {
            // 重置时间倍速为1x（为下一天准备）
            SetTimeScale(1f);
            Debug.Log("[DayLoopManager] 结算界面显示，时间倍速已重置为1x");

            // 计算每日数据
            DailySettlementData data = CalculateDailySettlement();

            // 触发结算事件
            OnDailySettlement?.Invoke(data);
        }

        private DailySettlementData CalculateDailySettlement()
        {
            // 计算所有建筑的维护费用
            float totalMaintenanceFee = CalculateTotalMaintenanceFee();

            DailySettlementData data = new DailySettlementData();
            data.day = currentDay;
            data.totalSale = dailyTotalSale;
            data.bonusIncome = lotteryWinnings; // 彩票奖金
            lotteryWinnings = 0; // 重置
            data.dailyExpenses = dailyTotalExpenses + totalMaintenanceFee;
            data.dailyIncome = dailyTotalSale + data.bonusIncome - data.dailyExpenses;
            data.totalCustomers = dailyTotalCustomers;
            data.levelUps = todayLevelUps.ToArray(); // 传递升级列表

            // 计算今日金钱变化（建造阶段开始时的钱 - 当前的钱）
            if (ResourceManager.Instance != null)
            {
                data.dailyMoneyChange = buildPhaseStartMoney - ResourceManager.Instance.GetMoney();

                // 获取累计统计
                data.lifetimeIncome = ResourceManager.Instance.GetTotalIncome();
                data.lifetimeExpenses = ResourceManager.Instance.GetTotalExpenses();
            }

            // 今日累计获得的Fame（通过购买实时累积）
            data.fameEarned = Mathf.RoundToInt(dailyFameEarned);

            // 从玩家资源中扣除维护费用
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.SpendMoney(Mathf.RoundToInt(totalMaintenanceFee));
            }

            return data;
        }

        private float CalculateTotalMaintenanceFee()
        {
            float total = 0f;

            // 遍历所有建筑
            var wg = Runtime.WorldGrid.Instance;
            if (wg != null)
            {
                foreach (var building in wg.AllBuildings())
                {
                    total += building.GetMaintenanceFee();
                }
            }

            return total;
        }


        /// <summary>
        /// 开店：从建造阶段切换到营业阶段
        /// 玩家点击按钮调用此方法
        /// </summary>
        public void OpenStore()
        {
            if (currentPhase != GamePhase.BuildPhase)
            {
                Debug.LogWarning("DayLoopManager: 只能在建造阶段开店");
                return;
            }

            // 开店前检查破产（防止玩家在建造阶段花光所有钱后仍开店）
            if (CheckBankruptcy())
            {
                Debug.LogWarning("[DayLoopManager] 无法开店：资金不足，已破产");
                OnBankruptcy?.Invoke();
                PauseTime();
                return;
            }

            // 补货闸门：必须在本次建造阶段至少成功补货一次才能开店（除非场上没货架）
            if (RestockManager.Instance != null && !RestockManager.Instance.IsReadyToOpen())
            {
                Debug.Log("[DayLoopManager] 尚未补货，打开 RestockPanel 并提示");
                RestockManager.Instance.RequestOpenWithoutRestock();
                return;
            }

            // 禁用建造按钮
            if (buildButton != null)
            {
                buildButton.interactable = false;
                Debug.Log("[DayLoopManager] 建造按钮已禁用（营业阶段）");
            }

            // 播放开店切换动画（OpenStoreButton → TimeControlUI）
            if (storeToggleAnimator != null)
            {
                storeToggleAnimator.PlayOpenStoreSequence();
            }
            else if (openStoreButton != null)
            {
                // 无动画器时退回旧逻辑
                openStoreButton.interactable = false;
            }

            // 关闭建造面板（如果打开的话）
            if (buildingListPanel != null && buildingListPanel.IsOpen())
            {
                buildingListPanel.ClosePanel();
                Debug.Log("[DayLoopManager] 建造面板已关闭");
            }

            // 切换到营业阶段
            currentPhase = GamePhase.OpenPhase;
            currentHour = storeOpenHour;
            isStoreOpen = true;

            // 切换gate sprite到营业阶段
            SetGateSprite(GamePhase.OpenPhase);

            // 播放开店音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.STORE_OPEN);
            }

            OnStoreOpen?.Invoke();

            Debug.Log($"[DayLoopManager] Day {currentDay} 开店，时间从 {buildPhaseHour:F1} 跳转到 {storeOpenHour:F1}");
        }

        /// <summary>
        /// 结算完成后调用，进入下一天的建造阶段
        /// </summary>
        public void AdvanceToNextDay()
        {
            // 检查破产
            if (CheckBankruptcy())
            {
                OnBankruptcy?.Invoke();
                PauseTime();
                return;
            }

            // 记录前一天的季节（用于检测季节变更）
            var prevSeason = CurrentSeason;

            currentDay++;
            currentHour = buildPhaseHour;

            // 检测季节变更
            var newSeason = CurrentSeason;
            if (prevSeason != newSeason)
            {
                OnSeasonChanged?.Invoke(newSeason);
                Debug.Log($"[DayLoopManager] 季节变更: {prevSeason} → {newSeason}");
            }

            // 重置每日统计
            dailyTotalSale = 0f;
            dailyTotalExpenses = 0f;
            dailyTotalCustomers = 0;
            dailyFameEarned = 0f;
            todayLevelUps.Clear();

            // 记录新一天建造阶段开始时的金钱
            if (ResourceManager.Instance != null)
            {
                buildPhaseStartMoney = ResourceManager.Instance.GetMoney();
            }

            // 重置状态标志
            hasStoppedSpawning = false;

            // （旧版在此自动补货，已移除——补货改由 RestockPanel 由玩家手动完成）

            // 恢复时间流动（修复第二天时间不计时的bug）
            ResumeTime();

            // 进入建造阶段
            currentPhase = GamePhase.BuildPhase;
            isStoreOpen = false;

            // 切换gate sprite到建造阶段
            SetGateSprite(GamePhase.BuildPhase);

            // 恢复建造按钮可交互状态
            if (buildButton != null)
            {
                buildButton.interactable = true;
                Debug.Log("[DayLoopManager] 建造按钮已启用（建造阶段）");
            }

            // 播放闭店切换动画（TimeControlUI → OpenStoreButton）
            if (storeToggleAnimator != null)
            {
                storeToggleAnimator.PlayCloseStoreSequence();
            }
            else if (openStoreButton != null)
            {
                // 无动画器时退回旧逻辑
                openStoreButton.interactable = true;
            }

            OnDayChanged?.Invoke(currentDay);
            OnBuildPhaseStart?.Invoke();

            Debug.Log($"[DayLoopManager] 进入 Day {currentDay} 建造阶段，时间设为 {buildPhaseHour:F1}:00");
        }

        /// <summary>
        /// 检查破产条件
        /// </summary>
        private bool CheckBankruptcy()
        {
            if (ResourceManager.Instance != null)
            {
                return ResourceManager.Instance.money < 0;
            }
            return false;
        }

        // 时间控制
        public void PauseTime()
        {
            isPaused = true;
            Time.timeScale = 0f; // 完全冻结Unity时间系统
        }

        public void ResumeTime()
        {
            isPaused = false;
            Time.timeScale = timeScale; // 恢复到当前倍速
        }

        public void SetTimeScale(float scale)
        {
            timeScale = Mathf.Max(0, scale);
            // 同时设置Unity的Time.timeScale（影响所有基于Time.deltaTime的系统）
            if (!isPaused)
            {
                Time.timeScale = timeScale;
            }
        }

        // 统计记录
        public void RecordSale(float amount)
        {
            dailyTotalSale += amount;
        }

        public void RecordExpense(float amount)
        {
            dailyTotalExpenses += amount;
        }

        public void RecordCustomerVisit()
        {
            dailyTotalCustomers++;
        }

        public void RecordCustomerLevelUp(CustomerLevelUpInfo info)
        {
            todayLevelUps.Add(info);
        }

        /// <summary>
        /// 记录Fame获取（由CustomerInteraction.TryPurchase调用）
        /// </summary>
        public void RecordFame(float amount)
        {
            dailyFameEarned += amount;
        }

        // 获取格式化时间字符串（将扩展时间转换为24小时制显示）
        public string GetFormattedTime()
        {
            // 将扩展时间转换为24小时制显示
            float displayHour = currentHour;
            if (currentHour >= 24f)
            {
                displayHour = currentHour - 24f;
            }

            int hour = Mathf.FloorToInt(displayHour);
            int minute = Mathf.FloorToInt((displayHour - hour) * 60);
            return $"{hour:00}:{minute:00}";
        }

        /// <summary>
        /// 根据游戏阶段设置gate的sprite
        /// </summary>
        private void SetGateSprite(GamePhase phase)
        {
            if (gateRenderer == null) return;

            switch (phase)
            {
                case GamePhase.BuildPhase:
                    if (gateBuildPhaseSprite != null)
                    {
                        gateRenderer.sprite = gateBuildPhaseSprite;
                        Debug.Log("[DayLoopManager] Gate切换为建造阶段sprite");
                    }
                    break;

                case GamePhase.OpenPhase:
                    if (gateOpenPhaseSprite != null)
                    {
                        gateRenderer.sprite = gateOpenPhaseSprite;
                        Debug.Log("[DayLoopManager] Gate切换为营业阶段sprite");
                    }
                    break;
            }
        }
    }

    [System.Serializable]
    public struct DailySettlementData
    {
        public int day;
        public float totalSale;              // 今日开店收益（当日销售总金额）
        public float bonusIncome;            // 今日额外收益（彩票奖金等）
        public float dailyExpenses;          // 今日维护费
        public float dailyIncome;            // 今日净收入（totalSale + bonusIncome - dailyExpenses）
        public int dailyMoneyChange;         // 今日金钱变化（建造阶段开始时的钱 - 结算时的钱）
        public int lifetimeIncome;           // 总收入（累计，仅来自顾客结账）
        public int lifetimeExpenses;         // 总开支（累计，所有花费）
        public int totalCustomers;           // 今日访客数
        public int fameEarned;               // 今日获得的声望值
        public CustomerLevelUpInfo[] levelUps; // 当日升级的顾客列表
    }
}
