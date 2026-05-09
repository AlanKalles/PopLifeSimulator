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
        [SerializeField] private int totalIncome = 0;      // 总收入（仅来自顾客结账）
        [SerializeField] private int totalExpenses = 0;    // 总开支（SpendMoney的累计）

        // Fame小数累积器（用于累积小数fame直到满1）
        private float fameAccumulator = 0f;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            // 订阅建筑变化事件以更新 Store Appeal
            ConstructionManager.OnBuildingPlacedOrDestroyed += RecalculateStoreAppeal;
            // 订阅日历数据变化（季节事件 / Buffer 激活/到期 → Appeal 修饰器变化）
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnCalendarDataChanged += RecalculateStoreAppeal;
        }

        void OnDestroy()
        {
            ConstructionManager.OnBuildingPlacedOrDestroyed -= RecalculateStoreAppeal;
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnCalendarDataChanged -= RecalculateStoreAppeal;
        }

        #region Getters
        public int GetFame() => fame;
        public int GetMoney() => money;
        public int GetTotalIncome() => totalIncome;
        public int GetTotalExpenses() => totalExpenses;
        public int GetStoreAppeal() => storeAppeal;
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
        /// 直接增加金钱（不计入总收入）
        /// 用于：拆除建筑返还、撤销建造返还等非营业收入
        /// </summary>
        public void RefundMoney(int amount)
        {
            money += amount;
            // 不计入 totalIncome
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// Midori 赞助补助到账（不计入 totalIncome 统计）
        /// 用于：还债失败时的破产救济补助
        /// </summary>
        public void AddSponsorshipMoney(int amount)
        {
            if (amount <= 0) return;
            money += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>
        /// 增加声望（整数版本）
        /// </summary>
        public void AddFame(int amount)
        {
            fame += amount;
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
                fameAccumulator -= wholeFame;
                OnFameChanged?.Invoke(fame);
            }
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
