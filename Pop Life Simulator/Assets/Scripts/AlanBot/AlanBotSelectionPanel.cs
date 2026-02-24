using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using PopLife.AlanBot.UI;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot选择面板UI（Screen Space Canvas）
    /// 3个按钮：Item Codex / Customer Codex / Calendar
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
        [SerializeField] private Button closeButton;

        [Header("子面板引用")]
        [SerializeField] private ItemCodexPanel itemCodexPanel;
        [SerializeField] private CustomerCodexPanel customerCodexPanel;
        [SerializeField] private CalendarPanel calendarPanel;

        [Header("动画")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        private Action onHideCallback;
        private Coroutine fadeCoroutine;

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
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);
        }

        /// <summary>
        /// 显示面板，提供关闭时的回调
        /// </summary>
        public void Show(Action hideCallback = null)
        {
            onHideCallback = hideCallback;

            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeCanvasGroup(0f, 1f, fadeInDuration, true, true));
        }

        /// <summary>
        /// 隐藏面板并触发回调
        /// </summary>
        public void Hide()
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeAndHide());
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
                elapsed += Time.deltaTime;
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
            Hide();
            if (itemCodexPanel != null)
                itemCodexPanel.Show();
        }

        private void OnCustomerCodexClicked()
        {
            Hide();
            if (customerCodexPanel != null)
                customerCodexPanel.Show();
        }

        private void OnCalendarClicked()
        {
            Hide();
            if (calendarPanel != null)
                calendarPanel.Show();
        }

        private void OnCloseClicked()
        {
            Hide();
        }
    }
}
