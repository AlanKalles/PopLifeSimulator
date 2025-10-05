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
            if (showTime && DayLoopManager.Instance != null)
            {
                UpdateTimeDisplay();
            }
        }

        private void UpdateDayDisplay(int day)
        {
            if (dayText != null)
            {
                dayText.text = string.Format(dayFormat, day);
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
