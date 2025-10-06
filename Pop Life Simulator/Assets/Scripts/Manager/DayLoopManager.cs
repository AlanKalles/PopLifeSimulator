using System;
using System.Collections;
using UnityEngine;
using PopLife.Customers.Runtime;

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
        [SerializeField] private float buildPhaseHour = 6f; // 6:00 建造阶段
        [SerializeField] private float storeOpenHour = 12f; // 12:00
        [SerializeField] private float storeCloseHour = 23f; // 23:00
        [SerializeField] private float settlementHour = 23f; // 23:00 开始结算

        [Header("Time Flow")]
        [SerializeField] private float timeScale = 1f; // 时间倍速
        private bool isPaused = false;

        [Header("Current State")]
        public int currentDay = 1;
        public float currentHour = 6f; // 当前游戏时间（小时）
        public GamePhase currentPhase = GamePhase.BuildPhase;
        public bool isStoreOpen = false;

        [Header("Daily Statistics")]
        public float dailyTotalSale = 0f;
        public float dailyTotalExpenses = 0f;
        public int dailyTotalCustomers = 0;

        // Events
        public event Action OnBuildPhaseStart; // 建造阶段开始
        public event Action OnStoreOpen;
        public event Action OnStoreClose;
        public event Action<DailySettlementData> OnDailySettlement;
        public event Action<int> OnDayChanged;
        public event Action OnBankruptcy; // 破产事件

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
        }

        private void Start()
        {
            // 游戏开始时进入建造阶段
            currentHour = buildPhaseHour;
            currentPhase = GamePhase.BuildPhase;
            isStoreOpen = false;
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
                // 计算游戏时间流速
                float hoursPerSecond = 24f / realSecondsPerDay;
                currentHour += hoursPerSecond * timeScale * Time.deltaTime;

                // 检查是否到达营业结束时间
                if (isStoreOpen && currentHour >= settlementHour)
                {
                    TriggerDailySettlement();
                }
            }
        }

        private void TriggerDailySettlement()
        {
            isStoreOpen = false;
            OnStoreClose?.Invoke(); // 触发 CustomerSpawner.StopSpawning()

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
                    Debug.Log("[DayLoopManager] 所有顾客已离开，显示结算界面");
                    break;
                }

                elapsed += Time.deltaTime;

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

                yield return new WaitForSeconds(0.5f); // 每0.5秒检查一次
            }

            // 所有顾客离开，显示结算界面
            ShowSettlementUI();
        }

        /// <summary>
        /// 显示结算界面
        /// </summary>
        private void ShowSettlementUI()
        {
            // 计算每日数据
            DailySettlementData data = CalculateDailySettlement();

            // 触发结算事件
            OnDailySettlement?.Invoke(data);

            // 暂停时间
            PauseTime();
        }

        private DailySettlementData CalculateDailySettlement()
        {

            // 计算所有建筑的维护费用
            float totalMaintenanceFee = CalculateTotalMaintenanceFee();

            DailySettlementData data = new DailySettlementData();
            data.day = currentDay;
            data.totalSale = dailyTotalSale;
            data.totalExpenses = dailyTotalExpenses + totalMaintenanceFee;
            data.dailyIncome = dailyTotalSale - data.totalExpenses;
            data.totalCustomers = dailyTotalCustomers;

            // 计算声誉奖励 (可以根据设计调整公式)
            data.fameEarned = CalculateFameReward(data.dailyIncome, data.totalCustomers);

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

            // 遍历所有楼层的所有建筑
            var floors = FindObjectsByType<Runtime.FloorGrid>(FindObjectsSortMode.None);
            foreach (var floor in floors)
            {
                foreach (var building in floor.AllBuildings())
                {
                    total += building.GetMaintenanceFee();
                }
            }

            return total;
        }

        private int CalculateFameReward(float income, int customers)
        {
            // 简单公式：基于收入和顾客数量
            // 可以根据游戏平衡性调整
            float baseReward = income * 0.01f + customers * 0.5f;
            return Mathf.Max(0, Mathf.RoundToInt(baseReward));
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

            // 切换到营业阶段
            currentPhase = GamePhase.OpenPhase;
            currentHour = storeOpenHour;
            isStoreOpen = true;

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

            currentDay++;
            currentHour = buildPhaseHour;

            // 重置每日统计
            dailyTotalSale = 0f;
            dailyTotalExpenses = 0f;
            dailyTotalCustomers = 0;

            // 进入建造阶段
            currentPhase = GamePhase.BuildPhase;
            isStoreOpen = false;

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
        public void PauseTime() => isPaused = true;
        public void ResumeTime() => isPaused = false;
        public void SetTimeScale(float scale) => timeScale = Mathf.Max(0, scale);

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

        // 获取格式化时间字符串
        public string GetFormattedTime()
        {
            int hour = Mathf.FloorToInt(currentHour);
            int minute = Mathf.FloorToInt((currentHour - hour) * 60);
            return $"{hour:00}:{minute:00}";
        }
    }

    [System.Serializable]
    public struct DailySettlementData
    {
        public int day;
        public float totalSale;
        public float totalExpenses;
        public float dailyIncome;
        public int totalCustomers;
        public int fameEarned;
    }
}
