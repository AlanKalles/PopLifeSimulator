using UnityEngine;

namespace PopLife.Data
{
    /// <summary>
    /// 彩票系统配置 - ScriptableObject
    /// 控制彩票的票价、奖金、开奖周期等参数
    /// </summary>
    [CreateAssetMenu(fileName = "LotteryConfig", menuName = "PopLife/Lottery/Lottery Config")]
    public class LotteryConfig : ScriptableObject
    {
        [Header("Basic Settings")]
        [Tooltip("单张彩票价格")]
        [SerializeField] private int ticketCost = 50;

        [Tooltip("每张彩票的数字位数")]
        [SerializeField] private int digitCount = 4;

        [Tooltip("每位数字的最大值（含）")]
        [SerializeField] private int maxDigitValue = 9;

        [Header("Draw Schedule")]
        [Tooltip("开奖周期（每N天一次）")]
        [SerializeField] private int drawCycleDays = 4;

        [Tooltip("在周期中的第几天开奖（0-based offset，3表示周期最后一天）")]
        [SerializeField] private int drawDayOffset = 3;

        [Header("Prize Tiers")]
        [Tooltip("大奖 - 4/4 全中")]
        [SerializeField] private int grandPrize = 50000;

        [Tooltip("二等奖 - 3/4 匹配")]
        [SerializeField] private int secondPrize = 500;

        [Tooltip("三等奖 - 2/4 匹配")]
        [SerializeField] private int thirdPrize = 100;

        // ===== Public Getters =====
        public int TicketCost => ticketCost;
        public int DigitCount => digitCount;
        public int MaxDigitValue => maxDigitValue;
        public int DrawCycleDays => drawCycleDays;
        public int DrawDayOffset => drawDayOffset;
        public int GrandPrize => grandPrize;
        public int SecondPrize => secondPrize;
        public int ThirdPrize => thirdPrize;

        /// <summary>
        /// 根据匹配数量返回对应奖金
        /// </summary>
        public int GetPrize(int matchCount) => matchCount switch
        {
            4 => grandPrize,
            3 => secondPrize,
            2 => thirdPrize,
            _ => 0
        };

        /// <summary>
        /// 获取奖项名称
        /// </summary>
        public string GetPrizeName(int matchCount) => matchCount switch
        {
            4 => "Grand Prize",
            3 => "2nd Prize",
            2 => "3rd Prize",
            _ => ""
        };
    }
}
