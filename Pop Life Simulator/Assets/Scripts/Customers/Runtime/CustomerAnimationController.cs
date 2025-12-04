using System.Collections;
using UnityEngine;
using Pathfinding;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 顾客动画控制器
    /// 自动管理移动动画、朝向翻转，并提供手动触发特殊动画的接口
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CustomerAnimationController : MonoBehaviour
    {
        [Header("组件引用")]
        [Tooltip("顾客的 SpriteRenderer，用于翻转朝向")]
        public SpriteRenderer spriteRenderer;

        [Header("移动检测参数")]
        [Tooltip("低于此速度视为静止")]
        public float idleThreshold = 0.1f;

        // 组件缓存
        private Animator animator;
        private IAstarAI ai; // FollowerEntity 实现此接口

        // 动画状态哈希（性能优化）
        private static readonly int WalkHash = Animator.StringToHash("customer_walk");
        private static readonly int IdleHash = Animator.StringToHash("customer_idle");
        private static readonly int ThinkHash = Animator.StringToHash("customer_think");
        private static readonly int InteractionHash = Animator.StringToHash("customer_interaction");
        private static readonly int PickProductHash = Animator.StringToHash("customer_pickproduct");
        private static readonly int CheckoutHash = Animator.StringToHash("customer_checkout");
        private static readonly int UpsetHash = Animator.StringToHash("customer_upset");

        // 状态跟踪
        private string currentState;
        private bool isPlayingOneShot = false; // 是否正在播放单次动画
        private bool isPlayingLoop = false;    // 是否正在播放手动循环动画

        void Awake()
        {
            animator = GetComponent<Animator>();
            ai = GetComponent<IAstarAI>();

            if (animator == null)
            {
                Debug.LogError($"[CustomerAnimationController] {gameObject.name} 缺少 Animator 组件！");
            }

            if (ai == null)
            {
                Debug.LogError($"[CustomerAnimationController] {gameObject.name} 缺少 IAstarAI 组件（FollowerEntity）！");
            }

            if (spriteRenderer == null)
            {
                Debug.LogWarning($"[CustomerAnimationController] {gameObject.name} 未分配 SpriteRenderer，将尝试自动获取");
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// 设置顾客ID（保留接口以便未来扩展）
        /// 专属动画现在通过 AnimatorOverrideController 在 CustomerAgent 中处理
        /// </summary>
        /// <param name="id">顾客ID，如 "C001"</param>
        public void SetCustomerID(string id)
        {
            // 专属动画现在由 CustomerAgent.SetupAnimatorOverride() 处理
            // 此方法保留以便未来扩展其他顾客专属行为
        }

        void Update()
        {
            // 如果正在播放单次或手动循环动画，不自动切换
            if (isPlayingOneShot || isPlayingLoop)
            {
                return;
            }

            // 自动根据速度切换移动/待机动画
            UpdateMovementAnimation();
        }

        /// <summary>
        /// 根据速度自动更新移动动画和朝向
        /// </summary>
        private void UpdateMovementAnimation()
        {
            if (ai == null || animator == null)
            {
                return;
            }

            // 获取速度
            Vector3 velocity = ai.velocity;
            float speed = velocity.magnitude;

            if (speed > idleThreshold)
            {
                // 移动中：播放行走动画
                // 专属动画通过 AnimatorOverrideController 自动替换 customer_walk
                PlayState("customer_walk");

                // 根据 X 方向翻转 Sprite
                if (spriteRenderer != null && velocity.x != 0)
                {
                    if (velocity.x > 0)
                    {
                        spriteRenderer.flipX = false; // 朝右（默认方向）
                    }
                    else
                    {
                        spriteRenderer.flipX = true; // 朝左（镜像翻转）
                    }
                }
            }
            else
            {
                // 静止：播放待机动画
                PlayState("customer_idle");
            }
        }

        /// <summary>
        /// 播放动画状态（避免重复切换）
        /// </summary>
        /// <param name="stateName">动画状态名称</param>
        private void PlayState(string stateName)
        {
            if (animator == null)
            {
                return;
            }

            if (currentState != stateName)
            {
                animator.Play(stateName);
                currentState = stateName;
            }
        }

        /// <summary>
        /// 播放单次动画（如 pickproduct, checkout）
        /// </summary>
        /// <param name="stateName">动画状态名称</param>
        /// <param name="duration">动画持续时间（秒）</param>
        public void PlayOneShot(string stateName, float duration)
        {
            if (animator == null)
            {
                return;
            }

            animator.Play(stateName);
            currentState = stateName;
            isPlayingOneShot = true;

            // 延迟后恢复自动控制
            StartCoroutine(ResetOneShotAfterDelay(duration));
        }

        /// <summary>
        /// 延迟后重置单次动画标志
        /// </summary>
        private IEnumerator ResetOneShotAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            isPlayingOneShot = false;
        }

        /// <summary>
        /// 播放循环动画（如 think, interaction）
        /// </summary>
        /// <param name="stateName">动画状态名称</param>
        public void PlayLoop(string stateName)
        {
            if (animator == null)
            {
                return;
            }

            PlayState(stateName);
            isPlayingLoop = true;
        }

        /// <summary>
        /// 停止循环动画，恢复自动控制
        /// </summary>
        public void StopLoop()
        {
            isPlayingLoop = false;
        }

        /// <summary>
        /// 播放拾取商品动画（0.25秒）
        /// </summary>
        public void PlayPickProduct()
        {
            PlayOneShot("customer_pickproduct", 2f);
        }

        /// <summary>
        /// 播放结账动画（0.67秒）
        /// </summary>
        public void PlayCheckout()
        {
            PlayOneShot("customer_checkout", 0.67f);
        }

        /// <summary>
        /// 播放 upset 动画（0.85秒）
        /// </summary>
        public void PlayUpset()
        {
            PlayOneShot("customer_upset", 0.85f);
        }

        /// <summary>
        /// 播放思考动画（循环）
        /// </summary>
        public void PlayThink()
        {
            PlayLoop("customer_think");
        }

        /// <summary>
        /// 播放交互动画（循环）
        /// </summary>
        public void PlayInteraction()
        {
            PlayLoop("customer_interaction");
        }

        /// <summary>
        /// 停止当前动画，恢复自动控制
        /// </summary>
        public void StopCurrentAnimation()
        {
            isPlayingOneShot = false;
            isPlayingLoop = false;
        }
    }
}
