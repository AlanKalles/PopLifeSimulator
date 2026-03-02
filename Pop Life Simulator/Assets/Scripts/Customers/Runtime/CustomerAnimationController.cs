using UnityEngine;
using Pathfinding;
using PrimeTween;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 顾客程序化动画控制器
    /// 使用 Sin 波数学计算实现行走/待机动画，PrimeTween 实现特殊动画
    /// 管理6部件 SpriteRenderer + BodyParts 容器翻转
    /// </summary>
    public class CustomerAnimationController : MonoBehaviour
    {
        /// <summary>
        /// 动画状态枚举
        /// </summary>
        private enum AnimState
        {
            Idle,
            Walk,
            Think,
            PickProduct,
            Checkout,
            Upset,
            Interaction
        }

        [Header("部件引用")]
        [SerializeField] private SpriteRenderer headRenderer;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer leftArmRenderer;
        [SerializeField] private SpriteRenderer rightArmRenderer;
        [SerializeField] private SpriteRenderer leftFootRenderer;
        [SerializeField] private SpriteRenderer rightFootRenderer;

        [Header("容器引用")]
        [SerializeField] private Transform bodyPartsContainer;

        [Header("Emoji 控制器")]
        [SerializeField] private CustomerEmojiController emojiController;

        [Header("行走参数")]
        [SerializeField] private float footMoveRange = 0.031f;
        [SerializeField] private float footMoveSpeed = 8f;
        [SerializeField] private float bodyBobAmount = 0.008f;
        [SerializeField] private float armSwingAngle = 10f;

        [Header("待机参数")]
        [SerializeField] private float breatheSpeed = 2f;
        [SerializeField] private float breatheAmount = 0.006f;
        [SerializeField] private float returnToIdleSpeed = 5f;

        [Header("特殊动画参数")]
        [SerializeField] private float pickProductSquatDistance = 0.02f;
        [SerializeField] private float upsetShakeAmplitude = 0.012f;

        [Header("移动检测参数")]
        [SerializeField] private float idleThreshold = 0.1f;

        // 组件缓存
        private IAstarAI ai;

        // 原始位置缓存（Awake 时记录）
        private Vector3 headOriginPos;
        private Vector3 bodyOriginPos;
        private Vector3 leftArmOriginPos;
        private Vector3 rightArmOriginPos;
        private Vector3 leftFootOriginPos;
        private Vector3 rightFootOriginPos;

        // 状态跟踪
        private AnimState currentState = AnimState.Idle;
        private bool isPlayingOneShot;
        private bool isPlayingLoop;

        /// <summary>
        /// 单次特殊动画是否正在播放（供行为树 Action 轮询等待）
        /// </summary>
        public bool IsPlayingOneShot => isPlayingOneShot;
        private Sequence currentOneShotSequence;

        // 所有部件渲染器数组（用于批量操作）
        private SpriteRenderer[] allPartRenderers;

        /// <summary>
        /// 测试用速度覆盖（设置后绕过 AI 速度检测，用于无寻路组件的测试场景）
        /// </summary>
        [HideInInspector] public Vector3? debugVelocityOverride;

        void Awake()
        {
            ai = GetComponent<IAstarAI>();
            if (ai == null)
                Debug.LogWarning($"[CustomerAnimationController] {gameObject.name} 缺少 IAstarAI 组件，将依赖 debugVelocityOverride 驱动行走");

            CacheOriginPositions();

            allPartRenderers = new SpriteRenderer[]
            {
                headRenderer, bodyRenderer, leftArmRenderer,
                rightArmRenderer, leftFootRenderer, rightFootRenderer
            };
        }

        private void CacheOriginPositions()
        {
            if (headRenderer) headOriginPos = headRenderer.transform.localPosition;
            if (bodyRenderer) bodyOriginPos = bodyRenderer.transform.localPosition;
            if (leftArmRenderer) leftArmOriginPos = leftArmRenderer.transform.localPosition;
            if (rightArmRenderer) rightArmOriginPos = rightArmRenderer.transform.localPosition;
            if (leftFootRenderer) leftFootOriginPos = leftFootRenderer.transform.localPosition;
            if (rightFootRenderer) rightFootOriginPos = rightFootRenderer.transform.localPosition;
        }

        /// <summary>
        /// 获取当前速度（优先使用调试覆盖值）
        /// </summary>
        private bool TryGetVelocity(out Vector3 velocity)
        {
            if (debugVelocityOverride.HasValue)
            {
                velocity = debugVelocityOverride.Value;
                return true;
            }
            if (ai != null)
            {
                velocity = ai.velocity;
                return true;
            }
            velocity = Vector3.zero;
            return false;
        }

        // ──────────────────── Update ────────────────────

        void Update()
        {
            // 单次或循环动画播放中，跳过自动状态切换
            if (isPlayingOneShot || isPlayingLoop)
                return;

            UpdateMovementState();

            switch (currentState)
            {
                case AnimState.Walk:
                    AnimateWalk();
                    break;
                case AnimState.Idle:
                    AnimateIdle();
                    break;
            }

            UpdateFacing();
        }

        private void UpdateMovementState()
        {
            if (!TryGetVelocity(out var velocity)) return;
            float speed = velocity.magnitude;
            currentState = speed > idleThreshold ? AnimState.Walk : AnimState.Idle;
        }

        // ──────────────────── Walk 程序化动画 ────────────────────

        private void AnimateWalk()
        {
            float sin = Mathf.Sin(Time.time * footMoveSpeed);

            // 脚步：正半波/负半波交替抬起
            if (rightFootRenderer)
                rightFootRenderer.transform.localPosition =
                    rightFootOriginPos + Vector3.up * (Mathf.Max(0f, sin) * footMoveRange);
            if (leftFootRenderer)
                leftFootRenderer.transform.localPosition =
                    leftFootOriginPos + Vector3.up * (Mathf.Max(0f, -sin) * footMoveRange);

            // 身体和头部微晃（取绝对值使其双倍频率上下晃动）
            float bodyBob = Mathf.Abs(sin) * bodyBobAmount;
            if (bodyRenderer)
                bodyRenderer.transform.localPosition = bodyOriginPos + Vector3.up * bodyBob;
            if (headRenderer)
                headRenderer.transform.localPosition = headOriginPos + Vector3.up * bodyBob;

            // 手臂摆动（Z轴旋转）
            if (leftArmRenderer)
                leftArmRenderer.transform.localRotation = Quaternion.Euler(0, 0, sin * armSwingAngle);
            if (rightArmRenderer)
                rightArmRenderer.transform.localRotation = Quaternion.Euler(0, 0, -sin * armSwingAngle);
        }

        // ──────────────────── Idle 程序化动画 ────────────────────

        private void AnimateIdle()
        {
            float breathe = Mathf.Sin(Time.time * breatheSpeed) * breatheAmount;

            // 身体和头部呼吸起伏
            if (bodyRenderer)
                bodyRenderer.transform.localPosition = bodyOriginPos + Vector3.up * breathe;
            if (headRenderer)
                headRenderer.transform.localPosition = headOriginPos + Vector3.up * breathe;

            // 脚和手臂平滑回到原位
            float lerpT = Time.deltaTime * returnToIdleSpeed;

            if (leftFootRenderer)
                leftFootRenderer.transform.localPosition =
                    Vector3.Lerp(leftFootRenderer.transform.localPosition, leftFootOriginPos, lerpT);
            if (rightFootRenderer)
                rightFootRenderer.transform.localPosition =
                    Vector3.Lerp(rightFootRenderer.transform.localPosition, rightFootOriginPos, lerpT);
            if (leftArmRenderer)
                leftArmRenderer.transform.localRotation =
                    Quaternion.Lerp(leftArmRenderer.transform.localRotation, Quaternion.identity, lerpT);
            if (rightArmRenderer)
                rightArmRenderer.transform.localRotation =
                    Quaternion.Lerp(rightArmRenderer.transform.localRotation, Quaternion.identity, lerpT);
        }

        // ──────────────────── 方向翻转 ────────────────────

        private void UpdateFacing()
        {
            if (bodyPartsContainer == null) return;
            if (!TryGetVelocity(out var velocity)) return;

            if (Mathf.Abs(velocity.x) > 0.01f)
            {
                Vector3 scale = bodyPartsContainer.localScale;
                scale.x = velocity.x > 0 ? 1f : -1f;
                bodyPartsContainer.localScale = scale;
            }
        }

        // ──────────────────── 特殊动画（PrimeTween） ────────────────────

        /// <summary>
        /// 播放拾取商品动画（~2秒）
        /// 身体+头部下蹲 → 回位 + emoji 上升淡出
        /// </summary>
        public void PlayPickProduct()
        {
            StopCurrentAnimation();
            isPlayingOneShot = true;
            currentState = AnimState.PickProduct;

            // 身体+头部下蹲效果
            if (bodyRenderer && headRenderer)
            {
                currentOneShotSequence = Sequence.Create()
                    .Group(Tween.LocalPositionY(bodyRenderer.transform,
                        bodyOriginPos.y - pickProductSquatDistance, 0.4f, Ease.OutQuad))
                    .Group(Tween.LocalPositionY(headRenderer.transform,
                        headOriginPos.y - pickProductSquatDistance, 0.4f, Ease.OutQuad))
                    .Chain(Tween.LocalPositionY(bodyRenderer.transform,
                        bodyOriginPos.y, 0.3f, Ease.InOutQuad))
                    .Group(Tween.LocalPositionY(headRenderer.transform,
                        headOriginPos.y, 0.3f, Ease.InOutQuad))
                    .ChainDelay(1.3f)
                    .ChainCallback(this, target =>
                    {
                        target.isPlayingOneShot = false;
                        target.emojiController?.StopEmoji();
                    });
            }
            else
            {
                // 没有渲染器时只做延迟恢复
                currentOneShotSequence = Sequence.Create()
                    .ChainDelay(2f)
                    .ChainCallback(this, target =>
                    {
                        target.isPlayingOneShot = false;
                        target.emojiController?.StopEmoji();
                    });
            }

            emojiController?.PlayPickProduct();
        }

        /// <summary>
        /// 播放结账动画（0.67秒）
        /// emoji 淡入上升
        /// </summary>
        public void PlayCheckout()
        {
            StopCurrentAnimation();
            isPlayingOneShot = true;
            currentState = AnimState.Checkout;

            currentOneShotSequence = Sequence.Create()
                .ChainDelay(0.67f)
                .ChainCallback(this, target =>
                {
                    target.isPlayingOneShot = false;
                    target.emojiController?.StopEmoji();
                });

            emojiController?.PlayCheckout();
        }

        /// <summary>
        /// 播放 upset 动画（0.85秒）
        /// 身体水平抖动 + emoji 不满表情循环
        /// </summary>
        public void PlayUpset()
        {
            StopCurrentAnimation();
            isPlayingOneShot = true;
            currentState = AnimState.Upset;

            // 衰减水平抖动
            if (bodyPartsContainer)
            {
                Vector3 containerOrigin = bodyPartsContainer.localPosition;
                currentOneShotSequence = Sequence.Create()
                    .Group(Tween.Custom(this, 0f, 1f, 0.85f, (target, t) =>
                    {
                        float shake = Mathf.Sin(t * Mathf.PI * 8) * (1f - t) * target.upsetShakeAmplitude;
                        var pos = containerOrigin;
                        pos.x += shake;
                        target.bodyPartsContainer.localPosition = pos;
                    }, Ease.Linear))
                    .ChainCallback(this, target =>
                    {
                        // 确保容器位置归零
                        target.bodyPartsContainer.localPosition = Vector3.zero;
                        target.isPlayingOneShot = false;
                        target.emojiController?.StopEmoji();
                    });
            }
            else
            {
                currentOneShotSequence = Sequence.Create()
                    .ChainDelay(0.85f)
                    .ChainCallback(this, target =>
                    {
                        target.isPlayingOneShot = false;
                        target.emojiController?.StopEmoji();
                    });
            }

            emojiController?.PlayUpset();
        }

        // ──────────────────── 循环动画 ────────────────────

        /// <summary>
        /// 播放思考动画（循环）
        /// </summary>
        public void PlayThink()
        {
            StopCurrentAnimation();
            isPlayingLoop = true;
            currentState = AnimState.Think;
            emojiController?.PlayThink();
        }

        /// <summary>
        /// 播放交互动画（循环）
        /// </summary>
        public void PlayInteraction()
        {
            StopCurrentAnimation();
            isPlayingLoop = true;
            currentState = AnimState.Interaction;
            emojiController?.PlayInteraction();
        }

        /// <summary>
        /// 停止循环动画，恢复自动控制
        /// </summary>
        public void StopLoop()
        {
            isPlayingLoop = false;
            emojiController?.StopEmoji();
        }

        /// <summary>
        /// 停止当前动画，恢复自动控制
        /// </summary>
        public void StopCurrentAnimation()
        {
            isPlayingOneShot = false;
            isPlayingLoop = false;

            // 停止 PrimeTween 序列
            if (currentOneShotSequence.isAlive)
            {
                currentOneShotSequence.Stop();
            }

            emojiController?.StopEmoji();
            ResetAllParts();
        }

        /// <summary>
        /// 保留接口以便未来扩展（当前空实现）
        /// </summary>
        public void SetCustomerID(string id)
        {
        }

        // ──────────────────── 新增 API ────────────────────

        /// <summary>
        /// 赋值6个部件精灵（由 CustomerAgent 调用）
        /// parts 索引: [0]=Head, [1]=LeftFoot, [2]=RightFoot, [3]=Body, [4]=LeftArm, [5]=RightArm
        /// </summary>
        public void SetupParts(Sprite[] parts)
        {
            if (parts == null || parts.Length < 6)
            {
                Debug.LogWarning($"[CustomerAnimationController] {gameObject.name} parts 数据无效（需要6个）");
                return;
            }

            if (headRenderer) headRenderer.sprite = parts[(int)PartIndex.Head];
            if (leftFootRenderer) leftFootRenderer.sprite = parts[(int)PartIndex.LeftFoot];
            if (rightFootRenderer) rightFootRenderer.sprite = parts[(int)PartIndex.RightFoot];
            if (bodyRenderer) bodyRenderer.sprite = parts[(int)PartIndex.Body];
            if (leftArmRenderer) leftArmRenderer.sprite = parts[(int)PartIndex.LeftArm];
            if (rightArmRenderer) rightArmRenderer.sprite = parts[(int)PartIndex.RightArm];
        }

        /// <summary>
        /// 设置所有部件 + emoji 的 sorting layer（进店/出店切换）
        /// 不改变各部件的相对 sortingOrder
        /// </summary>
        public void SetAllSortingLayer(string layerName)
        {
            foreach (var renderer in allPartRenderers)
            {
                if (renderer != null)
                    renderer.sortingLayerName = layerName;
            }

            emojiController?.SetSortingLayer(layerName);
        }

        // ──────────────────── 内部工具 ────────────────────

        /// <summary>
        /// 重置所有部件到原始位置/旋转
        /// </summary>
        private void ResetAllParts()
        {
            if (headRenderer) headRenderer.transform.localPosition = headOriginPos;
            if (bodyRenderer) bodyRenderer.transform.localPosition = bodyOriginPos;
            if (leftArmRenderer)
            {
                leftArmRenderer.transform.localPosition = leftArmOriginPos;
                leftArmRenderer.transform.localRotation = Quaternion.identity;
            }
            if (rightArmRenderer)
            {
                rightArmRenderer.transform.localPosition = rightArmOriginPos;
                rightArmRenderer.transform.localRotation = Quaternion.identity;
            }
            if (leftFootRenderer) leftFootRenderer.transform.localPosition = leftFootOriginPos;
            if (rightFootRenderer) rightFootRenderer.transform.localPosition = rightFootOriginPos;

            // 重置 BodyParts 容器位置（Upset 抖动可能改变了它）
            if (bodyPartsContainer) bodyPartsContainer.localPosition = Vector3.zero;
        }
    }
}
