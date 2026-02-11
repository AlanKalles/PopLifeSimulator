using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 详情面板中的奖励条目
    /// 根据 RewardType 显示对应的奖励文本和图标
    /// </summary>
    public class QuestRewardItem : MonoBehaviour
    {
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TextMeshProUGUI rewardText;

        [Header("奖励图标（按类型）")]
        [SerializeField] private Sprite moneyIcon;
        [SerializeField] private Sprite fameIcon;
        [SerializeField] private Sprite blueprintIcon;
        [SerializeField] private Sprite customerIcon;

        public void SetData(QuestReward reward)
        {
            if (rewardText != null)
            {
                rewardText.text = reward.type switch
                {
                    RewardType.Money => $"${reward.amount}",
                    RewardType.Fame => $"{reward.amount} Fame",
                    RewardType.Blueprint => $"Blueprint: {reward.itemId}",
                    RewardType.Customer => $"New Customer: {reward.itemId}",
                    _ => reward.type.ToString()
                };
            }

            if (rewardIcon != null)
            {
                Sprite icon = reward.type switch
                {
                    RewardType.Money => moneyIcon,
                    RewardType.Fame => fameIcon,
                    RewardType.Blueprint => blueprintIcon,
                    RewardType.Customer => customerIcon,
                    _ => null
                };

                if (icon != null)
                {
                    rewardIcon.sprite = icon;
                    rewardIcon.enabled = true;
                }
                else
                {
                    rewardIcon.enabled = false;
                }
            }
        }
    }
}
