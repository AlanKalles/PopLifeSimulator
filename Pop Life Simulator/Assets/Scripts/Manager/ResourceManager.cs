using System;
using UnityEngine;
using PopLife.Runtime;

namespace PopLife
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance;

        [Header("Current Resources")]
        public int money;
        public int fame;

        /// <summary>
        /// 金钱变化时触发，参数为当前金钱值
        /// </summary>
        public event Action<int> OnMoneyChanged;

        /// <summary>
        /// 声望变化时触发，参数为当前 fame 值
        /// </summary>
        public event Action<int> OnFameChanged;

        [Header("Store Appeal")]
        [SerializeField] private int storeAppeal;

        /// <summary>
        /// Store Appeal 变化时触发，参数为当前 storeAppeal 值
        /// </summary>
        public event Action<int> OnStoreAppealChanged;

        [Header("Lifetime Statistics")]
        [SerializeField] private int totalIncome = 0;             // 总收入（仅来自顾客结账）
        [SerializeField] private int totalExpenses = 0;           // 总开支（SpendMoney的累计）
        [SerializeField] private int totalNonSaleIncome = 0;      // 累计非营业收入（refund + quest + sponsorship）
        [SerializeField] private int lifetimeFameGained = 0;      // 累计获得fame（gross，仅累加）
        [SerializeField] private int lifetimeCustomersEntered = 0; // 累计真实进店顾客数

        // Daily 分类计数器（OnBuildPhaseStart 清零）
        private int dailyMaintenanceExpense = 0;
        private int dailyRestockExpense = 0;
        private int dailyConstructionExpense = 0;
        private int dailyQuestRewardIncome = 0;
        private int dailyRefundIncome = 0;

        // Fame小数累积器（用于累积小数fame直到满1）
        private float fameAccumulator = 0f;

        // ES3 持久化
        private const string ES3_FILE = "ResourceState.es3";
        private bool loaded = false;

        void Awake()
        {
            Instance = this;
            Load();
        }

        void Start()
        {
            // 订阅建筑变化事件以更新 Store Appeal
            ConstructionManager.OnBuildingPlacedOrDestroyed += RecalculateStoreAppeal;
            // 订阅日历数据变化（季节事件 / Buffer 激活/到期 → Appeal 修饰器变化）
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnCalendarDataChanged += RecalculateStoreAppeal;
            // 订阅每日建造阶段开始：重置 daily 计数 + 持久化 lifetime
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnBuildPhaseStart += OnDailyBoundary;
        }

        void OnDestroy()
        {
            ConstructionManager.OnBuildingPlacedOrDestroyed -= RecalculateStoreAppeal;
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnCalendarDataChanged -= RecalculateStoreAppeal;
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnBuildPhaseStart -= OnDailyBoundary;
        }

        void OnApplicationQuit()
        {
            Save();
        }

        /// <summary>
        /// 每日建造阶段开始时：重置 daily 计数 + 持久化 lifetime 字段
        /// </summary>
        private void OnDailyBoundary()
        {
            dailyMaintenanceExpense = 0;
            dailyRestockExpense = 0;
            dailyConstructionExpense = 0;
            dailyQuestRewardIncome = 0;
            dailyRefundIncome = 0;
            Save();
        }

        #region Getters
        public int GetFame() => fame;
        public int GetMoney() => money;
        public int GetTotalIncome() => totalIncome;
        public int GetTotalExpenses() => totalExpenses;
        public int GetStoreAppeal() => storeAppeal;

        // 新增 Lifetime/Daily 查询接口（供结算面板使用）
        public int GetLifetimeAllIncome() => totalIncome + totalNonSaleIncome;
        public int GetLifetimeFameGained() => lifetimeFameGained;
        public int GetLifetimeCustomersEntered() => lifetimeCustomersEntered;
        public int GetDailyRestockExpense() => dailyRestockExpense;
        public int GetDailyConstructionExpense() => dailyConstructionExpense;
        public int GetDailyMaintenanceExpense() => dailyMaintenanceExpense;
        public int GetDailyQuestRewardIncome() => dailyQuestRewardIncome;
        public int GetDailyRefundIncome() => dailyRefundIncome;
        #endregion

        #region Resource Checks
        /// <summary>
        /// 检查玩家是否有足够的资源
        /// </summary>
        public bool CanAfford(int moneyCost, int fameCost)
        {
            return money >= moneyCost && fame >= fameCost;
        }
        #endregion

        #region Spend Methods
        /// <summary>
        /// 花费金钱和声望（会记录金钱开支到总开支）
        /// </summary>
        public void Spend(int moneyCost, int fameCost)
        {
            money -= moneyCost;
            fame -= fameCost;
            totalExpenses += moneyCost; // 记录金钱开支
            if (moneyCost != 0) OnMoneyChanged?.Invoke(money);
            if (fameCost != 0) OnFameChanged?.Invoke(fame);
        }

        /// <summary>
        /// 仅花费金钱（会记录到总开支）
        /// 用于：维护费、建造成本、移动成本等
        /// </summary>
        public void SpendMoney(int amount)
        {
            money -= amount;
            totalExpenses += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// 花费金钱（计入 Restock 类别 + 总开支）
        /// 用于：货架补货
        /// </summary>
        public void SpendOnRestock(int amount)
        {
            SpendMoney(amount);
            dailyRestockExpense += amount;
        }

        /// <summary>
        /// 花费金钱（计入 Construction 类别 + 总开支）
        /// 用于：建造、移动、升级建筑
        /// </summary>
        public void SpendOnConstruction(int amount)
        {
            SpendMoney(amount);
            dailyConstructionExpense += amount;
        }

        /// <summary>
        /// 花费金钱（计入 Maintenance 类别 + 总开支）
        /// 用于：每日维护费
        /// </summary>
        public void SpendOnMaintenance(int amount)
        {
            SpendMoney(amount);
            dailyMaintenanceExpense += amount;
        }

        /// <summary>
        /// 仅花费声望
        /// </summary>
        public void SpendFame(int amount)
        {
            fame -= amount;
            OnFameChanged?.Invoke(fame);
        }
        #endregion

        #region Add Methods
        /// <summary>
        /// 增加金钱（会记录到总收入）
        /// ⚠️ 仅用于顾客结账收入！
        /// </summary>
        public void AddMoney(int amount)
        {
            money += amount;
            totalIncome += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// 直接增加金钱（拆除建筑/撤销建造返还）
        /// 计入 lifetime 非营业收入 + daily Refund 分类
        /// </summary>
        public void RefundMoney(int amount)
        {
            money += amount;
            totalNonSaleIncome += amount;
            dailyRefundIncome += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// 任务奖励到账
        /// 计入 lifetime 非营业收入 + daily QuestReward 分类
        /// </summary>
        public void AddQuestRewardMoney(int amount)
        {
            if (amount <= 0) return;
            money += amount;
            totalNonSaleIncome += amount;
            dailyQuestRewardIncome += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// Midori 赞助补助到账
        /// 计入 lifetime 非营业收入（结算面板的 sponsorshipAmount 单独追踪不在此处）
        /// </summary>
        public void AddSponsorshipMoney(int amount)
        {
            if (amount <= 0) return;
            money += amount;
            totalNonSaleIncome += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// 增加声望（整数版本）
        /// </summary>
        public void AddFame(int amount)
        {
            fame += amount;
            if (amount > 0) lifetimeFameGained += amount; // 仅正向累计 gross lifetime
            if (amount != 0) OnFameChanged?.Invoke(fame);
        }

        /// <summary>
        /// 增加声望（浮点数版本，累积小数直到满1）
        /// </summary>
        public void AddFame(float amount)
        {
            fameAccumulator += amount;
            int wholeFame = Mathf.FloorToInt(fameAccumulator);
            if (wholeFame > 0)
            {
                fame += wholeFame;
                lifetimeFameGained += wholeFame; // 仅正向累计 gross lifetime
                fameAccumulator -= wholeFame;
                OnFameChanged?.Invoke(fame);
            }
        }

        /// <summary>
        /// 真实进店顾客 +1（由 CustomerPresenceService 在顾客入店时调用）
        /// 仅追踪 lifetime 累计，daily 入店数仍由 CustomerPresenceService.EnteredTodayCount 维护
        /// </summary>
        public void IncrementLifetimeCustomers()
        {
            lifetimeCustomersEntered++;
        }
        #endregion

        #region ES3 Persistence
        /// <summary>
        /// 保存 lifetime 字段到 ES3（不保存 daily 字段，daily 是会话临时）
        /// </summary>
        private void Save()
        {
            try
            {
                ES3.Save("money", money, ES3_FILE);
                ES3.Save("fame", fame, ES3_FILE);
                ES3.Save("totalIncome", totalIncome, ES3_FILE);
                ES3.Save("totalExpenses", totalExpenses, ES3_FILE);
                ES3.Save("totalNonSaleIncome", totalNonSaleIncome, ES3_FILE);
                ES3.Save("lifetimeFameGained", lifetimeFameGained, ES3_FILE);
                ES3.Save("lifetimeCustomersEntered", lifetimeCustomersEntered, ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceManager] ES3 Save 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从 ES3 加载 lifetime 字段；首次启动文件不存在时保留 Inspector 默认值
        /// </summary>
        private void Load()
        {
            if (loaded) return;
            try
            {
                money = ES3.Load("money", ES3_FILE, money);
                fame = ES3.Load("fame", ES3_FILE, fame);
                totalIncome = ES3.Load("totalIncome", ES3_FILE, totalIncome);
                totalExpenses = ES3.Load("totalExpenses", ES3_FILE, totalExpenses);
                totalNonSaleIncome = ES3.Load("totalNonSaleIncome", ES3_FILE, totalNonSaleIncome);
                lifetimeFameGained = ES3.Load("lifetimeFameGained", ES3_FILE, lifetimeFameGained);
                lifetimeCustomersEntered = ES3.Load("lifetimeCustomersEntered", ES3_FILE, lifetimeCustomersEntered);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceManager] ES3 Load 失败（首次运行属正常情况）: {e.Message}");
            }
            loaded = true;
        }
        #endregion

        #region Store Appeal
        /// <summary>
        /// 遍历所有楼层的所有货架，累加 appeal 值
        /// 触发时机：建造/拆除/移动/升级货架
        /// </summary>
        public void RecalculateStoreAppeal()
        {
            var wg = WorldGrid.Instance;
            if (wg == null) return;

            int total = 0;
            foreach (var shelf in wg.AllShelves())
            {
                var hostTile = WorldGrid.ResolveHostTile(shelf);
                if (hostTile == null || !ConstructionGuards.IsStoreOwnedByPlayer(hostTile.StoreId))
                    continue;

                total += Mathf.RoundToInt(shelf.GetAppeal());
            }
            storeAppeal = total;
            OnStoreAppealChanged?.Invoke(storeAppeal);
        }
        #endregion
    }
}
