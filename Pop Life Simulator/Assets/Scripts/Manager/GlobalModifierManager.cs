using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife
{
    /// <summary>
    /// 全局修饰器聚合管理器
    /// 监听 CalendarManager.OnCalendarDataChanged，将所有活跃 SeasonEvent / Buffer 的
    /// GlobalModifierPayload 烘焙成缓存的 MasterPayload，供消费端 O(1) 只读查询
    ///
    /// 架构约束：
    /// - 加性百分比叠加（防止乘法爆炸）
    /// - 品类乘数使用固定数组索引（O(1)）
    /// - 不序列化聚合结果（加载时从 SO 重建）
    /// </summary>
    [DefaultExecutionOrder(-40)] // 在 CalendarManager(-50) 之后初始化
    public class GlobalModifierManager : MonoBehaviour
    {
        public static GlobalModifierManager Instance { get; private set; }

        // ── 运行时缓存（MasterPayload）──────────────────────────────

        static readonly int CATEGORY_COUNT = Enum.GetValues(typeof(ProductCategory)).Length;

        // A. 全局乘数
        private float constructionCostMul;
        private float upgradeCostMul;
        private float spawnRateMul;
        private float moneyBagMul;
        private float globalFameMul;
        private float maintenanceCostMul;

        // B. 品类扁平数组
        private float[] categoryPriceMul;
        private float[] categoryFameMul;
        private float[] categoryQuantityMul;

        // C. 货架Appeal加值
        private int globalAppealAddend;
        private Dictionary<ShelfArchetype, int> shelfSpecificAppealAddends;
        private int[] categoryAppealAddend;

        // D. AI行为
        private HashSet<ProductCategory> bonusInterests;

        // E. 补货禁令
        private HashSet<ProductCategory> restockBlockedCategories;

        #region Unity 生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 分配固定数组
            categoryPriceMul = new float[CATEGORY_COUNT];
            categoryFameMul = new float[CATEGORY_COUNT];
            categoryQuantityMul = new float[CATEGORY_COUNT];
            categoryAppealAddend = new int[CATEGORY_COUNT];
            shelfSpecificAppealAddends = new Dictionary<ShelfArchetype, int>();
            bonusInterests = new HashSet<ProductCategory>();
            restockBlockedCategories = new HashSet<ProductCategory>();

            // 设置默认值
            ResetPayload();
        }

        private void Start()
        {
            // 订阅日历数据变化
            if (CalendarManager.Instance != null)
            {
                CalendarManager.Instance.OnCalendarDataChanged += RebakePayload;
            }

            // 安全网：首次手动烘焙
            RebakePayload();
        }

        private void OnDestroy()
        {
            if (CalendarManager.Instance != null)
            {
                CalendarManager.Instance.OnCalendarDataChanged -= RebakePayload;
            }

            if (Instance == this)
                Instance = null;
        }

        #endregion

        #region 聚合逻辑

        /// <summary>
        /// 重新烘焙 MasterPayload：重置 → 聚合所有活跃载荷
        /// 仅在 CalendarManager 广播 OnCalendarDataChanged 时调用
        /// </summary>
        private void RebakePayload()
        {
            ResetPayload();

            if (CalendarManager.Instance == null) return;

            // 聚合今日季节事件
            var events = CalendarManager.Instance.GetTodaySeasonEvents();
            foreach (var evt in events)
            {
                if (evt != null && evt.modifierPayload != null)
                    AggregatePayload(evt.modifierPayload);
            }

            // 聚合活跃 Buffer（跳过尚未到生效日的 pending buffer）
            int today = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
            var buffers = CalendarManager.Instance.GetActiveBuffers();
            foreach (var buf in buffers)
            {
                if (buf?.data == null) continue;
                if (today < buf.startAbsoluteDay) continue;  // 延迟激活：startDay 当天才烘焙生效
                if (buf.data.modifierPayload != null)
                    AggregatePayload(buf.data.modifierPayload);
                // 动态品类禁令（Robot Strike 路径）
                if (buf.hasRolledBlockedCategory)
                    restockBlockedCategories.Add(buf.rolledBlockedCategory);
            }

#if UNITY_EDITOR
            Debug.Log($"[GlobalModifierManager] 烘焙完成: " +
                      $"建造×{constructionCostMul:F2}, 升级×{upgradeCostMul:F2}, " +
                      $"生成速率×{spawnRateMul:F2}, 钱包×{moneyBagMul:F2}, " +
                      $"Fame×{globalFameMul:F2}, 维护×{maintenanceCostMul:F2}, " +
                      $"Appeal全局+{globalAppealAddend}, " +
                      $"货架特定Appeal:{shelfSpecificAppealAddends.Count}项, " +
                      $"奖励兴趣:{bonusInterests.Count}类, " +
                      $"禁补品类:{restockBlockedCategories.Count}类");
#endif
        }

        /// <summary>
        /// 重置所有缓存到默认值
        /// </summary>
        private void ResetPayload()
        {
            constructionCostMul = 1f;
            upgradeCostMul = 1f;
            spawnRateMul = 1f;
            moneyBagMul = 1f;
            globalFameMul = 1f;
            maintenanceCostMul = 1f;

            for (int i = 0; i < CATEGORY_COUNT; i++)
            {
                categoryPriceMul[i] = 1f;
                categoryFameMul[i] = 1f;
                categoryQuantityMul[i] = 1f;
                categoryAppealAddend[i] = 0;
            }

            globalAppealAddend = 0;
            shelfSpecificAppealAddends.Clear();
            bonusInterests.Clear();
            restockBlockedCategories.Clear();
        }

        /// <summary>
        /// 将单个载荷加性叠加到缓存中
        /// 叠加规则：total += (value - 1.0f)，防止乘法爆炸
        /// </summary>
        private void AggregatePayload(GlobalModifierPayload p)
        {
            // 加性叠加全局乘数
            constructionCostMul += (p.constructionCostMultiplier - 1f);
            upgradeCostMul += (p.upgradeCostMultiplier - 1f);
            spawnRateMul += (p.customerSpawnRateMultiplier - 1f);
            moneyBagMul += (p.customerMoneyBagMultiplier - 1f);
            globalFameMul += (p.globalFameGainMultiplier - 1f);
            maintenanceCostMul += (p.maintenanceCostMultiplier - 1f);

            // 加性叠加品类乘数
            if (p.categoryPriceModifiers != null)
            {
                foreach (var cm in p.categoryPriceModifiers)
                    categoryPriceMul[(int)cm.category] += (cm.multiplier - 1f);
            }

            if (p.categoryFameModifiers != null)
            {
                foreach (var cm in p.categoryFameModifiers)
                    categoryFameMul[(int)cm.category] += (cm.multiplier - 1f);
            }

            if (p.categoryQuantityModifiers != null)
            {
                foreach (var cm in p.categoryQuantityModifiers)
                    categoryQuantityMul[(int)cm.category] += (cm.multiplier - 1f);
            }

            // 求和Appeal加值（按货架）
            if (p.shelfAppealModifiers != null)
            {
                foreach (var sam in p.shelfAppealModifiers)
                {
                    if (sam.targetShelf == null)
                    {
                        globalAppealAddend += sam.addend;
                    }
                    else
                    {
                        // 安全处理：不存在的key先初始化为0再累加
                        if (shelfSpecificAppealAddends.TryGetValue(sam.targetShelf, out int existing))
                            shelfSpecificAppealAddends[sam.targetShelf] = existing + sam.addend;
                        else
                            shelfSpecificAppealAddends[sam.targetShelf] = sam.addend;
                    }
                }
            }

            // 求和Appeal加值（按品类）
            if (p.categoryAppealModifiers != null)
            {
                foreach (var cam in p.categoryAppealModifiers)
                    categoryAppealAddend[(int)cam.category] += cam.addend;
            }

            // 并集兴趣品类（HashSet自动去重）
            if (p.bonusInterestCategories != null)
            {
                foreach (var cat in p.bonusInterestCategories)
                    bonusInterests.Add(cat);
            }

            // 并集禁补品类（HashSet自动去重）
            if (p.restockBlockedCategories != null)
            {
                foreach (var cat in p.restockBlockedCategories)
                    restockBlockedCategories.Add(cat);
            }
        }

        #endregion

        #region 只读 Getter API（全部 O(1)）

        // ── 全局乘数 ──

        public float GetConstructionCostMultiplier() => Mathf.Max(0f, constructionCostMul);
        public float GetUpgradeCostMultiplier() => Mathf.Max(0f, upgradeCostMul);
        public float GetSpawnRateMultiplier() => Mathf.Max(0f, spawnRateMul);
        public float GetMoneyBagMultiplier() => Mathf.Max(0f, moneyBagMul);
        public float GetGlobalFameMultiplier() => globalFameMul;
        public float GetMaintenanceCostMultiplier() => Mathf.Max(0f, maintenanceCostMul);

        // ── 品类乘数（数组直接索引）──

        public float GetCategoryPriceMultiplier(ProductCategory cat) => Mathf.Max(0f, categoryPriceMul[(int)cat]);
        public float GetCategoryFameMultiplier(ProductCategory cat) => categoryFameMul[(int)cat];
        public float GetCategoryQuantityMultiplier(ProductCategory cat) => Mathf.Max(0f, categoryQuantityMul[(int)cat]);

        /// <summary>
        /// 组合Fame乘数 = 全局Fame × 品类Fame（允许负数，支持惩罚性事件）
        /// </summary>
        public float GetFameMultiplier(ProductCategory cat) => globalFameMul * categoryFameMul[(int)cat];

        // ── 货架Appeal加值（全局 + 货架特定）──

        /// <summary>
        /// 获取指定货架原型的总Appeal加值
        /// </summary>
        public int GetShelfAppealAddend(ShelfArchetype arch)
        {
            int total = globalAppealAddend;
            if (arch != null && shelfSpecificAppealAddends.TryGetValue(arch, out int specific))
                total += specific;
            return total;
        }

        /// <summary>
        /// 获取指定品类的Appeal加值（C_Lingere Sale 类 Buffer 用）
        /// </summary>
        public int GetCategoryAppealAddend(ProductCategory cat) => categoryAppealAddend[(int)cat];

        // ── AI行为 ──

        /// <summary>
        /// 获取当前活跃的奖励兴趣品类集合（只读引用）
        /// </summary>
        public HashSet<ProductCategory> GetBonusInterestCategories() => bonusInterests;

        // ── 补货禁令 ──

        /// <summary>
        /// 检查指定品类是否被禁止补货（Robot Strike 类 Buffer 用）
        /// </summary>
        public bool IsRestockBlocked(ProductCategory cat) => restockBlockedCategories.Contains(cat);

        #endregion
    }
}
