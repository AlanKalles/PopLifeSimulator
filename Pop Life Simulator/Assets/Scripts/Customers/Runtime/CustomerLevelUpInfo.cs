using System;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 顾客升级信息记录，用于每日结算展示
    /// </summary>
    [Serializable]
    public class CustomerLevelUpInfo
    {
        public string customerId;
        public string customerName;
        public int oldLevel;         // 升级前等级
        public int newLevel;         // 升级后等级
        public int totalXp;          // 当前总经验
        public int xpGained;         // 本次获得的经验
        public string appearanceId;  // 外貌ID（用于UI显示头像）
    }
}
