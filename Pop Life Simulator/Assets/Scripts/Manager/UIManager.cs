using UnityEngine;
using System;
using PopLife.UI;

namespace PopLife
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("Panel References")]
        [SerializeField] private DailySettlementPanel dailySettlementPanel;
        [SerializeField] private AlertPanel alertPanel;
        [SerializeField] private ScrollingMessageBar scrollingMessageBar;
        [SerializeField] private UI.ConfirmationPanel confirmationPanel;

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

        /// <summary>
        /// 显示消息（旧接口，仅打印日志）
        /// </summary>
        public void ShowMessage(string msg)
        {
            Debug.Log($"[UI] {msg}");
        }

        /// <summary>
        /// 显示警告弹窗（基于警告类型）
        /// </summary>
        /// <param name="type">警告类型</param>
        /// <param name="onClose">关闭时的回调（可选）</param>
        public void ShowAlert(AlertType type, Action onClose = null)
        {
            if (alertPanel != null)
            {
                alertPanel.Show(type, onClose);
            }
            else
            {
                Debug.LogWarning("[UIManager] AlertPanel is not assigned!");
                ShowMessage(type.ToString());
            }
        }

        /// <summary>
        /// 显示警告弹窗（自定义消息）
        /// </summary>
        /// <param name="customMessage">自定义消息文本</param>
        /// <param name="onClose">关闭时的回调（可选）</param>
        public void ShowAlert(string customMessage, Action onClose = null)
        {
            if (alertPanel != null)
            {
                alertPanel.Show(customMessage, onClose);
            }
            else
            {
                Debug.LogWarning("[UIManager] AlertPanel is not assigned!");
                ShowMessage(customMessage);
            }
        }

        /// <summary>
        /// 检查是否有警告弹窗正在显示
        /// </summary>
        public bool IsAlertShowing()
        {
            return alertPanel != null && alertPanel.IsShowing();
        }

        /// <summary>
        /// 添加消息到滚动播报条
        /// </summary>
        /// <param name="message">消息内容</param>
        public void ShowScrollingMessage(string message)
        {
            if (scrollingMessageBar != null)
            {
                scrollingMessageBar.EnqueueMessage(message);
            }
            else
            {
                Debug.LogWarning("[UIManager] ScrollingMessageBar is not assigned!");
            }
        }

        /// <summary>
        /// 清空滚动播报条的消息队列
        /// </summary>
        public void ClearScrollingMessages()
        {
            if (scrollingMessageBar != null)
            {
                scrollingMessageBar.ClearQueue();
            }
        }

        /// <summary>
        /// 显示确认对话框（用于建筑销毁等操作）
        /// </summary>
        /// <param name="buildingName">建筑名称</param>
        /// <param name="refundAmount">退款金额</param>
        /// <param name="onConfirm">确认回调</param>
        /// <param name="onCancel">取消回调（可选）</param>
        public void ShowConfirmation(string buildingName, int refundAmount, Action onConfirm, Action onCancel = null)
        {
            if (confirmationPanel != null)
            {
                confirmationPanel.Show(buildingName, refundAmount, onConfirm, onCancel);
            }
            else
            {
                Debug.LogWarning("[UIManager] ConfirmationPanel is not assigned!");
                // Fallback: 直接执行确认操作
                onConfirm?.Invoke();
            }
        }

        /// <summary>
        /// 检查是否有确认对话框正在显示
        /// </summary>
        public bool IsConfirmationShowing()
        {
            return confirmationPanel != null && confirmationPanel.IsShowing();
        }

        /// <summary>
        /// 隐藏确认对话框（强制关闭）
        /// </summary>
        public void HideConfirmation()
        {
            if (confirmationPanel != null)
            {
                confirmationPanel.Hide();
            }
        }
    }
}
