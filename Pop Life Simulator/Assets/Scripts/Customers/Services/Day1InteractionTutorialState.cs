namespace PopLife.Customers.Services
{
    /// <summary>
    /// Day 1 专用 interaction 的纯状态：只锁定当天第三个进店顾客。
    /// </summary>
    public class Day1InteractionTutorialState
    {
        public const int RequiredEnteredCustomerIndex = 3;

        private int enteredCount;
        private string forcedCustomerId;

        public bool IsCompleted { get; private set; }
        public bool HasForcedCustomer => !string.IsNullOrEmpty(forcedCustomerId);
        public string ForcedCustomerId => forcedCustomerId;

        public bool RecordCustomerEntered(string customerId, int day, bool alreadyCompleted)
        {
            if (day != 1 || alreadyCompleted || IsCompleted || string.IsNullOrEmpty(customerId))
                return false;

            enteredCount++;
            if (enteredCount != RequiredEnteredCustomerIndex || HasForcedCustomer)
                return false;

            forcedCustomerId = customerId;
            return true;
        }

        public bool IsForcedCustomer(string customerId)
        {
            return !IsCompleted
                && !string.IsNullOrEmpty(forcedCustomerId)
                && forcedCustomerId == customerId;
        }

        public void MarkCompleted()
        {
            IsCompleted = true;
        }

        public void ResetForNewDay()
        {
            enteredCount = 0;
            forcedCustomerId = null;
            IsCompleted = false;
        }
    }
}
