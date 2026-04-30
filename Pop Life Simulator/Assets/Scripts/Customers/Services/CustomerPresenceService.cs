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
