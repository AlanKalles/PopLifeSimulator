using System;
using System.Collections;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace PopLife
{
    /// <summary>
    /// 破产/负债管理器：在每日维护费扣除后判定玩家是否进入还债失败/首次负债态，
    /// 触发对应 Midori 对话与补助金额，或直接 Game Over。
    ///
    /// 设计要点：
    /// - 玩家可因维护费导致负债，建造系统已有 CanAfford 阻止建造负债，本管理器不参与建造逻辑。
    /// - debtWarningActive 在所有处理（含补助到账）后用最终金额更新；补助让钱回正后下一天不再被判定为"还在债中"。
    /// - debtFailureCount 终生累计，永不重置——补助梯度递增带来递增压力。
    /// - 第 4 次还债失败 = 真正破产 → 跳过结算面板，由 DayLoopManager 显示破产面板。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class BankruptcyManager : MonoBehaviour
    {
        public static BankruptcyManager Instance;

        [Header("Sponsorship Configuration (designer-tuneable)")]
        [SerializeField] private int firstFailureSponsorship = 100;
        [SerializeField] private int secondFailureSponsorship = 300;
        [SerializeField] private int thirdFailureSponsorship = 500;
        [SerializeField] private int gameOverFailureCount = 4;

        [Header("Dialogue Conversations")]
        [SerializeField] private string firstDebtConversation = "Midori/Bankrupt/1";
        [SerializeField] private string firstFailureConversation = "Midori/Bankrupt/2";
        [SerializeField] private string secondFailureConversation = "Midori/Bankrupt/3";
        [SerializeField] private string thirdFailureConversation = "Midori/Bankrupt/4";

        [Header("Debug (read-only)")]
        [SerializeField] private bool dbgHasFirstDebtTriggered;
        [SerializeField] private int dbgDebtFailureCount;
        [SerializeField] private bool dbgDebtWarningActive;

        // Persisted state (ES3) — 在 Start 阶段加载，遵循 RestockManager 约束
        private bool hasFirstDebtTriggered;
        private int debtFailureCount;        // lifetime, never reset
        private bool debtWarningActive;      // 上次结算后玩家是否仍负债（用最终金额判定，含补助后）

        public const string ES3_FIRST_DEBT = "BankruptcyMgr_HasFirstDebtTriggered";
        public const string ES3_FAIL_COUNT = "BankruptcyMgr_DebtFailureCount";
        public const string ES3_DEBT_ACTIVE = "BankruptcyMgr_DebtWarningActive";

        public struct EvaluationResult
        {
            public int sponsorshipAmount;
            public bool gameOver;
        }

        // 公开只读 API
        public bool HasFirstDebtTriggered => hasFirstDebtTriggered;
        public int DebtFailureCount => debtFailureCount;
        public bool DebtWarningActive => debtWarningActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // 必须保留在 Start，不要移到 Awake：GameStateManager.ClearAllSaves 在 Awake 阶段删除 ES3 key，
        // 依赖此函数总是晚于 ClearAllSaves 才执行（参考 RestockManager 同款约束）。
        private void Start()
        {
            LoadFromES3();
            RefreshDebugFields();
        }

        /// <summary>
        /// 在维护费扣除后调用：判定玩家状态并触发对应对话/补助/破产。
        /// 通过回调返回结果（IEnumerator 协程不能直接 return 值）。
        /// </summary>
        public IEnumerator EvaluatePostMaintenance(Action<EvaluationResult> onComplete)
        {
            var result = new EvaluationResult();
            int money = ResourceManager.Instance != null ? ResourceManager.Instance.GetMoney() : 0;
            bool inDebtNow = money < 0;

            // 还债失败：上次结算后处于负债态（debtWarningActive），这次扣完维护费仍负债
            if (debtWarningActive && inDebtNow)
            {
                debtFailureCount++;

                if (debtFailureCount >= gameOverFailureCount)
                {
                    // 真正破产 - 不发补助、不更新 debtWarningActive，由 DayLoopManager 显示破产面板
                    result.gameOver = true;
                    SaveToES3();
                    RefreshDebugFields();
                    onComplete?.Invoke(result);
                    yield break;
                }

                int sponsorship = GetSponsorshipForFailure(debtFailureCount);
                string conversation = GetFailureConversation(debtFailureCount);

                yield return PlayConversation(conversation);

                if (sponsorship > 0 && ResourceManager.Instance != null)
                    ResourceManager.Instance.AddSponsorshipMoney(sponsorship);

                result.sponsorshipAmount = sponsorship;
            }
            // 首次负债（lifetime 一次）
            else if (inDebtNow && !hasFirstDebtTriggered)
            {
                hasFirstDebtTriggered = true;
                yield return PlayConversation(firstDebtConversation);
            }

            // ⚠️ 关键：在所有处理（包含补助到账）后用最终金额更新债务标志。
            // 这样补助让钱回正后，下一天不会被误判为"还在债中"。
            debtWarningActive = ResourceManager.Instance != null
                && ResourceManager.Instance.GetMoney() < 0;

            SaveToES3();
            RefreshDebugFields();
            onComplete?.Invoke(result);
        }

        /// <summary>
        /// 播放对话：开始前检查 → pause → StartConversation → 等待结束 → resume。
        /// 不承诺 StopCoroutine 中断时一定 Resume — 这是协程的固有限制。
        /// 若未来需要更强保证，应迁移到 DialogueGameplayPauseService 的 push/pop 模式。
        /// </summary>
        private IEnumerator PlayConversation(string conversation)
        {
            if (string.IsNullOrWhiteSpace(conversation)) yield break;
            if (!DialogueManager.ConversationHasValidEntry(conversation))
            {
                Debug.LogWarning($"[BankruptcyManager] Conversation not found: {conversation}");
                yield break;
            }

            DayLoopManager.Instance?.PauseGameplayClock();
            DialogueManager.StartConversation(conversation);

            while (DialogueManager.isConversationActive)
                yield return null;

            DayLoopManager.Instance?.ResumeGameplayClock();
        }

        /// <summary>清除存档与内存状态（GameStateManager.ClearAllSaves 调用）</summary>
        public void ClearSaveAndResetState()
        {
            if (ES3.KeyExists(ES3_FIRST_DEBT)) ES3.DeleteKey(ES3_FIRST_DEBT);
            if (ES3.KeyExists(ES3_FAIL_COUNT)) ES3.DeleteKey(ES3_FAIL_COUNT);
            if (ES3.KeyExists(ES3_DEBT_ACTIVE)) ES3.DeleteKey(ES3_DEBT_ACTIVE);
            hasFirstDebtTriggered = false;
            debtFailureCount = 0;
            debtWarningActive = false;
            RefreshDebugFields();
        }

        private int GetSponsorshipForFailure(int count) => count switch
        {
            1 => firstFailureSponsorship,
            2 => secondFailureSponsorship,
            3 => thirdFailureSponsorship,
            _ => 0
        };

        private string GetFailureConversation(int count) => count switch
        {
            1 => firstFailureConversation,
            2 => secondFailureConversation,
            3 => thirdFailureConversation,
            _ => null
        };

        private void SaveToES3()
        {
            ES3.Save(ES3_FIRST_DEBT, hasFirstDebtTriggered);
            ES3.Save(ES3_FAIL_COUNT, debtFailureCount);
            ES3.Save(ES3_DEBT_ACTIVE, debtWarningActive);
        }

        private void LoadFromES3()
        {
            if (ES3.KeyExists(ES3_FIRST_DEBT))
                hasFirstDebtTriggered = ES3.Load<bool>(ES3_FIRST_DEBT);

            if (ES3.KeyExists(ES3_FAIL_COUNT))
                debtFailureCount = ES3.Load<int>(ES3_FAIL_COUNT);

            if (ES3.KeyExists(ES3_DEBT_ACTIVE))
                debtWarningActive = ES3.Load<bool>(ES3_DEBT_ACTIVE);
        }

        private void RefreshDebugFields()
        {
            dbgHasFirstDebtTriggered = hasFirstDebtTriggered;
            dbgDebtFailureCount = debtFailureCount;
            dbgDebtWarningActive = debtWarningActive;
        }
    }
}
