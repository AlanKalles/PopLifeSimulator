using UnityEngine;
using TMPro;

namespace PopLife
{
    /// <summary>
    /// 显示玩家资源（金钱和名声值）
    /// </summary>
    public class ResourceDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private TextMeshProUGUI fameText;

        [Header("Display Format")]
        [SerializeField] private string moneyPrefix = "$";
        [SerializeField] private string famePrefix = "Fame: ";

        private void Update()
        {
            if (ResourceManager.Instance != null)
            {
                UpdateMoneyDisplay(ResourceManager.Instance.money);
                UpdateFameDisplay(ResourceManager.Instance.fame);
            }
        }

        private void UpdateMoneyDisplay(int money)
        {
            if (moneyText != null)
            {
                moneyText.text = moneyPrefix + money.ToString("N0");
            }
        }

        private void UpdateFameDisplay(int fame)
        {
            if (fameText != null)
            {
                fameText.text = famePrefix + fame.ToString();
            }
        }
    }
}
