using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PrimeTween;
using PopLife.Data;
using PopLife.Manager;

namespace PopLife.UI
{
    /// <summary>
    /// Lottery number selection and ticket purchase panel.
    /// </summary>
    public class LotteryPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Digit Selectors")]
        [Tooltip("4 digit text displays")]
        [SerializeField] private TextMeshProUGUI[] digitTexts;
        [Tooltip("4 increment buttons")]
        [SerializeField] private Button[] digitUpButtons;
        [Tooltip("4 decrement buttons")]
        [SerializeField] private Button[] digitDownButtons;

        [Header("Info Display")]
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI nextDrawText;
        [SerializeField] private TextMeshProUGUI currentTicketText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Prize Table")]
        [SerializeField] private TextMeshProUGUI grandPrizeText;
        [SerializeField] private TextMeshProUGUI secondPrizeText;
        [SerializeField] private TextMeshProUGUI thirdPrizeText;

        [Header("Buttons")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private Button closeButton;

        [Header("Entry Button")]
        [Tooltip("Lottery entry button on screen")]
        [SerializeField] private Button entryButton;
        [SerializeField] private CanvasGroup entryButtonCanvasGroup;

        [Header("Unlock Animation")]
        [SerializeField] private float fadeInDuration = 0.5f;

        [Header("Animation")]
        [SerializeField] private LotteryPanelAnimator animator;

        private int[] selectedDigits;
        private bool isUnlocked;
        private LotteryDigitReel[] digitReels;

        private UnityEngine.Events.UnityAction[] incrementActions;
        private UnityEngine.Events.UnityAction[] decrementActions;

        private void Awake()
        {
            int digitCount = LotteryManager.Instance != null
                ? LotteryManager.Instance.GetDigitCount()
                : 4;
            selectedDigits = new int[digitCount];

            if (TutorialEventBus.IsMarkerTriggered(TutorialMarker.LotteryUnlocked))
            {
                isUnlocked = true;
                SetEntryButtonVisible(true);
            }
            else
            {
                isUnlocked = false;
                SetEntryButtonVisible(false);
            }

            if (panelRoot != null)
                panelRoot.SetActive(false);

            // 初始化滚轴
            int maxVal = LotteryManager.Instance?.GetMaxDigitValue() ?? 9;
            digitReels = new LotteryDigitReel[digitCount];
            for (int i = 0; i < digitCount && i < digitTexts.Length; i++)
            {
                if (digitTexts[i] == null) continue;
                var reel = digitTexts[i].gameObject.AddComponent<LotteryDigitReel>();
                reel.Initialize(digitTexts[i], maxVal);
                digitReels[i] = reel;
            }
        }

        private void OnEnable()
        {
            if (!isUnlocked)
                TutorialEventBus.OnMarkerTriggered += OnMarkerTriggered;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpen;
            }

            if (entryButton != null)
                entryButton.onClick.AddListener(Show);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(OnPurchaseClicked);

            int count = Mathf.Min(digitUpButtons.Length, selectedDigits.Length);
            incrementActions = new UnityEngine.Events.UnityAction[count];
            decrementActions = new UnityEngine.Events.UnityAction[count];

            for (int i = 0; i < count; i++)
            {
                int index = i;
                incrementActions[i] = () => IncrementDigit(index);
                decrementActions[i] = () => DecrementDigit(index);

                if (digitUpButtons[i] != null)
                    digitUpButtons[i].onClick.AddListener(incrementActions[i]);
                if (i < digitDownButtons.Length && digitDownButtons[i] != null)
                    digitDownButtons[i].onClick.AddListener(decrementActions[i]);
            }
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;

            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpen;
            }

            if (entryButton != null) entryButton.onClick.RemoveListener(Show);
            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
            if (purchaseButton != null) purchaseButton.onClick.RemoveListener(OnPurchaseClicked);

