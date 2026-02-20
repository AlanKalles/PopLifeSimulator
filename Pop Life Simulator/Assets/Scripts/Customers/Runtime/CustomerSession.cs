using System;
using System.Collections.Generic;
using PopLife.Customers.Data;


namespace PopLife.Customers.Runtime
{
    [Serializable]
    public class ShelfVisit
    {
        public string shelfId;
        public int categoryIndex;
        public float staySeconds;
        public int reservedQty;
        public int boughtQty;
        public float waitSeconds;
    }

    [Serializable]
    public class CustomerSession
    {
        public string customerId;
        public string dayId;
        public string sessionId;
        public int moneyBagStart;
        public int moneySpent;
        public int trustDelta;
        public int embarrassmentPeak;
        public string leaveReason;
        public float timeInStore;
        public float pathLength;
        public float cashierQueueTime;
        public List<ShelfVisit> visitedShelves = new();
        // 每次购买时记录命中的漏斗层级（用于 XP 计算）
        public List<FunnelPhase> purchaseFunnelTiers = new();
    }
}