using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;
using Pathfinding;
using PopLife.Customers.Runtime;
using PopLife.Customers.Services;
using PopLife.Customers.Data;
using PopLife.Runtime;

namespace PopLife.Customers.NodeCanvas.Actions
{
    [Category("PopLife/Customer")]
    [Description("移动到商店入口并进入（外部街道 → 商店内部）")]
    public class MoveToEntranceAction : ActionTask
    {
        [Tooltip("到达距离（兼容 RVO 局部避障）")]
        public float stoppingDistance = 0.8f;

        [Tooltip("超时时间（秒），超时则失败")]
        public float timeoutSeconds = 30f;

        // 两阶段状态
        private enum Phase { ApproachOutside, CrossToInside }
        private Phase currentPhase;

        private AILerp aiLerp;
        private Seeker seeker;
        private AIDestinationSetter destinationSetter;
        private CustomerBlackboardAdapter customerBlackboard;
        private Transform targetTransform;
        private float startTime;
        private float pauseStartedAt = -1f;
        private bool aiWasStoppedBeforePause;
        private CustomerAnimationController animController;

        // 外部图 GraphMask，用于限制第一阶段寻路 + 进店后排除
        private GraphMask outsideGraphMask;
        private GraphMask entryGraphMask;
        private GraphMask storeInteriorGraphMask;

        protected override string info
        {
            get { return "移动到商店入口"; }
        }

        protected override void OnExecute()
        {
            // 获取组件
            aiLerp = agent.GetComponent<AILerp>();
            seeker = agent.GetComponent<Seeker>();
            destinationSetter = agent.GetComponent<AIDestinationSetter>();
            customerBlackboard = agent.GetComponent<CustomerBlackboardAdapter>();
            animController = agent.GetComponent<CustomerAnimationController>();

            if (aiLerp == null)
            {
                Debug.LogError("[MoveToEntranceAction] 找不到 AILerp 组件");
                EndAction(false);
                return;
            }

            if (customerBlackboard == null)
            {
                Debug.LogError("[MoveToEntranceAction] 找不到 CustomerBlackboardAdapter 组件");
                EndAction(false);
                return;
            }

            if (animController == null)
            {
                Debug.LogError("[MoveToEntranceAction] 找不到 CustomerAnimationController 组件");
                EndAction(false);
                return;
            }

            // 验证入口配置
            if (customerBlackboard.entranceOutsideAnchor == null)
            {
                Debug.LogError($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} 没有设置 entranceOutsideAnchor");
                EndAction(false);
                return;
            }

            if (customerBlackboard.entranceInsideAnchor == null)
            {
                Debug.LogError($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} 没有设置 entranceInsideAnchor");
                EndAction(false);
                return;
            }

            // 设置目标为外部锚点
            targetTransform = customerBlackboard.entranceOutsideAnchor;
            currentPhase = Phase.ApproachOutside;

            // 设置移动速度
            if (customerBlackboard.moveSpeed > 0)
            {
                aiLerp.speed = customerBlackboard.moveSpeed;
            }

            // 第一阶段：显式获取 outside graph，避免在重叠区域被 snap 到内部图
            var nav = PopLife.Customers.Services.NavigationService.Instance;
            if (nav == null || !nav.HasOutsideGraph)
            {
                Debug.LogError("[MoveToEntranceAction] Outside graph not available");
                EndAction(false);
                return;
            }
            outsideGraphMask = nav.GetOutsideGraphMask();
            if (!nav.TryGetEntryMask(customerBlackboard.destinationStoreId, out entryGraphMask)
                || !nav.TryGetStoreInteriorGraphMask(customerBlackboard.destinationStoreId, out storeInteriorGraphMask))
            {
                Debug.LogError($"[MoveToEntranceAction] Store graph mask not available for '{customerBlackboard.destinationStoreId}'");
                EndAction(false);
                return;
            }

            if (seeker != null)
            {
                seeker.graphMask = outsideGraphMask;
            }

            // 设置 A* 寻路目标
            if (destinationSetter != null)
            {
                destinationSetter.target = targetTransform;
            }
            else
            {
                aiLerp.destination = targetTransform.position;
            }

            // 开始移动
            aiLerp.isStopped = false;

            // 记录开始时间
            startTime = Time.time;

            Debug.Log($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} 开始移动到入口外侧 {targetTransform.position}");
        }

        protected override void OnUpdate()
        {
            if (aiLerp == null || customerBlackboard == null)
            {
                EndAction(false);
                return;
            }

            // 检查超时（不受电梯穿越阻止）
            if (Time.time - startTime > timeoutSeconds)
            {
                Debug.LogWarning($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} 移动到入口超时");
                aiLerp.isStopped = true;
                EndAction(false);
                return;
            }

            // 电梯穿越中不做到达判断
            var detector = agent.GetComponent<LinkTraversalDetector>();
            if (detector != null && detector.IsTraversingElevator) return;

            // 到达判断
            bool arrived = aiLerp.reachedDestination;
            if (!arrived && targetTransform != null)
            {
                float dist = Vector3.Distance(agent.transform.position, targetTransform.position);
                arrived = dist <= stoppingDistance;
            }

            if (!arrived) return;

            // 根据当前阶段分发
            if (currentPhase == Phase.ApproachOutside)
            {
                OnReachedOutside();
            }
            else
            {
                OnReachedInside();
            }
        }

        /// <summary>
        /// 第一阶段完成：到达入口外侧，开始穿越 NodeLink2
        /// </summary>
        private void OnReachedOutside()
        {
            Debug.Log($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} 到达入口外侧，准备穿越 NodeLink2");

            aiLerp.isStopped = true;

            // 第二阶段：只允许 outside + 目标 store interior，避免误跨到其他店铺 graph
            if (seeker != null)
            {
                seeker.graphMask = entryGraphMask;
            }

            // 切换目标到内部锚点
            targetTransform = customerBlackboard.entranceInsideAnchor;
            currentPhase = Phase.CrossToInside;

            if (destinationSetter != null)
            {
                destinationSetter.target = targetTransform;
            }
            else
            {
                aiLerp.destination = targetTransform.position;
            }

            aiLerp.isStopped = false;
        }

        /// <summary>
        /// 第二阶段完成：到达入口内侧，正式进入商店
        /// </summary>
        private void OnReachedInside()
        {
            Debug.Log($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} 到达入口内侧，进入商店");

            aiLerp.isStopped = true;

            // 进入店内后只允许当前目标 store 的 interior graph
            if (seeker != null)
            {
                seeker.graphMask = storeInteriorGraphMask;
            }

            // 标记已进入商店
            customerBlackboard.hasEnteredStore = true;
            var customerAgent = agent.GetComponent<CustomerAgent>();
            if (customerAgent != null && customerBlackboard.visitPurpose == CustomerVisitPurpose.PlayerStore)
            {
                CustomerEventBus.RaiseCustomerEnteredStore(customerAgent);
            }

            // 更新黑板的队列位置（用于后续的 MoveToTargetAction）
            customerBlackboard.assignedQueueSlot = targetTransform;

            // 进入商店：切换所有部件 + emoji 到 InsideStoreLayer
            if (animController != null)
            {
                animController.SetAllSortingLayer("InsideStoreLayer");
                Debug.Log($"[MoveToEntranceAction] 顾客 {customerBlackboard.customerId} sortingLayer 切换到 InsideStoreLayer");
            }

            EndAction(true);
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
                startTime += Time.time - pauseStartedAt;
                pauseStartedAt = -1f;
            }

            if (aiLerp != null)
            {
                aiLerp.isStopped = aiWasStoppedBeforePause;
            }
        }
    }
}
