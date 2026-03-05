using UnityEngine;
using TMPro;
using PopLife.Data;

namespace PopLife
{
    public class DayCounterUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private bool showTime = true;

        [Header("Season Colors")]
        [SerializeField] private Color springColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color summerColor = new Color(0.95f, 0.85f, 0.1f);
        [SerializeField] private Color autumnColor = new Color(0.95f, 0.55f, 0.1f);
        [SerializeField] private Color winterColor = new Color(0.3f, 0.6f, 1f);

        private void OnEnable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged += UpdateDayDisplay;
                UpdateDisplay();
            }
        }

        private void OnDisable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnDayChanged -= UpdateDayDisplay;
            }
        }

        private void Update()
        {
            if (DayLoopManager.Instance != null)
            {
                // 每帧更新天数显示（因为可能从当日跳到次日）
                UpdateDayDisplay(DayLoopManager.Instance.currentDay);

                if (showTime)
                {
                    UpdateTimeDisplay();
                }
            }
        }

        private void UpdateDayDisplay(int day)
        {
            if (dayText != null && DayLoopManager.Instance != null)
            {
                int displayDay = day;

                // 如果当前时间超过24小时，显示次日
                if (DayLoopManager.Instance.currentHour >= 24f)
                {
                    displayDay = day + 1;
                }

                var date = CalendarUtils.AbsoluteDayToDate(displayDay, DayLoopManager.Instance.StartingYear);
                string seasonName = date.season.ToString();
                string seasonHex = ColorUtility.ToHtmlStringRGB(GetSeasonColor(date.season));

                dayText.text = $"<color=#{seasonHex}>{seasonName}</color> Day {date.dayInSeason}";
            }
        }

        private Color GetSeasonColor(Season season) => season switch
        {
            Season.Spring => springColor,
            Season.Summer => summerColor,
            Season.Autumn => autumnColor,
            Season.Winter => winterColor,
            _ => Color.white
        };

        private void UpdateTimeDisplay()
        {
            if (timeText != null && DayLoopManager.Instance != null)
            {
                timeText.text = DayLoopManager.Instance.GetFormattedTime();
            }
        }

        private void UpdateDisplay()
        {
            if (DayLoopManager.Instance != null)
            {
                UpdateDayDisplay(DayLoopManager.Instance.currentDay);
                UpdateTimeDisplay();
            }
        }
    }
}
