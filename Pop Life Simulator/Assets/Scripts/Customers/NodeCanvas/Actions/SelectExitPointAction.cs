using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using PopLife.Customers.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("Select the nearest exit point to leave the store")]
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
            get { return "Select nearest exit point"; }
        }

        protected override void OnExecute()
        {
            // Check if ExitPointManager exists
            if (ExitPointManager.Instance == null)
            {
                Debug.LogError("[SelectExitPointAction] ExitPointManager not found in scene");
                EndAction(false);
                return;
            }

            // Get nearest exit point
            var nearestExit = ExitPointManager.Instance.GetNearestExitPoint(agent.transform.position);

            if (nearestExit == null)
            {
                Debug.LogError("[SelectExitPointAction] No exit points available");
                EndAction(false);
                return;
            }

            // Set target exit point Transform (for MoveToTargetAction to use)
            targetExitPoint.value = nearestExit.transform;

            // Optionally set exit ID
            if (targetExitId != null)
            {
                targetExitId.value = nearestExit.exitId;
            }

            // Update CustomerBlackboardAdapter
            var blackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            if (blackboard != null)
            {
                blackboard.targetExitPoint = nearestExit.transform;
                blackboard.targetExitId = nearestExit.exitId;
            }

            Debug.Log($"[SelectExitPointAction] Customer selected exit {nearestExit.exitId} at world position {nearestExit.transform.position}");

            EndAction(true);
        }
    }
}
