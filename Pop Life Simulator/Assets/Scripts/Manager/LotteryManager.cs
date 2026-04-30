using System;
using UnityEngine;
using PopLife.Data;

namespace PopLife
{
    /// <summary>
    /// 彩票持久化数据
    /// </summary>
    [Serializable]
    public class LotterySaveData
    {
        public int[] currentTicket;       // 当前持有票号（null=无票）
        public int ticketPurchaseDay;     // 购买日 absoluteDay
        public int[] lastDrawnNumber;     // 最近一次开奖号码
        public int lastDrawDay;           // 最近开奖的 absoluteDay
        public int totalWinnings;         // 历史总奖金
        public int totalTicketsBought;    // 历史总购买次数

        // 待领取奖金（持久化，防止退出游戏丢失）
        public int pendingPrize;          // 待领取奖金额
        public int pendingMatchCount;     // 待领取的匹配数
        public int[] pendingPlayerTicket; // 开奖时暂存的玩家票号
    }

    /// <summary>
    /// 彩票系统核心管理器
    /// 负责购票、开奖、中奖判定、News Ticker 播报和持久化
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class LotteryManager : MonoBehaviour
    {
        public static LotteryManager Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private LotteryConfig config;

        [Header("Debug Info")]
        [SerializeField] private bool isDrawDayToday;
        [SerializeField] private int daysUntilDraw;

        // 持久化数据
        private LotterySaveData saveData = new();

        // 运行时状态（不持久化）
        private bool hasAnnouncedToday;

        // ===== 事件 =====
        public event Action<int[]> OnDrawCompleted;     // 开奖完成（传递中奖号码）
        public event Action<int, int> OnPlayerWon;      // 玩家中奖（匹配数, 奖金额）
        public event Action OnTicketPurchased;           // 购票成功

        // ===== ES3 常量 =====
        private const string ES3_KEY = "lotteryData";
        private const string ES3_FILE = "Lottery.es3";

        #region 生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            LoadState();
        }

