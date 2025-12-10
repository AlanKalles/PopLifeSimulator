using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.Manager;

namespace PopLife.UI.Stats
{
    /// <summary>
    /// Stats 面板主控制器 - 管理滑动动画和页面切换
    /// </summary>
    public class StatsPanelController : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private GameObject rootObject; // 需要控制显隐的根对象
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Sub Panels")]
        [SerializeField] private GameObject economyPanel;
        [SerializeField] private GameObject productsPanel;
        [SerializeField] private GameObject currentCustomersPanel;

        [Header("Tab Buttons")]
        [SerializeField] private Button economyButton;
        [SerializeField] private Button productsButton;
        [SerializeField] private Button currentCustomersButton;

        [Header("Animation Settings")]
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float panelWidth = 400f; // 面板宽度

        private bool isVisible = false;
        private Coroutine slideCoroutine;

        // 当前激活的面板
        private enum PanelType { Economy, Products, CurrentCustomers }
        private PanelType currentPanel = PanelType.Economy;

        void Awake()
        {
            // 初始化位置（隐藏在左侧屏幕外）
            if (panelRoot != null)
            {
                panelRoot.anchoredPosition = new Vector2(-panelWidth, panelRoot.anchoredPosition.y);
            }

            // 初始状态
            canvasGroup.alpha = 1f;

            // 隐藏 root 对象（而不是脚本所在的 GameObject）
            if (rootObject != null)
            {
                rootObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            // 绑定按钮事件
            if (economyButton != null)
                economyButton.onClick.AddListener(() => SwitchPanel(PanelType.Economy));

            if (productsButton != null)
                productsButton.onClick.AddListener(() => SwitchPanel(PanelType.Products));

            if (currentCustomersButton != null)
                currentCustomersButton.onClick.AddListener(() => SwitchPanel(PanelType.CurrentCustomers));

            // 监听结算事件自动关闭
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlement;
            }
        }

        void OnDisable()
        {
            // 取消按钮事件
            if (economyButton != null)
                economyButton.onClick.RemoveAllListeners();

            if (productsButton != null)
                productsButton.onClick.RemoveAllListeners();

            if (currentCustomersButton != null)
                currentCustomersButton.onClick.RemoveAllListeners();

            // 取消结算事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlement;
            }
        }

        void Update()
        {
            // ESC 键关闭面板
            if (Input.GetKeyDown(KeyCode.Escape) && isVisible)
            {
                Hide();
            }
        }

        /// <summary>
        /// 切换面板显示/隐藏
        /// </summary>
        public void Toggle()
        {
            if (isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public void Show()
        {
            if (isVisible) return;

            // 激活 root 对象
            if (rootObject != null)
            {
                rootObject.SetActive(true);
            }

            isVisible = true;

            if (slideCoroutine != null)
                StopCoroutine(slideCoroutine);

            slideCoroutine = StartCoroutine(SlideIn());

            // 显示默认面板
            SwitchPanel(PanelType.Economy);
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            if (!isVisible) return;

            isVisible = false;

            if (slideCoroutine != null)
                StopCoroutine(slideCoroutine);

            slideCoroutine = StartCoroutine(SlideOut());
        }

        /// <summary>
        /// 滑入动画（从左往右）
        /// </summary>
        private IEnumerator SlideIn()
        {
            float elapsed = 0f;
            Vector2 startPos = new Vector2(-panelWidth, panelRoot.anchoredPosition.y);
            Vector2 endPos = new Vector2(0, panelRoot.anchoredPosition.y);

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDuration;
                // 使用 EaseOutCubic 缓动
                t = 1 - Mathf.Pow(1 - t, 3);
                panelRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }

            panelRoot.anchoredPosition = endPos;
        }

        /// <summary>
        /// 滑出动画（从右往左）
        /// </summary>
        private IEnumerator SlideOut()
        {
            float elapsed = 0f;
            Vector2 startPos = panelRoot.anchoredPosition;
            Vector2 endPos = new Vector2(-panelWidth, panelRoot.anchoredPosition.y);

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDuration;
                // 使用 EaseInCubic 缓动
                t = Mathf.Pow(t, 3);
                panelRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }

            panelRoot.anchoredPosition = endPos;

            // 隐藏 root 对象
            if (rootObject != null)
            {
                rootObject.SetActive(false);
            }
        }

        /// <summary>
        /// 切换子面板
        /// </summary>
        private void SwitchPanel(PanelType panelType)
        {
            // 播放切换音效（仅当切换到不同的面板时）
            if (currentPanel != panelType && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(AudioKeys.UI_STATS_SWITCH);
            }

            currentPanel = panelType;

            // 隐藏所有面板
            if (economyPanel != null) economyPanel.SetActive(false);
            if (productsPanel != null) productsPanel.SetActive(false);
            if (currentCustomersPanel != null) currentCustomersPanel.SetActive(false);

            // 显示目标面板并更新标题
            switch (panelType)
            {
                case PanelType.Economy:
                    if (economyPanel != null) economyPanel.SetActive(true);
                    UpdateButtonStates(economyButton, productsButton, currentCustomersButton);
                    UpdateTitle("Economy");
                    break;
                case PanelType.Products:
                    if (productsPanel != null) productsPanel.SetActive(true);
                    UpdateButtonStates(productsButton, economyButton, currentCustomersButton);
                    UpdateTitle("Products");
                    break;
                case PanelType.CurrentCustomers:
                    if (currentCustomersPanel != null) currentCustomersPanel.SetActive(true);
                    UpdateButtonStates(currentCustomersButton, economyButton, productsButton);
                    UpdateTitle("Current Customers");
                    break;
            }
        }

        /// <summary>
        /// 更新标题文本
        /// </summary>
        private void UpdateTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title;
            }
        }

        /// <summary>
        /// 更新按钮激活状态（通过 Interactable 控制，使用 Inspector 中设置的状态颜色）
        /// </summary>
        private void UpdateButtonStates(Button activeButton, Button inactiveButton1, Button inactiveButton2)
        {
            // 使用按钮的 interactable 属性来切换状态
            // 激活的按钮设为不可交互（显示 Disabled 颜色），其他设为可交互
            if (activeButton != null)
            {
                activeButton.interactable = false;
            }

            if (inactiveButton1 != null)
            {
                inactiveButton1.interactable = true;
            }

            if (inactiveButton2 != null)
            {
                inactiveButton2.interactable = true;
            }
        }

        /// <summary>
        /// 结算时自动关闭面板
        /// </summary>
        private void OnDailySettlement(DailySettlementData data)
        {
            if (isVisible)
            {
                Hide();
            }
        }
    }
}
