using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Text;
using TMPro;
using PopLife.AlanBot.UI;
using PopLife.Manager;
using PopLife.UI.Guide;
using PopLife.UI.Quest;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot选择面板UI（Screen Space Canvas）
    /// 4个按钮：Item Codex / Customer Codex / Calendar / Guide
    /// CanvasGroup淡入淡出
    /// </summary>
    public class AlanBotSelectionPanel : MonoBehaviour
    {
        [Header("面板根")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("按钮")]
        [SerializeField] private Button itemCodexButton;
        [SerializeField] private Button customerCodexButton;
        [SerializeField] private Button calendarButton;
        [SerializeField] private Button questLogButton;
        [SerializeField] private Button guideButton;
        [SerializeField] private Button closeButton;

        [Header("子面板引用")]
        [SerializeField] private ItemCodexPanel itemCodexPanel;
        [SerializeField] private CustomerCodexPanel customerCodexPanel;
        [SerializeField] private CalendarPanel calendarPanel;
        [SerializeField] private QuestLogPanel questLogPanel;
        [SerializeField] private GuideCollectionPanel guideCollectionPanel;

        [Header("动画")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        [Header("台词显示")]
        [SerializeField] private TMP_Text quoteText;
        [SerializeField] private float typewriterSpeed = 30f; // 字符/秒

        private Action onHideCallback;
        private Coroutine fadeCoroutine;
        private Coroutine typewriterCoroutine;

        private void Awake()
        {
            // 初始隐藏
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            // 绑定按钮
            if (itemCodexButton != null)
                itemCodexButton.onClick.AddListener(OnItemCodexClicked);
            if (customerCodexButton != null)
                customerCodexButton.onClick.AddListener(OnCustomerCodexClicked);
            if (calendarButton != null)
                calendarButton.onClick.AddListener(OnCalendarClicked);
            if (questLogButton != null)
                questLogButton.onClick.AddListener(OnQuestLogClicked);
            if (guideButton != null)
                guideButton.onClick.AddListener(OnGuideClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);
        }

        /// <summary>
        /// 显示面板，提供关闭时的回调
        /// </summary>
        public void Show(Action hideCallback = null)
        {
            CloseShelfListPanelIfOpen();

            onHideCallback = hideCallback;

            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration, true, true));

            // 抽取随机台词并启动打字机（每次 Show 都换新台词）
            StopTypewriter();
            string quote = AlanBotQuoteLibrary.GetRandomQuote();
            if (quoteText != null)
            {
                quoteText.text = string.Empty;
                if (!string.IsNullOrEmpty(quote))
                    typewriterCoroutine = StartCoroutine(TypewriterRoutine(quote));
            }
        }

        private static void CloseShelfListPanelIfOpen()
        {
            var shelfListPanel = FindFirstObjectByType<PopLife.UI.ShelfListPanel>();
            if (shelfListPanel != null && shelfListPanel.IsOpen())
            {
                shelfListPanel.ClosePanel();
            }
        }

        /// <summary>
        /// 隐藏面板并触发回调
        /// </summary>
        public void Hide()
        {
            // 停止打字机：保持 quoteText 当前内容，让面板淡出动画自然带走半截字
            StopTypewriter();

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeAndHide());
        }

        private void OnEnable()
        {
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnDailySettlement += OnDailySettlement;
        }

        private void OnDisable()
        {
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnDailySettlement -= OnDailySettlement;

            // 兜底：场景切换/对象销毁时停止打字机
            StopTypewriter();
        }

        // 结算时自动关闭面板（包括正在显示的子面板）
        private void OnDailySettlement(DailySettlementData data)
        {
            // 子面板独立显示时 panelRoot 已被关闭，但 Calendar/ItemCodex 等可能仍在显示，
            // 强制关闭所有从 AlanBot 入口打开的面板，保证结算面板独占。
            if (panelRoot != null && panelRoot.activeSelf)
                Hide();

            if (itemCodexPanel != null) itemCodexPanel.Hide();
            if (customerCodexPanel != null) customerCodexPanel.Hide();
            if (calendarPanel != null) calendarPanel.Hide();
            if (questLogPanel != null) questLogPanel.Hide();
            if (guideCollectionPanel != null) guideCollectionPanel.Hide();

            // 清理回调链，避免子面板关闭后回弹到 AlanBot 选择面板
            onHideCallback = null;
        }

        private void StopTypewriter()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            // 不修改 quoteText.text —— 保持当前进度
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            if (quoteText == null) yield break;

            float charDuration = 1f / Mathf.Max(typewriterSpeed, 1f);
            var sb = new StringBuilder(fullText.Length);
            foreach (char c in fullText)
            {
                sb.Append(c);
                quoteText.text = sb.ToString();
                // 使用 unscaled 时间，与 FadeCanvasGroup 一致，不受 Time.timeScale 影响
                yield return new WaitForSecondsRealtime(charDuration);
            }
            typewriterCoroutine = null;
        }

        private IEnumerator FadeAndHide()
        {
            yield return FadeCanvasGroup(1f, 0f, fadeOutDuration, false, false);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            // 触发回调：恢复AlanBot状态
            onHideCallback?.Invoke();
            onHideCallback = null;
        }

        private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration,
            bool interactable, bool blocksRaycasts)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        // ─── 按钮回调 ───

        private void OnItemCodexClicked()
        {
            GameStateManager.Instance?.NotifyItemCodexOpened();

            // 保存当前回调，子面板关闭时恢复AlanBot状态
            var savedCallback = onHideCallback;
            onHideCallback = null; // 防止Hide()触发回调

            Hide();
            if (itemCodexPanel != null)
                itemCodexPanel.Show(closeCallback: () =>
                {
                    // 关闭Codex后返回选择面板
                    Show(savedCallback);
                });
        }

        private void OnCustomerCodexClicked()
        {
            GameStateManager.Instance?.NotifyCustomerCodexOpened();

            var savedCallback = onHideCallback;
            onHideCallback = null;

            Hide();
            if (customerCodexPanel != null)
                customerCodexPanel.Show(closeCallback: () =>
                {
                    Show(savedCallback);
                });
        }

        private void OnCalendarClicked()
        {
            GameStateManager.Instance?.NotifyCalendarOpened();

            // 保存回调链（与 ItemCodex/CustomerCodex 一致的模式）
            var savedCallback = onHideCallback;
            onHideCallback = null;

            Hide();
            if (calendarPanel != null)
                calendarPanel.Show(closeCallback: () =>
                {
                    Show(savedCallback);
                });
        }

        private void OnQuestLogClicked()
        {
            var savedCallback = onHideCallback;
            onHideCallback = null;

            Hide();
            if (questLogPanel != null)
                questLogPanel.Show(closeCallback: () =>
                {
                    Show(savedCallback);
                });
        }

        private void OnGuideClicked()
        {
            var savedCallback = onHideCallback;
            onHideCallback = null;

            Hide();
            if (guideCollectionPanel != null)
                guideCollectionPanel.Show(() =>
                {
                    Show(savedCallback);
                });
        }

        private void OnCloseClicked()
        {
            Hide();
        }
    }
}