        private void Start()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnNewDayStart;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpened;
            }
        }

        private void OnDestroy()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnNewDayStart;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpened;
            }

            if (Instance == this) Instance = null;
        }

        #endregion

        #region 周期计算

        /// <summary>
        /// 判断指定日期是否为开奖日
        /// </summary>
        public bool IsDrawDay(int absoluteDay)
        {
            return ((absoluteDay - 1) % config.DrawCycleDays) == config.DrawDayOffset;
        }

        /// <summary>
        /// 从指定日期算起的下一个开奖日
        /// </summary>
        public int GetNextDrawDay(int fromDay)
        {
            int offset = (fromDay - 1) % config.DrawCycleDays;
            int daysUntil = config.DrawDayOffset - offset;
            if (daysUntil <= 0) daysUntil += config.DrawCycleDays;
            return fromDay + daysUntil;
        }

        /// <summary>
        /// 距离下次开奖的天数
        /// </summary>
        public int GetDaysUntilDraw()
        {
            int today = DayLoopManager.Instance?.currentDay ?? 1;
            if (IsDrawDay(today)) return 0;
            return GetNextDrawDay(today) - today;
        }

        #endregion

        #region 购票

        /// <summary>
        /// 检查当前是否可以购票
        /// </summary>
        public bool CanPurchaseTicket()
        {
            var dlm = DayLoopManager.Instance;
            if (dlm == null || dlm.currentPhase != GamePhase.BuildPhase) return false;
            if (IsDrawDay(dlm.currentDay)) return false;
            if (HasTicketForCurrentCycle()) return false;
            return ResourceManager.Instance != null
                && ResourceManager.Instance.CanAfford(config.TicketCost, 0);
        }

        /// <summary>
        /// 检查玩家是否已购买当前周期的彩票
        /// </summary>
        public bool HasTicketForCurrentCycle()
        {
            if (saveData.currentTicket == null || saveData.currentTicket.Length == 0) return false;
            int today = DayLoopManager.Instance?.currentDay ?? 1;

            // 计算当前周期的起始日
            int nextDraw = IsDrawDay(today) ? today : GetNextDrawDay(today);
            int cycleStart = nextDraw - config.DrawCycleDays + 1;

            return saveData.ticketPurchaseDay >= cycleStart
                && saveData.ticketPurchaseDay <= nextDraw;
        }

        /// <summary>
        /// 尝试购买彩票
        /// </summary>
        public bool TryPurchaseTicket(int[] digits)
        {
            if (!CanPurchaseTicket()) return false;
            if (digits == null || digits.Length != config.DigitCount) return false;

            // 验证每位数字在有效范围内
            foreach (int d in digits)
            {
                if (d < 0 || d > config.MaxDigitValue) return false;
            }

            ResourceManager.Instance.SpendMoney(config.TicketCost);
            saveData.currentTicket = (int[])digits.Clone();
            saveData.ticketPurchaseDay = DayLoopManager.Instance.currentDay;
            saveData.totalTicketsBought++;
            SaveState();

            OnTicketPurchased?.Invoke();
            return true;
        }

        #endregion

        #region 开奖 (OnBuildPhaseStart)

        /// <summary>
        /// 每日开始时检查是否为开奖日
        /// </summary>
        private void OnNewDayStart()
        {
            hasAnnouncedToday = false;

            int today = DayLoopManager.Instance.currentDay;

            // 更新 debug 信息
            isDrawDayToday = IsDrawDay(today);
            daysUntilDraw = GetDaysUntilDraw();

            if (!IsDrawDay(today)) return;

            // 防重入：同一天不重复开奖（防止场景重载/热重载导致二次开奖覆盖号码）
            if (saveData.lastDrawDay == today) return;

            // 生成开奖号码
            int[] drawnNumber = GenerateDrawNumber();
            saveData.lastDrawnNumber = drawnNumber;
            saveData.lastDrawDay = today;

            Debug.Log($"[LotteryManager] 开奖日 Day {today}，号码: {string.Join("", drawnNumber)}");

            // 判定中奖（在清空彩票前）
            saveData.pendingPrize = 0;
            saveData.pendingMatchCount = 0;
            saveData.pendingPlayerTicket = null;

            if (saveData.currentTicket != null && saveData.currentTicket.Length > 0)
            {
                saveData.pendingPlayerTicket = (int[])saveData.currentTicket.Clone();

                int matches = CountMatches(saveData.currentTicket, drawnNumber);
                int prize = config.GetPrize(matches);
                saveData.pendingMatchCount = matches;
                saveData.pendingPrize = prize;

                Debug.Log($"[LotteryManager] 玩家票号: {string.Join("", saveData.currentTicket)}，匹配 {matches}/4，奖金: ${prize}");
            }

            // 清空已使用的彩票
            saveData.currentTicket = null;
            SaveState();

            OnDrawCompleted?.Invoke(drawnNumber);
        }

        #endregion

        #region News Ticker 播报 (OnStoreOpen)

        /// <summary>
        /// 开店时插入彩票开奖播报
        /// </summary>
        private void OnStoreOpened()
        {
            if (hasAnnouncedToday) return;

            int today = DayLoopManager.Instance.currentDay;
            if (!IsDrawDay(today)) return;
            if (saveData.lastDrawnNumber == null || saveData.lastDrawnNumber.Length == 0) return;

            hasAnnouncedToday = true;

            // 构建带颜色的数字字符串
            string numberStr = string.Join(" ", saveData.lastDrawnNumber);
            string message = $"This week's Lottery Number is: <color=yellow>{numberStr}</color>";

            // 使用优先级插入方法（插入到队首）
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPriorityNewsMessage(message);
            }
        }

        #endregion

        #region 中奖领取

        /// <summary>
        /// 是否有待领取的奖金（中奖语义：prize > 0）
        /// </summary>
        public bool HasPendingWin() => saveData.pendingPrize > 0;

        /// <summary>
        /// 是否有待展示的开奖结果（玩家本期买了票，无论中奖与否）
        /// </summary>
        public bool HasPendingDrawResult()
            => saveData.pendingPlayerTicket != null && saveData.pendingPlayerTicket.Length > 0;

        public int GetPendingPrize() => saveData.pendingPrize;
        public int GetPendingMatchCount() => saveData.pendingMatchCount;
        public int[] GetPendingPlayerTicket() => saveData.pendingPlayerTicket;
        public int[] GetLastDrawnNumber() => saveData.lastDrawnNumber;

        /// <summary>
        /// 确认本期开奖结果（中奖则计入营业收入，未中奖也清理 pending 数据）
        /// </summary>
        public void CollectPrize()
        {
            // 没有待展示的结果且无奖金，直接返回
            if (!HasPendingDrawResult() && saveData.pendingPrize <= 0) return;

            int prize = saveData.pendingPrize;
            int matchCount = saveData.pendingMatchCount;

            if (prize > 0)
            {
                ResourceManager.Instance?.AddMoney(prize);
                saveData.totalWinnings += prize;
            }

            // 清零并持久化，防止重复领取/重复展示
            saveData.pendingPrize = 0;
            saveData.pendingMatchCount = 0;
            saveData.pendingPlayerTicket = null;
            SaveState();

            // OnPlayerWon 仅在真实中奖时触发，保留事件原语义
            if (prize > 0)
                OnPlayerWon?.Invoke(matchCount, prize);

            Debug.Log($"[LotteryManager] 本期开奖结果已确认（{matchCount}/4 匹配，奖金 ${prize}）");
        }

        #endregion

        #region 公共查询 API

        public int[] GetCurrentTicket() => saveData.currentTicket;
        public int GetTicketCost() => config != null ? config.TicketCost : 50;
        public int GetDigitCount() => config != null ? config.DigitCount : 4;
        public int GetMaxDigitValue() => config != null ? config.MaxDigitValue : 9;
        public bool IsDrawDayToday() => IsDrawDay(DayLoopManager.Instance?.currentDay ?? 1);
        public int GetTotalWinnings() => saveData.totalWinnings;
        public int GetTotalTicketsBought() => saveData.totalTicketsBought;
        public LotteryConfig GetConfig() => config;

        #endregion

        #region 工具方法

        /// <summary>
        /// 生成随机开奖号码
        /// </summary>
        private int[] GenerateDrawNumber()
        {
            int[] number = new int[config.DigitCount];
            for (int i = 0; i < config.DigitCount; i++)
            {
                number[i] = UnityEngine.Random.Range(0, config.MaxDigitValue + 1);
            }
            return number;
        }

        /// <summary>
        /// 计算位置对应的匹配数
        /// </summary>
        private int CountMatches(int[] ticket, int[] drawn)
        {
            int matches = 0;
            int len = Mathf.Min(ticket.Length, drawn.Length);
            for (int i = 0; i < len; i++)
            {
                if (ticket[i] == drawn[i]) matches++;
            }
            return matches;
        }

        #endregion

        #region 持久化

        private void SaveState()
        {
            ES3.Save(ES3_KEY, saveData, ES3_FILE);
        }

        private void LoadState()
        {
            if (ES3.FileExists(ES3_FILE) && ES3.KeyExists(ES3_KEY, ES3_FILE))
            {
                saveData = ES3.Load<LotterySaveData>(ES3_KEY, ES3_FILE);
            }
            else
            {
                saveData = new LotterySaveData();
            }
        }

        /// <summary>
        /// 清除存档（供 GameStateManager 调用）
        /// </summary>
        public static void ClearSave()
        {
            if (ES3.FileExists(ES3_FILE))
            {
                ES3.DeleteFile(ES3_FILE);
            }
        }

        /// <summary>
        /// 清除存档并重置内存状态
        /// 必须同时重置内存数据，否则后续 SaveState 会把陈旧数据写回磁盘
        /// </summary>
        public void ClearSaveAndResetState()
        {
            saveData = new LotterySaveData();
            hasAnnouncedToday = false;
            isDrawDayToday = false;
            daysUntilDraw = 0;
            ClearSave();
        }

        #endregion
    }
}
