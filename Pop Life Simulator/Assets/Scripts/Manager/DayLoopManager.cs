using System;
using UnityEngine;

namespace PopLife
{
    public class DayLoopManager : MonoBehaviour
    {
        public static DayLoopManager Instance;

        [Header("Time Settings")]
        [SerializeField] private float realSecondsPerDay = 30f; // 现实30秒 = 游戏1天
        [SerializeField] private float storeOpenHour = 12f; // 12:00
        [SerializeField] private float storeCloseHour = 23f; // 23:00
        [SerializeField] private float settlementHour = 23f; // 23:00 开始结算

        [Header("Time Flow")]
        [SerializeField] private float timeScale = 1f; // 时间倍速
        private bool isPaused = false;

        [Header("Current State")]
        public int currentDay = 1;
        public float currentHour = 12f; // 当前游戏时间（小时）
        public bool isStoreOpen = true;

        [Header("Daily Statistics")]
        public float dailyTotalSale = 0f;
        public float dailyTotalExpenses = 0f;
        public int dailyTotalCustomers = 0;

        // Events
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
            currentHour = storeOpenHour;
            isStoreOpen = true;
            OnStoreOpen?.Invoke();
        }

        private void Update()
        {
            if (isPaused) return;

            // 计算游戏时间流速
            float hoursPerSecond = 24f / realSecondsPerDay;
            currentHour += hoursPerSecond * timeScale * Time.deltaTime;

            // 检查是否到达营业结束时间
            if (isStoreOpen && currentHour >= settlementHour)
            {
                TriggerDailySettlement();
            }
        }

        private void TriggerDailySettlement()
        {
            isStoreOpen = false;
            OnStoreClose?.Invoke();

            // 计算每日数据
            DailySettlementData data = CalculateDailySettlement();

            // 触发结算事件
            OnDailySettlement?.Invoke(data);

            // 暂停时间，等待UI关闭后再继续
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
            var floors = FindObjectsOfType<Runtime.FloorGrid>();
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
        /// 结算完成后调用，进入下一天
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
            currentHour = storeOpenHour;

            // 重置每日统计
            dailyTotalSale = 0f;
            dailyTotalExpenses = 0f;
            dailyTotalCustomers = 0;

            isStoreOpen = true;

            OnDayChanged?.Invoke(currentDay);
            OnStoreOpen?.Invoke();

            ResumeTime();
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
