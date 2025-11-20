using UnityEngine;
using Pathfinding;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 管理单个顾客的对话交互状态
    /// Manages the dialogue interaction state for a single customer
    /// </summary>
    public class CustomerInteractionState : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float dialogueStateDuration = 10f;  // 对话等待持续时间
        [SerializeField] private Collider2D interactionCollider;     // 交互碰撞器（仅对话状态时激活）

        // 组件引用
        private FollowerEntity followerEntity;
        private CustomerAnimationController animationController;
        private CustomerBlackboardAdapter blackboardAdapter;

        // 状态
        private bool isInDialogueState = false;
        private bool isWaitingForDialogueEnd = false;  // 对话已触发，等待结束
        private float dialogueStateTimer = 0f;

        // 属性
        public bool IsInDialogueState => isInDialogueState;
        public bool HasTriggeredToday => blackboardAdapter != null && blackboardAdapter.hasTriggeredDialogueToday;

        private void Awake()
        {
            // 获取组件引用
            followerEntity = GetComponent<FollowerEntity>();
            animationController = GetComponent<CustomerAnimationController>();
            blackboardAdapter = GetComponent<CustomerBlackboardAdapter>();

            // 确保交互碰撞器初始状态为禁用
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }
        }

        private void Update()
        {
            // 如果正在等待对话结束，不更新计时器
            if (isWaitingForDialogueEnd)
                return;

            // 更新对话状态计时器
            if (isInDialogueState)
            {
                dialogueStateTimer -= Time.deltaTime;  // 使用游戏时间

                if (dialogueStateTimer <= 0f)
                {
                    // 超时，自动退出对话状态
                    ExitDialogueState();
                }
            }
        }

        /// <summary>
        /// 进入对话等待状态
        /// Enter dialogue waiting state
        /// </summary>
        public void EnterDialogueState()
        {
            if (isInDialogueState)
            {
                Debug.LogWarning($"[CustomerInteractionState] {gameObject.name} 已经处于对话状态");
                return;
            }

            isInDialogueState = true;
            isWaitingForDialogueEnd = false;
            dialogueStateTimer = dialogueStateDuration;

            // 暂停寻路
            if (followerEntity != null)
            {
                followerEntity.isStopped = true;
            }

            // 播放交互循环动画
            if (animationController != null)
            {
                animationController.PlayInteraction();
            }

            // 激活交互碰撞器
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }

            Debug.Log($"[CustomerInteractionState] {gameObject.name} 进入对话等待状态，持续 {dialogueStateDuration} 秒");
        }

        /// <summary>
        /// 退出对话状态
        /// Exit dialogue state
        /// </summary>
        public void ExitDialogueState()
        {
            if (!isInDialogueState)
                return;

            isInDialogueState = false;
            isWaitingForDialogueEnd = false;
            dialogueStateTimer = 0f;

            // 恢复寻路
            if (followerEntity != null)
            {
                followerEntity.isStopped = false;
            }

            // 停止循环动画（恢复自动控制）
            if (animationController != null)
            {
                animationController.StopLoop();
            }

            // 禁用交互碰撞器
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            // 标记今日已触发
            if (blackboardAdapter != null)
            {
                blackboardAdapter.hasTriggeredDialogueToday = true;
            }

            Debug.Log($"[CustomerInteractionState] {gameObject.name} 退出对话状态");
        }

        /// <summary>
        /// 玩家点击触发对话时调用
        /// Called when player clicks to trigger dialogue
        /// </summary>
        public void OnDialogueTriggered()
        {
            if (!isInDialogueState)
            {
                Debug.LogWarning($"[CustomerInteractionState] {gameObject.name} 不在对话状态，无法触发对话");
                return;
            }

            // 停止倒计时，等待对话结束
            isWaitingForDialogueEnd = true;
            dialogueStateTimer = 0f;

            // 禁用交互碰撞器（防止重复点击）
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            Debug.Log($"[CustomerInteractionState] {gameObject.name} 对话已触发，等待对话结束");
        }

        /// <summary>
        /// 对话结束时调用（由 CustomerInteractionManager 调用）
        /// Called when dialogue ends (called by CustomerInteractionManager)
        /// </summary>
        public void OnDialogueEnded()
        {
            if (!isWaitingForDialogueEnd)
            {
                Debug.LogWarning($"[CustomerInteractionState] {gameObject.name} 不在等待对话结束状态");
                return;
            }

            ExitDialogueState();
        }

        /// <summary>
        /// 获取交互碰撞器
        /// Get interaction collider
        /// </summary>
        public Collider2D GetInteractionCollider()
        {
            return interactionCollider;
        }

        /// <summary>
        /// 设置交互碰撞器引用（用于运行时设置）
        /// Set interaction collider reference (for runtime setup)
        /// </summary>
        public void SetInteractionCollider(Collider2D collider)
        {
            interactionCollider = collider;
            if (interactionCollider != null && !isInDialogueState)
            {
                interactionCollider.enabled = false;
            }
        }
    }
}
