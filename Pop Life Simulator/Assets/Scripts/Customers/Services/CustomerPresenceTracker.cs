using System.Collections.Generic;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 纯逻辑店内人数计数器。MonoBehaviour 包装见 CustomerPresenceService。
    /// </summary>
    public class CustomerPresenceTracker
    {
        private readonly HashSet<string> insideCustomerIds = new HashSet<string>();

        public int CurrentInsideCount => insideCustomerIds.Count;
        public int EnteredTodayCount { get; private set; }

        public bool TryEnter(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return false;
            if (!insideCustomerIds.Add(customerId)) return false;

            EnteredTodayCount++;
            return true;
        }

        public bool TryLeave(string customerId)
        {
            if (string.IsNullOrEmpty(customerId)) return false;
            return insideCustomerIds.Remove(customerId);
        }

        public bool Contains(string customerId)
        {
            return !string.IsNullOrEmpty(customerId) && insideCustomerIds.Contains(customerId);
        }

        public void ResetDaily()
        {
            insideCustomerIds.Clear();
            EnteredTodayCount = 0;
        }
    }
}
