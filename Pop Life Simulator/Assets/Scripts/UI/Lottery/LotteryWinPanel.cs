using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

namespace PopLife.UI
{
    /// <summary>
    /// 彩票中奖贺喜弹窗
    /// 在关店后、结算面板前弹出，玩家点击 Collect 后关闭
    /// </summary>
    public class LotteryWinPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject blockingPanel;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI matchInfoText;
        [SerializeField] private TextMeshProUGUI drawnNumberText;
        [SerializeField] private TextMeshProUGUI yourNumberText;
        [SerializeField] private TextMeshProUGUI prizeAmountText;
        [SerializeField] private TextMeshProUGUI prizeNameText;

        [Header("Buttons")]
        [SerializeField] private Button collectButton;

        private Action onCollected;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (panelRoot != null) panelRoot.SetActive(false);
            if (blockingPanel != null) blockingPanel.SetActive(false);
            SetCanvasGroupVisible(false);

            if (collectButton != null)
                collectButton.onClick.AddListener(OnCollectClicked);
        }

        private void OnDestroy()
        {
            if (collectButton != null)
                collectButton.onClick.RemoveListener(OnCollectClicked);
        }

        /// <summary>
        /// 显示中奖贺喜面板
        /// </summary>
        public void Show(int[] drawnNumber, int[] playerTicket, int matchCount, int prizeAmount, Action onCollectedCallback)
        {
            onCollected = onCollectedCallback;

            if (titleText != null)
                titleText.text = matchCount >= 4 ? "JACKPOT!" : "CONGRATULATIONS!";

            if (matchInfoText != null)
                matchInfoText.text = $"You matched {matchCount} out of {drawnNumber.Length} numbers!";

            if (prizeNameText != null && LotteryManager.Instance != null)
                prizeNameText.text = LotteryManager.Instance.GetConfig().GetPrizeName(matchCount);

            if (prizeAmountText != null)
                prizeAmountText.text = $"${prizeAmount:N0}";

            if (drawnNumberText != null)
                drawnNumberText.text = BuildColoredNumber(drawnNumber, playerTicket);
            if (yourNumberText != null)
                yourNumberText.text = BuildColoredNumber(playerTicket, drawnNumber);

            SetCanvasGroupVisible(true);
            if (panelRoot != null) panelRoot.SetActive(true);
            if (blockingPanel != null) blockingPanel.SetActive(true);

            AudioManager.Instance?.PlaySound(AudioKeys.LOTTERY_WIN);
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (blockingPanel != null) blockingPanel.SetActive(false);
            SetCanvasGroupVisible(false);
        }

        private void OnCollectClicked()
        {
            LotteryManager.Instance?.CollectPrize();
            Hide();
            AudioManager.Instance?.PlaySound(AudioKeys.UI_CONFIRM);
            onCollected?.Invoke();
            onCollected = null;
        }

        /// <summary>
        /// 构建带颜色标记的号码字符串
        /// 匹配位显示绿色，不匹配位显示红色
        /// </summary>
        private string BuildColoredNumber(int[] number, int[] reference)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < number.Length; i++)
            {
                bool match = i < reference.Length && number[i] == reference[i];
                string color = match ? "#00FF00" : "#FF4444";
                if (i > 0) sb.Append("  ");
                sb.Append($"<color={color}>{number[i]}</color>");
            }
            return sb.ToString();
        }

        private void SetCanvasGroupVisible(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
