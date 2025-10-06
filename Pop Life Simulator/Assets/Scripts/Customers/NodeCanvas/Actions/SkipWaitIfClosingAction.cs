using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("条件等待（闭店时缩短等待时间）")]
    public class SkipWaitIfClosingAction : ActionTask
    {
        [Tooltip("正常营业时的等待时间（秒）")]
        public float normalWaitTime = 1.0f;

        [Tooltip("闭店时的等待时间（秒）")]
        public float urgentWaitTime = 0.1f;

        private float elapsedTime;
        private float targetWaitTime;

        protected override string info
        {
            get { return $"Wait ({normalWaitTime}s / {urgentWaitTime}s)"; }
        }

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[SkipWaitIfClosing] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            elapsedTime = 0f;
            targetWaitTime = blackboard.isClosingTime ? urgentWaitTime : normalWaitTime;

            Debug.Log($"[SkipWaitIfClosing] Customer {blackboard.customerId} 等待 {targetWaitTime}s (closing: {blackboard.isClosingTime})");
        }

        protected override void OnUpdate()
        {
            elapsedTime += Time.deltaTime;

            if (elapsedTime >= targetWaitTime)
            {
                EndAction(true);
            }
        }
    }
}
