using UnityEngine;
using PopLife.Customers.Data;
using PopLife.Customers.Services;
using TMPro;


namespace PopLife.Customers.Runtime
{
    [RequireComponent(typeof(CustomerBlackboardAdapter))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CustomerAnimationController))]
    public class CustomerAgent : MonoBehaviour
    {
        public CustomerBlackboardAdapter bb;
        public string customerID;
        public AppearanceDatabase appearanceDB;

        [Header("动画覆盖")]
        [Tooltip("基础 Override Controller（在 Inspector 中设置其 Controller 为 customer.controller）")]
        public AnimatorOverrideController baseOverrideController;

        // 当前访问会话
        public CustomerSession currentSession;

        // 缓存的原型和特质（用于经验计算）
        public CustomerArchetype cachedArchetype;
        public Trait[] cachedTraits;

        // 缓存的 CustomerRecord（用于交互系统获取对话ID）
        private CustomerRecord cachedRecord;

        private SpriteRenderer spriteRenderer;
        private TextMeshPro nameText;
        private CustomerAnimationController animationController;
        private Animator animator;

        void Awake()
        {
            if (!bb) bb = GetComponent<CustomerBlackboardAdapter>();
            if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
            if (!nameText) nameText = GetComponentInChildren<TextMeshPro>();
            if (!animationController) animationController = GetComponent<CustomerAnimationController>();
            if (!animator) animator = GetComponent<Animator>();

            // 禁用 Animator 直到 Initialize 完成，防止动画覆盖 sprite
            if (animator != null)
            {
                animator.enabled = false;
            }
        }


// 原型期的最小初始化：由 Spawner 调用
        public void Initialize(CustomerRecord record, CustomerArchetype archetype, Trait[] traits, int categories, int daySeed)
        {
// 0) 设置顾客ID
            customerID = record.customerId;

// 0.1) 缓存 CustomerRecord（用于交互系统获取对话ID）
            cachedRecord = record;

// 1) 设置外貌
            if (!string.IsNullOrEmpty(record.appearanceId) && appearanceDB != null)
            {
                Sprite sprite = appearanceDB.Get(record.appearanceId);
                Debug.Log($"[CustomerAgent] {customerID}: appearanceId='{record.appearanceId}', sprite={sprite}, spriteRenderer={spriteRenderer}");
                if (sprite != null && spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    Debug.Log($"[CustomerAgent] {customerID}: sprite 设置成功, spriteRenderer.sprite={spriteRenderer.sprite}");
                }
                else if (sprite == null)
                {
                    Debug.LogWarning($"CustomerAgent: 找不到外貌ID '{record.appearanceId}' 对应的Sprite");
                }
            }

// 2) 最终兴趣（含 Trait interest 修正，已在 Record 组合）
            var finalInterest = record.ComposeFinalInterest(archetype, categories, traits);


// 3) Trait 乘子
            var eff = TraitResolver.Compute(traits);


// 4) 采样本次钱袋与尴尬上限（曲线 × Trait 乘子）
            int walletCap = Mathf.RoundToInt(record.walletCapBase * archetype.walletCapCurve.Eval(record.loyaltyLevel) * eff.walletCapMul);
            int embarrassmentCap = Mathf.RoundToInt(archetype.embarrassmentCapCurve.Eval(record.loyaltyLevel) * eff.embarrassmentCapMul);
            int queueTolerance = Mathf.RoundToInt(archetype.queueToleranceSeconds * eff.patienceMul);
            float finalMoveSpeed = archetype.moveSpeed * eff.moveSpeedMul;


// 5) 注入黑板
            bb.InjectFromRecord(record, archetype, finalInterest, embarrassmentCap, finalMoveSpeed);
            bb.moneyBag = Random.Range(Mathf.Max(10, walletCap/2), walletCap + 1);
            bb.embarrassment = 0;
            bb.queueToleranceSec = queueTolerance;

// 6) 设置头顶名字显示
            if (nameText != null)
            {
                nameText.text = record.name;
            }

// 7) 创建当前会话
            currentSession = new CustomerSession
            {
                customerId = record.customerId,
                dayId = PopLife.DayLoopManager.Instance?.currentDay.ToString() ?? "0",
                sessionId = System.Guid.NewGuid().ToString(),
                moneyBagStart = bb.moneyBag,
                moneySpent = 0,
                trustDelta = 0,
                visitedShelves = new System.Collections.Generic.List<ShelfVisit>()
            };

// 8) 缓存原型和特质（用于销毁时计算经验）
            cachedArchetype = archetype;
            cachedTraits = traits;

// 9) 设置动画覆盖控制器（如果有专属walk动画）
            SetupAnimatorOverride(customerID);

// 10) 设置动画控制器的顾客ID（用于播放专属动画）
            if (animationController != null)
            {
                animationController.SetCustomerID(customerID);
            }

// 11) 启用 Animator（在设置完 sprite 和动画ID之后）
            if (animator != null)
            {
                animator.enabled = true;
            }

            CustomerEventBus.RaiseSpawned(this);
        }

        /// <summary>
        /// 设置动画覆盖控制器
        /// 如果存在 {customerID}_walk 动画，则创建 Override Controller 副本并替换 walk 动画
        /// </summary>
        private void SetupAnimatorOverride(string id)
        {
            if (animator == null || baseOverrideController == null || string.IsNullOrEmpty(id))
                return;

            // 尝试加载专属 walk 动画
            string walkAnimPath = $"Animations/{id}_walk";
            AnimationClip customWalkClip = Resources.Load<AnimationClip>(walkAnimPath);

            if (customWalkClip == null)
            {
                // 没有专属动画，使用原始控制器
                animator.runtimeAnimatorController = baseOverrideController.runtimeAnimatorController;
                return;
            }

            // 创建 Override Controller 副本
            var overrideController = new AnimatorOverrideController(baseOverrideController.runtimeAnimatorController);

            // 替换 customer_walk 为专属动画
            overrideController["customer_walk"] = customWalkClip;

            animator.runtimeAnimatorController = overrideController;
            Debug.Log($"[CustomerAgent] {id}: 已设置专属 walk 动画");
        }

        /// <summary>
        /// 获取缓存的 CustomerRecord
        /// Get cached CustomerRecord
        /// </summary>
        public CustomerRecord GetCustomerRecord()
        {
            return cachedRecord;
        }

        /// <summary>
        /// 获取特质声望贡献倍率
        /// Get trait fame multiplier
        /// </summary>
        public float GetTraitFameMul()
        {
            if (cachedTraits == null || cachedTraits.Length == 0) return 1f;
            return TraitResolver.Compute(cachedTraits).fameMul;
        }
    }
}