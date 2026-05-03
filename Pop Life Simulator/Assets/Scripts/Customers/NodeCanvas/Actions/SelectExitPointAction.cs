using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("Select spawn point as exit point (customer returns to where they spawned)")]
    public class SelectExitPointAction : ActionTask
    {
        [BlackboardOnly]
        [Tooltip("Target exit point Transform (for MoveToTargetAction)")]
        public BBParameter<Transform> targetExitPoint;

        [BlackboardOnly]
        [Tooltip("Target exit point ID (optional)")]
        public BBParameter<string> targetExitId;

        protected override string info
        {
            get { return "Select spawn point as exit point"; }
        }

        protected override void OnExecute()
        {
            // Get CustomerBlackboardAdapter to access spawn point
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard == null)
            {
                Debug.LogError("[SelectExitPointAction] CustomerBlackboardAdapter not found on agent");
                EndAction(false);
                return;
            }

            Transform chosen = null;
            string source = null;
            string exitId = null;

            // 优先级 1：使用 spawn point（设计意图：customer 撤离回到生成点）
            if (blackboard.spawnPoint != null)
            {
                chosen = blackboard.spawnPoint;
                source = "spawnPoint";
            }
            // 优先级 2：ExitPointManager 按 store 找最近 ExitPoint
            else if (ExitPointManager.Instance != null)
            {
                var nearestExit = ExitPointManager.Instance.GetNearestExitPointForStore(blackboard.destinationStoreId, agent.transform.position);
                if (nearestExit != null)
                {
                    chosen = nearestExit.transform;
                    source = "ExitPointManager.nearest";
                    exitId = nearestExit.exitId;
                }
            }

            // 最终保底：用 entranceOutsideAnchor，避免 ActionList 因 Failure 中断导致 BT 重启循环
            // Failure 会让 LeaveStrategy 失败 → BT repeat:true 重启 → MoveToEntrance 把已离店顾客拉回入口
            if (chosen == null && blackboard.entranceOutsideAnchor != null)
            {
                chosen = blackboard.entranceOutsideAnchor;
                source = "entranceOutsideAnchor (final fallback)";
                Debug.LogWarning($"[SelectExitPointAction] 顾客 {blackboard.customerId} 没有 spawnPoint 且 ExitPointManager 无可用出口，回退到 entranceOutsideAnchor");
            }

            if (exitId != null && targetExitId != null)
            {
                targetExitId.value = exitId;
                blackboard.targetExitId = exitId;
            }

            if (chosen == null)
            {
                Debug.LogError($"[SelectExitPointAction] 顾客 {blackboard.customerId} 无任何可用出口（spawnPoint/ExitPointManager/entranceOutsideAnchor 全为空）");
                EndAction(false);
                return;
            }

            targetExitPoint.value = chosen;
            blackboard.targetExitPoint = chosen;

            Debug.Log($"[SelectExitPointAction] 顾客 {blackboard.customerId} 选择出口 source={source}, pos={chosen.position}, isClosingTime={blackboard.isClosingTime}");
            EndAction(true);
        }
    }
}
