using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;

namespace PopLife.Customers.NodeCanvas.Conditions
{
    [Category("PopLife/Customer")]
    [Description("检查顾客是否应该进入对话交互状态")]
    public class ShouldEnterInteractionCondition : ConditionTask
    {
        protected override string info
        {
            get { return "Should Enter Interaction?"; }
        }

        protected override bool OnCheck()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[ShouldEnterInteractionCondition] CustomerBlackboardAdapter not found!");
                return false;
            }

            // 检查条件：
            // 1. 已进入商店
            if (!blackboard.hasEnteredStore)
            {
                return false;
            }

            // 2. 不处于 upset 状态
            if (blackboard.isUpset)
            {
                return false;
            }

            // 3. 不是关店时间
            if (blackboard.isClosingTime)
            {
                return false;
            }

            // 4. 今日未触发过对话
            if (blackboard.hasTriggeredDialogueToday)
            {
                return false;
            }

            // 5. 检查是否有可用的对话ID
            var customerAgent = agent.GetComponent<CustomerAgent>();
            if (customerAgent != null)
            {
                var record = customerAgent.GetCustomerRecord();
                if (record == null || record.availableNarrativeIds == null || record.availableNarrativeIds.Length == 0)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            // 6. 随机概率检查
            float probability = CustomerInteractionManager.Instance != null
                ? CustomerInteractionManager.Instance.TriggerProbability
                : 0.15f;

            bool shouldTrigger = Random.value < probability;

            if (shouldTrigger)
            {
                Debug.Log($"[ShouldEnterInteractionCondition] 顾客 {blackboard.customerId} 触发对话交互（概率: {probability * 100}%）");
            }

            return shouldTrigger;
        }
    }
}
