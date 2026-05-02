using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("Move to a club stay point, idle, then continue.")]
    public class ClubStayPointAction : ActionTask
    {
        [BlackboardOnly]
        public BBParameter<Transform> trackingPoint;

        [BlackboardOnly]
        public BBParameter<float> idleSeconds;

        public float stoppingDistance = 0.5f;
        public float timeoutSeconds = 30f;

        private enum Phase { Moving, Idling }

        private Phase phase;
        private AILerp aiLerp;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter blackboard;
        private float startedAt;
        private float phaseStartedAt;
        private float pauseStartedAt = -1f;
        private bool aiWasStoppedBeforePause;

        protected override string info => "Club Stay Point";

        protected override void OnExecute()
        {
            aiLerp = agent.GetComponent<AILerp>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            blackboard = agent.GetComponent<CustomerBlackboardAdapter>();

            if (aiLerp == null)
            {
                Debug.LogError("[ClubStayPointAction] Missing AILerp.");
                EndAction(false);
                return;
            }

            if (trackingPoint.value == null)
            {
                Debug.LogError($"[ClubStayPointAction] trackingPoint missing for {agent.name}, skipping idle.");
                aiLerp.isStopped = true;
                EndAction(true);
                return;
            }

            if (blackboard != null && blackboard.moveSpeed > 0)
            {
                aiLerp.speed = blackboard.moveSpeed;
            }

            if (destinationSetter != null)
            {
                destinationSetter.target = trackingPoint.value;
            }
            else
            {
                aiLerp.destination = trackingPoint.value.position;
            }

            aiLerp.isStopped = false;
            aiLerp.SearchPath();
            phase = Phase.Moving;
            startedAt = Time.time;
            phaseStartedAt = Time.time;
        }

        protected override void OnUpdate()
        {
            if (aiLerp == null)
            {
                EndAction(false);
                return;
            }

            if (Time.time - startedAt > timeoutSeconds)
            {
                Debug.LogWarning($"[ClubStayPointAction] Timeout for {blackboard?.customerId}; continuing to exit.");
                aiLerp.isStopped = true;
                EndAction(true);
                return;
            }

            if (phase == Phase.Moving)
            {
                bool arrived = aiLerp.reachedDestination;
                if (!arrived && trackingPoint.value != null)
                {
                    arrived = Vector3.Distance(agent.transform.position, trackingPoint.value.position) <= stoppingDistance;
                }

                if (arrived)
                {
                    aiLerp.isStopped = true;
                    phase = Phase.Idling;
                    phaseStartedAt = Time.time;
                }
            }
            else
            {
                float wait = blackboard != null && blackboard.isClosingTime
                    ? Mathf.Min(0.2f, Mathf.Max(0f, idleSeconds.value))
                    : Mathf.Max(0f, idleSeconds.value);

                if (Time.time - phaseStartedAt >= wait)
                {
                    EndAction(true);
                }
            }
        }

        protected override void OnStop()
        {
            if (aiLerp != null)
            {
                aiLerp.isStopped = true;
            }
        }

        protected override void OnPause()
        {
            pauseStartedAt = Time.time;
            if (aiLerp != null)
            {
                aiWasStoppedBeforePause = aiLerp.isStopped;
                aiLerp.isStopped = true;
            }
        }

        protected override void OnResume()
        {
            if (pauseStartedAt >= 0f)
            {
                float pausedFor = Time.time - pauseStartedAt;
                startedAt += pausedFor;
                phaseStartedAt += pausedFor;
                pauseStartedAt = -1f;
            }

            if (aiLerp != null)
            {
                aiLerp.isStopped = aiWasStoppedBeforePause;
            }
        }
    }
}
