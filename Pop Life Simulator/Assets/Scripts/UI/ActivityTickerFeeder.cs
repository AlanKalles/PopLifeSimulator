using UnityEngine;
using PopLife.Runtime;
using PopLife.Customers.Services;
using PopLife.Customers.Runtime;

namespace PopLife.UI
{
    /// <summary>
    /// 顾客活动播报数据源 - 监听顾客事件，生成活动消息喂入 ScrollingMessageBar
    /// 播报事件：进店、拿货、结账
    /// </summary>
    public class ActivityTickerFeeder : MonoBehaviour
    {
        [SerializeField] private ScrollingMessageBar ticker;

        void Start()
        {
            if (ticker == null)
                ticker = GetComponent<ScrollingMessageBar>();
        }

        void OnEnable()
        {
            CustomerEventBus.OnSpawned += HandleSpawned;
            CustomerEventBus.OnPurchased += HandlePurchased;
            CustomerEventBus.OnCheckedOut += HandleCheckedOut;
        }

        void OnDisable()
        {
            CustomerEventBus.OnSpawned -= HandleSpawned;
            CustomerEventBus.OnPurchased -= HandlePurchased;
            CustomerEventBus.OnCheckedOut -= HandleCheckedOut;
        }

        /// <summary>
        /// 顾客进店
        /// </summary>
        private void HandleSpawned(CustomerAgent agent)
        {
            if (agent == null || ticker == null) return;

            string name = GetCustomerName(agent);
            ticker.EnqueueMessage($"{name} entered the store");
        }

        /// <summary>
        /// 顾客拿货
        /// </summary>
        private void HandlePurchased(CustomerAgent agent, ShelfInstance shelf, int qty, int price)
        {
            if (agent == null || ticker == null) return;

            string name = GetCustomerName(agent);
            string itemName = shelf != null && shelf.archetype != null ? shelf.archetype.name : "item";
            ticker.EnqueueMessage($"{name} picked {itemName}");
        }

        /// <summary>
        /// 顾客结账
        /// </summary>
        private void HandleCheckedOut(CustomerAgent agent)
        {
            if (agent == null || ticker == null) return;

            string name = GetCustomerName(agent);
            int spent = agent.currentSession?.moneySpent ?? 0;
            ticker.EnqueueMessage($"{name} spent ${spent}");
        }

        /// <summary>
        /// 从 CustomerRecord 获取顾客名字（修复旧版 TMP 读取导致 Unknown 的问题）
        /// </summary>
        private string GetCustomerName(CustomerAgent agent)
        {
            var record = agent.GetCustomerRecord();
            if (record != null && !string.IsNullOrEmpty(record.name))
                return record.name;
            return "Unknown";
        }
    }
}
