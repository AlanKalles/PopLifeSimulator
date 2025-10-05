using UnityEngine;

namespace PopLife
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("Panel References")]
        [SerializeField] private DailySettlementPanel dailySettlementPanel;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlement;
            }
        }

        private void OnDisable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlement;
            }
        }

        private void Start()
        {
            // 如果 DayLoopManager 在 UIManager 之前初始化，在 Start 中订阅
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlement;
            }
        }

        private void OnDailySettlement(DailySettlementData data)
        {
            if (dailySettlementPanel != null)
            {
                dailySettlementPanel.gameObject.SetActive(true);
                dailySettlementPanel.ShowSettlement(data);
            }
        }

        public void ShowMessage(string msg)
        {
            Debug.Log($"[UI] {msg}");
        }
    }
}
