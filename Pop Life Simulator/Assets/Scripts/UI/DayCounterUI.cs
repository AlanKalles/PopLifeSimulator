using UnityEngine;
using TMPro;

namespace PopLife
{
    public class DayCounterUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private string dayFormat = "Day {0}";
        [SerializeField] private bool showTime = true;

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

                dayText.text = string.Format(dayFormat, displayDay);
            }
        }

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