            if (incrementActions != null)
            {
                for (int i = 0; i < incrementActions.Length; i++)
                {
                    if (i < digitUpButtons.Length && digitUpButtons[i] != null)
                        digitUpButtons[i].onClick.RemoveListener(incrementActions[i]);
                    if (i < digitDownButtons.Length && digitDownButtons[i] != null)
                        digitDownButtons[i].onClick.RemoveListener(decrementActions[i]);
                }
            }
        }

        private void OnMarkerTriggered(TutorialMarker marker)
        {
            if (marker != TutorialMarker.LotteryUnlocked) return;

            isUnlocked = true;
            TutorialEventBus.OnMarkerTriggered -= OnMarkerTriggered;

            if (entryButtonCanvasGroup != null)
            {
                entryButtonCanvasGroup.alpha = 0f;
                entryButtonCanvasGroup.interactable = true;
                entryButtonCanvasGroup.blocksRaycasts = true;
                Tween.Alpha(entryButtonCanvasGroup, 1f, fadeInDuration, useUnscaledTime: true);
            }
        }

        private void SetEntryButtonVisible(bool visible)
        {
            if (entryButtonCanvasGroup != null)
            {
                entryButtonCanvasGroup.alpha = visible ? 1f : 0f;
                entryButtonCanvasGroup.interactable = visible;
                entryButtonCanvasGroup.blocksRaycasts = visible;
            }
        }

        private void OnBuildPhaseStart()
        {
            if (!isUnlocked) return;
            SetEntryButtonVisible(true);
        }

        private void OnStoreOpen()
        {
            if (!isUnlocked) return;
            SetEntryButtonVisible(false);
            Hide();
        }

        public void Show()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            RefreshDisplay();
            if (animator != null)
                animator.PlayOpenAnimation();
            AudioManager.Instance?.PlaySound(AudioKeys.UI_CLICK);
        }

        public void Hide()
        {
            if (panelRoot == null) return;
            AudioManager.Instance?.PlaySound(AudioKeys.UI_CLICK);
            if (animator != null)
            {
                animator.PlayCloseAnimation(() =>
                {
                    if (panelRoot != null)
                        panelRoot.SetActive(false);
                });
            }
            else
            {
                panelRoot.SetActive(false);
            }
        }

        public bool IsShowing()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        private void IncrementDigit(int index)
        {
            if (LotteryManager.Instance != null && LotteryManager.Instance.HasTicketForCurrentCycle())
                return;

            int max = LotteryManager.Instance != null
                ? LotteryManager.Instance.GetMaxDigitValue()
                : 9;
            selectedDigits[index] = (selectedDigits[index] + 1) % (max + 1);
            UpdateDigitDisplay(index);
            AudioManager.Instance?.PlaySound(AudioKeys.UI_CLICK);
        }

        private void DecrementDigit(int index)
        {
            if (LotteryManager.Instance != null && LotteryManager.Instance.HasTicketForCurrentCycle())
                return;

            int max = LotteryManager.Instance != null
                ? LotteryManager.Instance.GetMaxDigitValue()
                : 9;
            selectedDigits[index] = (selectedDigits[index] - 1 + max + 1) % (max + 1);
            UpdateDigitDisplay(index);
            AudioManager.Instance?.PlaySound(AudioKeys.UI_CLICK);
        }

        private void UpdateDigitDisplay(int index)
        {
            if (digitReels != null && index < digitReels.Length && digitReels[index] != null)
                digitReels[index].ScrollToDigit(selectedDigits[index]);
            else if (index < digitTexts.Length && digitTexts[index] != null)
                digitTexts[index].text = selectedDigits[index].ToString();
        }

        private void OnPurchaseClicked()
        {
            if (LotteryManager.Instance == null) return;

            if (LotteryManager.Instance.TryPurchaseTicket(selectedDigits))
            {
                AudioManager.Instance?.PlaySound(AudioKeys.LOTTERY_PURCHASE);
                if (animator != null) animator.PlayPurchaseSuccess();
                RefreshDisplay();
            }
            else
            {
                AudioManager.Instance?.PlaySound(AudioKeys.UI_ERROR);
                if (animator != null) animator.PlayPurchaseFailure();
            }
        }

        private void RefreshDisplay()
        {
            var lm = LotteryManager.Instance;
            if (lm == null) return;

            var config = lm.GetConfig();

            if (costText != null)
                costText.text = $"Cost: ${config.TicketCost}";

            if (nextDrawText != null)
            {
                int days = lm.GetDaysUntilDraw();
                if (days == 0)
                    nextDrawText.text = "Draw day today!";
                else
                    nextDrawText.text = $"Next draw in: {days} day{(days > 1 ? "s" : string.Empty)}";
            }

            if (grandPrizeText != null) grandPrizeText.text = $"${config.GrandPrize:N0}";
            if (secondPrizeText != null) secondPrizeText.text = $"${config.SecondPrize:N0}";
            if (thirdPrizeText != null) thirdPrizeText.text = $"${config.ThirdPrize:N0}";

            bool hasTicket = lm.HasTicketForCurrentCycle();
            int[] currentTicket = lm.GetCurrentTicket();

            if (currentTicketText != null)
            {
                bool showTicketLabel = hasTicket && currentTicket != null;
                currentTicketText.gameObject.SetActive(showTicketLabel);
                if (showTicketLabel)
                    currentTicketText.text = "Your ticket";
            }

            if (hasTicket && currentTicket != null)
            {
                for (int i = 0; i < selectedDigits.Length && i < currentTicket.Length; i++)
                {
                    selectedDigits[i] = currentTicket[i];
                }
            }

            if (statusText != null)
            {
                if (lm.IsDrawDayToday())
                    statusText.text = "Draw day! Tickets are closed.";
                else if (hasTicket)
                    statusText.text = "Ticket purchased! Good luck!";
                else
                    statusText.text = "Pick your lucky numbers!";
            }

            if (purchaseButton != null)
                purchaseButton.interactable = lm.CanPurchaseTicket();

            bool canEdit = !hasTicket && !lm.IsDrawDayToday();
            for (int i = 0; i < digitUpButtons.Length; i++)
            {
                if (digitUpButtons[i] != null)
                    digitUpButtons[i].interactable = canEdit;
            }
            for (int i = 0; i < digitDownButtons.Length; i++)
            {
                if (digitDownButtons[i] != null)
                    digitDownButtons[i].interactable = canEdit;
            }

            // RefreshDisplay 时直接 snap 到正确数字，不播动画
            for (int i = 0; i < selectedDigits.Length; i++)
            {
                if (digitReels != null && i < digitReels.Length && digitReels[i] != null)
                    digitReels[i].ScrollToDigit(selectedDigits[i], animate: false);
                else if (i < digitTexts.Length && digitTexts[i] != null)
                    digitTexts[i].text = selectedDigits[i].ToString();
            }
        }
    }
}