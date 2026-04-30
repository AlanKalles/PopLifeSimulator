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

        [Header("Content (Common - 两态共用)")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI matchInfoText;
        [SerializeField] private TextMeshProUGUI drawnNumberText;
        [SerializeField] private TextMeshProUGUI yourNumberText;

        [Header("Win State (中奖独有)")]
        [SerializeField] private GameObject winStateRoot;
        [SerializeField] private TextMeshProUGUI prizeAmountText;

        [Header("Lose State (未中奖独有)")]
        [SerializeField] private GameObject loseStateRoot;

        [Header("Buttons")]
        [SerializeField] private Button collectButton;
        [SerializeField] private TextMeshProUGUI collectButtonLabel;

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
        /// 显示开奖结果面板（中奖/未中奖均会调用）
        /// prizeAmount > 0 表示中奖，激活 winStateRoot；否则激活 loseStateRoot
        /// </summary>
        public void Show(int[] drawnNumber, int[] playerTicket, int matchCount, int prizeAmount, Action onCollectedCallback)
        {
            onCollected = onCollectedCallback;
            bool isWin = prizeAmount > 0;

            // 通用区块（两态共用）：中奖用感叹号，未中奖用句号
            if (matchInfoText != null)
            {
                string punctuation = isWin ? "!" : ".";
                matchInfoText.text = $"You matched {matchCount} out of {drawnNumber.Length} numbers{punctuation}";
            }
            if (drawnNumberText != null)
                drawnNumberText.text = BuildColoredNumber(drawnNumber, playerTicket);
            if (yourNumberText != null)
                yourNumberText.text = BuildColoredNumber(playerTicket, drawnNumber);

            // 标题（共用，根据匹配数差异化文案）
            if (titleText != null)
            {
                titleText.text = matchCount switch
                {
                    4 => "Jackpot!!!",
                    3 => "2nd Prize",
                    2 => "3rd Prize",
                    _ => "BETTER LUCK NEXT TIME"
                };
            }

            // 状态分支：切换中奖/未中奖独有视觉
            if (winStateRoot != null) winStateRoot.SetActive(isWin);
            if (loseStateRoot != null) loseStateRoot.SetActive(!isWin);

            // 中奖独有内容：奖金额度
            if (isWin && prizeAmountText != null)
                prizeAmountText.text = $"${prizeAmount:N0}";

            // 按钮文案：中奖 "Collect" / 未中奖 "Continue"
            if (collectButtonLabel != null)
                collectButtonLabel.text = isWin ? "Collect" : "Continue";

            // 显示
            SetCanvasGroupVisible(true);
            if (panelRoot != null) panelRoot.SetActive(true);
            if (blockingPanel != null) blockingPanel.SetActive(true);

            // 音效：仅中奖时播放
            if (isWin)
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
