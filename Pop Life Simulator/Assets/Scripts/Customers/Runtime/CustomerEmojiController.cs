using UnityEngine;
using PrimeTween;

namespace PopLife.Customers.Runtime
{
    /// <summary>
    /// 独立管理顾客头顶 emoji 表情显示
    /// 挂载在 customer 根物体上，通过 Inspector 引用 emoji 子物体的 SpriteRenderer
    /// emoji 不在 BodyParts 容器下，不随身体翻转，始终朝向玩家
    /// </summary>
    public class CustomerEmojiController : MonoBehaviour
    {
        [Header("组件引用")]
        [SerializeField] private SpriteRenderer emojiRenderer;

        [Header("表情精灵")]
        [SerializeField] private Sprite[] thinkSprites;        // 思考表情帧（循环）
        [SerializeField] private Sprite[] upsetSprites;        // 不满表情帧（循环）
        [SerializeField] private Sprite[] interactionSprites;  // 交互表情帧（循环）
        [SerializeField] private Sprite checkoutSprite;        // 结账表情（单帧）
        [SerializeField] private Sprite pickProductSprite;     // 拿货表情（单帧）

        [Header("动画参数")]
        [SerializeField] private float frameInterval = 0.3f;   // 循环帧切换间隔
        [SerializeField] private float riseDistance = 0.117f;   // 上升距离
        [SerializeField] private float riseDuration = 0.5f;    // 上升时长
        [SerializeField] private float fadeDuration = 0.3f;    // 淡入淡出时长

        // 内部状态
        private Transform emojiTransform;
        private Vector3 originLocalPos;
        private Sequence currentSequence;
        private Sprite[] currentLoopSprites;
        private int currentFrameIndex;
        private float frameTimer;
        private bool isLooping;

        void Awake()
        {
            if (emojiRenderer != null)
            {
                emojiTransform = emojiRenderer.transform;
                originLocalPos = emojiTransform.localPosition;
                emojiRenderer.sprite = null;
            }
        }

        void Update()
        {
            if (!isLooping || currentLoopSprites == null || currentLoopSprites.Length == 0)
                return;

            frameTimer += Time.deltaTime;
            if (frameTimer >= frameInterval)
            {
                frameTimer -= frameInterval;
                currentFrameIndex = (currentFrameIndex + 1) % currentLoopSprites.Length;
                emojiRenderer.sprite = currentLoopSprites[currentFrameIndex];
            }
        }

        // ──────────────────── 循环播放 ────────────────────

        /// <summary>
        /// 播放思考表情（循环）
        /// </summary>
        public void PlayThink()
        {
            StopEmoji();
            StartLoop(thinkSprites);
        }

        /// <summary>
        /// 播放不满表情（循环）
        /// </summary>
        public void PlayUpset()
        {
            StopEmoji();
            StartLoop(upsetSprites);
        }

        /// <summary>
        /// 播放交互表情（循环）
        /// </summary>
        public void PlayInteraction()
        {
            StopEmoji();
            StartLoop(interactionSprites);
        }

        private void StartLoop(Sprite[] sprites)
        {
            if (emojiRenderer == null || sprites == null || sprites.Length == 0)
                return;

            currentLoopSprites = sprites;
            currentFrameIndex = 0;
            frameTimer = 0f;
            isLooping = true;
            emojiRenderer.sprite = sprites[0];
            emojiRenderer.color = Color.white;
        }

        // ──────────────────── 单次播放 ────────────────────

        /// <summary>
        /// 播放拿货表情：显示 sprite + 上升 + 淡出
        /// </summary>
        public void PlayPickProduct()
        {
            StopEmoji();
            if (emojiRenderer == null || pickProductSprite == null) return;

            emojiRenderer.sprite = pickProductSprite;
            emojiRenderer.color = Color.white;
            emojiTransform.localPosition = originLocalPos;

            currentSequence = Sequence.Create()
                .Group(Tween.LocalPositionY(emojiTransform, originLocalPos.y + riseDistance,
                    riseDuration, Ease.OutQuad))
                .Chain(Tween.Custom(this, 1f, 0f, fadeDuration, (target, val) =>
                {
                    if (target.emojiRenderer != null)
                        target.emojiRenderer.color = new Color(1f, 1f, 1f, val);
                }, Ease.InQuad));
        }

        /// <summary>
        /// 播放结账表情：淡入 + 上升
        /// </summary>
        public void PlayCheckout()
        {
            StopEmoji();
            if (emojiRenderer == null || checkoutSprite == null) return;

            emojiRenderer.sprite = checkoutSprite;
            emojiRenderer.color = new Color(1f, 1f, 1f, 0f);
            emojiTransform.localPosition = originLocalPos;

            currentSequence = Sequence.Create()
                .Group(Tween.LocalPositionY(emojiTransform, originLocalPos.y + riseDistance,
                    riseDuration, Ease.OutQuad))
                .Group(Tween.Custom(this, 0f, 1f, fadeDuration, (target, val) =>
                {
                    if (target.emojiRenderer != null)
                        target.emojiRenderer.color = new Color(1f, 1f, 1f, val);
                }, Ease.Linear));
        }

        // ──────────────────── 停止 ────────────────────

        /// <summary>
        /// 停止所有 emoji 动画，重置到初始状态
        /// </summary>
        public void StopEmoji()
        {
            isLooping = false;
            currentLoopSprites = null;

            // 停止正在播放的 PrimeTween 序列
            if (currentSequence.isAlive)
            {
                currentSequence.Stop();
            }

            ResetEmoji();
        }

        /// <summary>
        /// 设置 emoji 的 sorting layer
        /// </summary>
        public void SetSortingLayer(string layerName)
        {
            if (emojiRenderer != null)
            {
                emojiRenderer.sortingLayerName = layerName;
            }
        }

        private void ResetEmoji()
        {
            if (emojiRenderer != null)
            {
                emojiRenderer.sprite = null;
                emojiRenderer.color = Color.white;
            }
            if (emojiTransform != null)
            {
                emojiTransform.localPosition = originLocalPos;
            }
        }
    }
}
