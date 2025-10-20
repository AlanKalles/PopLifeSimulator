using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using PopLife.Data;

namespace PopLife
{
    /// <summary>
    /// 警告类型枚举
    /// </summary>
    public enum AlertType
    {
        NotEnoughMoney,      // 资金不足
        NotEnoughFame,       // 声望不足
        BlueprintRequired,   // 需要蓝图
        InvalidPlacement,    // 无效放置
        General              // 通用警告
    }

    /// <summary>
    /// 模态警告弹窗组件
    /// 功能：
    /// - 显示不同类型的警告信息
    /// - 阻止其他UI交互（模态对话框）
    /// - 任意按键关闭
    /// - 关闭时触发回调（如取消建造）
    /// </summary>
    public class AlertPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;          // 面板根对象
        [SerializeField] private Image backgroundOverlay;       // 半透明遮罩背景
        [SerializeField] private TextMeshProUGUI messageText;   // 消息文本
        [SerializeField] private TextMeshProUGUI hintText;      // 提示文本（"Press any key to close"）

        [Header("Visual Settings")]
        [SerializeField] private Color overlayColor = new Color(0, 0, 0, 0.5f); // 遮罩颜色
        [SerializeField] private string closeHint = "Press any key to close...";

        [Header("Alert Messages")]
        [SerializeField] private string notEnoughMoneyMessage = "NOT ENOUGH MONEY";
        [SerializeField] private string notEnoughFameMessage = "NOT ENOUGH FAME";
        [SerializeField] private string blueprintRequiredMessage = "BLUEPRINT REQUIRED";
        [SerializeField] private string invalidPlacementMessage = "INVALID PLACEMENT";

        // 事件回调
        private Action onClose;

        private void Awake()
        {
            // 设置遮罩颜色
            if (backgroundOverlay != null)
            {
                backgroundOverlay.color = overlayColor;
            }

            // 设置提示文本
            if (hintText != null)
            {
                hintText.text = closeHint;
            }

            // 初始隐藏面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Update()
        {
            // 如果面板显示中，监听任意按键关闭
            if (panelRoot != null && panelRoot.activeSelf)
            {
                if (Input.anyKeyDown)
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// 显示警告弹窗（基于警告类型）
        /// </summary>
        public void Show(AlertType type, Action onCloseCallback = null)
        {
            string message = GetMessageByType(type);
            Show(message, onCloseCallback);
        }

        /// <summary>
        /// 显示警告弹窗（自定义消息）
        /// </summary>
        public void Show(string customMessage, Action onCloseCallback = null)
        {
            // 设置消息文本
            if (messageText != null)
            {
                messageText.text = customMessage;
            }

            // 保存回调
            onClose = onCloseCallback;

            // 显示面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            // 播放警告音效（可选）
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.UI_ERROR);
            }

            Debug.Log($"[AlertPanel] Showing: {customMessage}");
        }

        /// <summary>
        /// 关闭警告弹窗
        /// </summary>
        public void Close()
        {
            // 隐藏面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            // 触发关闭回调
            onClose?.Invoke();
            onClose = null;

            Debug.Log("[AlertPanel] Closed");
        }

        /// <summary>
        /// 根据警告类型获取消息
        /// </summary>
        private string GetMessageByType(AlertType type)
        {
            return type switch
            {
                AlertType.NotEnoughMoney => notEnoughMoneyMessage,
                AlertType.NotEnoughFame => notEnoughFameMessage,
                AlertType.BlueprintRequired => blueprintRequiredMessage,
                AlertType.InvalidPlacement => invalidPlacementMessage,
                AlertType.General => "WARNING",
                _ => "ALERT"
            };
        }

        /// <summary>
        /// 检查弹窗是否正在显示
        /// </summary>
        public bool IsShowing()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }
    }
}
