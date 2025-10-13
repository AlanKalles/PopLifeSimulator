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

            // Use spawn point as exit point
            if (blackboard.spawnPoint == null)
            {
                Debug.LogWarning("[SelectExitPointAction] Spawn point not set, falling back to nearest exit");

                // Fallback: use nearest exit point
                if (ExitPointManager.Instance == null)
                {
                    Debug.LogError("[SelectExitPointAction] ExitPointManager not found in scene");
                    EndAction(false);
                    return;
                }

                var nearestExit = ExitPointManager.Instance.GetNearestExitPoint(agent.transform.position);
                if (nearestExit == null)
                {
                    Debug.LogError("[SelectExitPointAction] No exit points available");
                    EndAction(false);
                    return;
                }

                targetExitPoint.value = nearestExit.transform;
                blackboard.targetExitPoint = nearestExit.transform;
                if (targetExitId != null)
                {
                    targetExitId.value = nearestExit.exitId;
                    blackboard.targetExitId = nearestExit.exitId;
                }

                Debug.Log($"[SelectExitPointAction] Customer using fallback nearest exit at {nearestExit.transform.position}");
            }
            else
            {
                // Use spawn point as exit
                targetExitPoint.value = blackboard.spawnPoint;
                blackboard.targetExitPoint = blackboard.spawnPoint;

                Debug.Log($"[SelectExitPointAction] Customer returning to spawn point at {blackboard.spawnPoint.position}");
            }

            EndAction(true);
        }
    }
}
