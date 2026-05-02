using UnityEngine;
using PopLife.Data;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using PopLife.UI.Quest;

namespace PopLife.Quest
{
    /// <summary>
    /// 任务奖励发放器 - 静态工具类
    /// 根据 QuestDefinition.Rewards 发放奖励
    /// 逻辑与 PopLifeLuaFunctions.GiveReward() 一致
    /// </summary>
    public static class QuestRewardDistributor
    {
        /// <summary>
        /// 发放指定任务的所有奖励
        /// </summary>
        public static void DistributeRewards(string questName)
        {
            var def = QuestDataService.Instance?.GetDefinition(questName);
            if (def == null || def.Rewards == null || def.Rewards.Length == 0) return;

            foreach (var reward in def.Rewards)
            {
                DistributeSingleReward(reward);
            }

            Debug.Log($"[QuestRewardDistributor] 已发放任务 {questName} 的 {def.Rewards.Length} 项奖励");
        }

        public static void DistributeSingleReward(QuestReward reward)
        {
            switch (reward.type)
            {
                case RewardType.Money:
                    // 使用 RefundMoney 不计入销售收入（与 PopLifeLuaFunctions.GiveMoney 一致）
                    if (ResourceManager.Instance != null)
                        ResourceManager.Instance.RefundMoney(reward.amount);
                    Debug.Log($"[QuestRewardDistributor] 发放奖励: Money +{reward.amount}");
                    break;

                case RewardType.Fame:
                    if (ResourceManager.Instance != null)
                        ResourceManager.Instance.AddFame(reward.amount);
                    Debug.Log($"[QuestRewardDistributor] 发放奖励: Fame +{reward.amount}");
                    break;

                case RewardType.Blueprint:
                    if (BlueprintManager.Instance != null && !string.IsNullOrEmpty(reward.itemId))
                        BlueprintManager.Instance.AddBlueprint(reward.itemId);
                    Debug.Log($"[QuestRewardDistributor] 发放奖励: Blueprint {reward.itemId}");
                    break;

                case RewardType.Customer:
                    if (!string.IsNullOrEmpty(reward.itemId))
                    {
                        // itemId stores customer name; resolve to customerId via repository
                        string customerId = reward.itemId;
                        if (CustomerRepository.Instance != null)
                        {
                            foreach (var rec in CustomerRepository.Instance.All())
                            {
                                if (rec.name == reward.itemId)
                                {
                                    customerId = rec.customerId;
                                    break;
                                }
                            }
                        }
                        var profile = SpawnerProfile.Load();
                        if (profile != null && !profile.unlockedCustomerIds.Contains(customerId))
                        {
                            profile.unlockedCustomerIds.Add(customerId);
                            profile.Save();
                        }
                    }
                    Debug.Log($"[QuestRewardDistributor] 发放奖励: Customer {reward.itemId}");
                    break;
            }
        }
    }
}
