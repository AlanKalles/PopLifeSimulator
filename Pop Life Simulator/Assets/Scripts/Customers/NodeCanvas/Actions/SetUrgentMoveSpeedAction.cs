using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Store")]
    [Description("设置紧急移动速度（闭店时加速）")]
    public class SetUrgentMoveSpeedAction : ActionTask
    {
        [Tooltip("闭店时的速度倍率")]
        public float urgentSpeedMultiplier = 2.0f;

        protected override string info
        {
            get { return $"Set Speed (×{urgentSpeedMultiplier} if closing)"; }
        }

        protected override void OnExecute()
        {
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[SetUrgentMoveSpeed] 找不到 CustomerBlackboardAdapter");
                EndAction(false);
                return;
            }

            var follower = agent.GetComponent<FollowerEntity>();
            if (follower == null)
            {
                Debug.LogError("[SetUrgentMoveSpeed] 找不到 FollowerEntity 组件");
                EndAction(false);
                return;
            }

            float finalSpeed = blackboard.moveSpeed;

            if (blackboard.isClosingTime)
            {
                finalSpeed *= urgentSpeedMultiplier;
                Debug.Log($"[SetUrgentMoveSpeed] Customer {blackboard.customerId} 紧急模式: speed = {finalSpeed} (×{urgentSpeedMultiplier})");
            }
            else
            {
                Debug.Log($"[SetUrgentMoveSpeed] Customer {blackboard.customerId} 正常速度: {finalSpeed}");
            }

            follower.maxSpeed = finalSpeed;
            EndAction(true);
        }
    }
}
