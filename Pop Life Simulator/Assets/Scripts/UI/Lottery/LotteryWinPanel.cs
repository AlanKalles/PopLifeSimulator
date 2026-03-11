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
        [SerializeField] private GameObject blockingPanel; // 半透明遮罩

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI matchInfoText;
        [SerializeField] private TextMeshProUGUI drawnNumberText;
        [SerializeField] private TextMeshProUGUI yourNumberText;
        [SerializeField] private TextMeshProUGUI prizeAmountText;
        [SerializeField] private TextMeshProUGUI prizeNameText;

        [Header("Buttons")]
        [SerializeField] private Button collectButton;

        // 回调
        private Action onCollected;

        private void Awake()
        {
            // 初始隐藏
            if (panelRoot != null) panelRoot.SetActive(false);
            if (blockingPanel != null) blockingPanel.SetActive(false);

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
        public void Show(int[] drawnNumber, int[] playerTicket,
            int matchCount, int prizeAmount, Action onCollectedCallback)
        {
            onCollected = onCollectedCallback;

            // 标题
            if (titleText != null)
                titleText.text = matchCount >= 4 ? "JACKPOT!" : "CONGRATULATIONS!";

            // 匹配信息
            if (matchInfoText != null)
                matchInfoText.text = $"You matched {matchCount} out of {drawnNumber.Length} numbers!";

            // 奖项名称
            if (prizeNameText != null && LotteryManager.Instance != null)
                prizeNameText.text = LotteryManager.Instance.GetConfig().GetPrizeName(matchCount);

            // 奖金金额
            if (prizeAmountText != null)
                prizeAmountText.text = $"${prizeAmount:N0}";

            // 构建带颜色标记的号码
            if (drawnNumberText != null)
                drawnNumberText.text = BuildColoredNumber(drawnNumber, playerTicket);
            if (yourNumberText != null)
                yourNumberText.text = BuildColoredNumber(playerTicket, drawnNumber);

            // 显示面板
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
        /// 构建带颜色的号码字符串
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
    }
}
