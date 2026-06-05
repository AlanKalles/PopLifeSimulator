using System;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 维护真实店内顾客集合：进入内侧入口算进店，穿过出口到外侧算离店。
    /// </summary>
    public class CustomerPresenceService : MonoBehaviour
    {
        public static CustomerPresenceService Instance { get; private set; }

        public event Action<CustomerAgent, int> OnCustomerEnteredStore;
        public event Action<CustomerAgent, int> OnCustomerLeftStore;

        [Header("Debug")]
        [SerializeField] private int currentInsideCount;
        [SerializeField] private int enteredTodayCount;

        private readonly CustomerPresenceTracker tracker = new CustomerPresenceTracker();

        public int CurrentInsideCount => tracker.CurrentInsideCount;
        public int EnteredTodayCount => tracker.EnteredTodayCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            CustomerEventBus.OnCustomerEnteredStore += HandleCustomerEnteredStore;
            CustomerEventBus.OnCustomerLeftStore += HandleCustomerLeftStore;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen += ResetDaily;
                DayLoopManager.Instance.OnBuildPhaseStart += ResetDaily;
            }
        }

        private void OnDisable()
        {
            CustomerEventBus.OnCustomerEnteredStore -= HandleCustomerEnteredStore;
            CustomerEventBus.OnCustomerLeftStore -= HandleCustomerLeftStore;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnStoreOpen -= ResetDaily;
                DayLoopManager.Instance.OnBuildPhaseStart -= ResetDaily;
            }

            if (Instance == this) Instance = null;
        }

        private void HandleCustomerEnteredStore(CustomerAgent agent)
        {
            if (agent == null) return;
            if (!tracker.TryEnter(agent.customerID)) return;

            // Lifetime 入店计数 +1
            if (PopLife.ResourceManager.Instance != null)
                PopLife.ResourceManager.Instance.IncrementLifetimeCustomers();

            // 在 record 上置位 everEnteredStore（用于 New vs Returning 判定）
            // 注意：StatsDataManager 已在 OnSpawned 时 snapshot 了 preEntered，
            // 所以这里置位不影响当日判定，只影响之后入店时的"是否新客"
            if (CustomerRepository.Instance != null)
            {
                var record = CustomerRepository.Instance.Get(agent.customerID);
                if (record != null && !record.everEnteredStore)
                {
                    record.everEnteredStore = true;
                    CustomerRepository.Instance.SaveSingleRecord(record);
                }
            }

            SyncDebugFields();
            OnCustomerEnteredStore?.Invoke(agent, tracker.EnteredTodayCount);
        }

        private void HandleCustomerLeftStore(CustomerAgent agent)
        {
            if (agent == null) return;
            if (!tracker.TryLeave(agent.customerID)) return;

            SyncDebugFields();
            OnCustomerLeftStore?.Invoke(agent, tracker.CurrentInsideCount);
        }

        public void ResetDaily()
        {
            tracker.ResetDaily();
            SyncDebugFields();
        }

        private void SyncDebugFields()
        {
            currentInsideCount = tracker.CurrentInsideCount;
            enteredTodayCount = tracker.EnteredTodayCount;
        }
    }
}
