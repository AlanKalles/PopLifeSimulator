using UnityEngine;
using System;
using PopLife.Data;
using PopLife.UI;
using PopLife.UI.Guide;
using PopLife.UI.Quest;
using PopLife.UI.Stats;
using NewSettlementPanel = PopLife.UI.NewDailySettlementPanel;

namespace PopLife
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("Panel References")]
        [SerializeField] private NewSettlementPanel dailySettlementPanel;
        [SerializeField] private AlertPanel alertPanel;
        [SerializeField] private UI.ConfirmationPanel confirmationPanel;
        [SerializeField] private StatsPanelController statsPanel;

        [Header("Ticker Bars")]
        [SerializeField] private ScrollingMessageBar newsTicker;
        [SerializeField] private ScrollingMessageBar activityTicker;

        [Header("Quest UI")]
        [SerializeField] private QuestTrackerPanel questTrackerPanel;
        [SerializeField] private QuestLogPanel questLogPanel;

        [Header("Guide UI")]
        [SerializeField] private OperationGuidePanel operationGuidePanel;
        [SerializeField] private GuideCollectionPanel guideCollectionPanel;

        [Header("Lottery UI")]
        [SerializeField] private LotteryPanel lotteryPanel;
        [SerializeField] private LotteryWinPanel lotteryWinPanel;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            if (DayLoopManager.Instance != null)
            {
                // 先移除再添加，防止重复订阅
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlement;
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
        /// 添加消息到新闻播报条
        /// </summary>
        public void ShowNewsMessage(string message)
        {
            if (newsTicker != null)
                newsTicker.EnqueueMessage(message);
        }

        /// <summary>
        /// 添加消息到活动播报条
        /// </summary>
        public void ShowActivityMessage(string message)
        {
            if (activityTicker != null)
                activityTicker.EnqueueMessage(message);
        }

        /// <summary>
        /// 添加消息到播报条（兼容旧接口，发送到新闻条）
        /// </summary>
        public void ShowScrollingMessage(string message)
        {
            ShowNewsMessage(message);
        }

        /// <summary>
        /// 清空所有播报条的消息队列
        /// </summary>
        public void ClearScrollingMessages()
        {
            if (newsTicker != null) newsTicker.ClearQueue();
            if (activityTicker != null) activityTicker.ClearQueue();
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

        /// <summary>
        /// 切换 Stats 面板显示/隐藏
        /// </summary>
        public void ToggleStatsPanel()
        {
            if (statsPanel != null)
            {
                // 播放打开Stats面板音效
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySound(AudioKeys.UI_STATS_OPEN);
                }

                statsPanel.Toggle();
            }
            else
            {
                Debug.LogWarning("[UIManager] StatsPanel is not assigned!");
            }
        }

        /// <summary>
        /// 显示 Stats 面板
        /// </summary>
        public void ShowStatsPanel()
        {
            if (statsPanel != null)
            {
                statsPanel.Show();
            }
        }

        /// <summary>
        /// 隐藏 Stats 面板
        /// </summary>
        public void HideStatsPanel()
        {
            if (statsPanel != null)
            {
                statsPanel.Hide();
            }
        }

        /// <summary>
        /// 显示任务详情（兼容旧接口，内部调用 ShowQuestLog）
        /// </summary>
        public void ShowQuestDetail(string questName)
        {
            ShowQuestLog(questName);
        }

        /// <summary>
        /// 打开任务日志面板，并聚焦到指定任务
        /// </summary>
        public void ShowQuestLog(string questName = null)
        {
            if (questLogPanel != null)
            {
                questLogPanel.Show(questName);
            }
        }

        /// <summary>
        /// 关闭任务日志面板
        /// </summary>
        public void HideQuestLog()
        {
            if (questLogPanel != null)
            {
                questLogPanel.Hide();
            }
        }

        /// <summary>
        /// 刷新任务追踪面板
        /// </summary>
        public void RefreshQuestTracker()
        {
            if (questTrackerPanel != null)
            {
                questTrackerPanel.RefreshQuests();
            }
        }

        #region Lottery UI

        /// <summary>
        /// 添加优先级消息到新闻播报条（插入到队首）
        /// </summary>
        public void ShowPriorityNewsMessage(string message)
        {
            if (newsTicker != null)
                newsTicker.EnqueuePriorityMessage(message);
        }

        /// <summary>
        /// 显示彩票中奖贺喜面板
        /// </summary>
        public void ShowLotteryWinPanel(int[] drawnNumber, int[] playerTicket,
            int matchCount, int prizeAmount, Action onCollected)
        {
            if (lotteryWinPanel != null)
            {
                lotteryWinPanel.Show(drawnNumber, playerTicket, matchCount, prizeAmount, onCollected);
            }
            else
            {
                Debug.LogWarning("[UIManager] LotteryWinPanel is not assigned!");
                onCollected?.Invoke();
            }
        }

        #endregion

        #region Guide UI

        /// <summary>
        /// 显示操作指南面板
        /// </summary>
        public void ShowOperationGuide(OperationGuideData data, Action onClose = null)
        {
            if (operationGuidePanel != null)
            {
                operationGuidePanel.Show(data, onClose);
            }
            else
            {
                Debug.LogWarning("[UIManager] OperationGuidePanel is not assigned!");
            }
        }

        /// <summary>
        /// 显示指南集合面板
        /// </summary>
        public void ShowGuideCollection()
        {
            if (guideCollectionPanel != null)
            {
                guideCollectionPanel.Show();
            }
        }

        /// <summary>
        /// 隐藏指南集合面板
        /// </summary>
        public void HideGuideCollection()
        {
            if (guideCollectionPanel != null)
            {
                guideCollectionPanel.Hide();
            }
        }

        /// <summary>
        /// 切换指南集合面板显示/隐藏
        /// </summary>
        public void ToggleGuideCollection()
        {
            if (guideCollectionPanel != null)
            {
                guideCollectionPanel.Toggle();
            }
        }

        #endregion
    }
}
