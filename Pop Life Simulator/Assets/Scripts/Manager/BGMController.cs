using UnityEngine;
using PopLife.Data;

namespace PopLife
{
    /// <summary>
    /// BGM控制器 - 根据游戏时间自动切换白天/夜晚BGM
    /// </summary>
    public class BGMController : MonoBehaviour
    {
        public static BGMController Instance;

        [Header("BGM Keys")]
        [SerializeField] private string dayBGMKey = AudioKeys.BGM_SHOP;
        [SerializeField] private string nightBGMKey = AudioKeys.BGM_NIGHT;

        [Header("Crossfade Duration")]
        [Tooltip("正常过渡时长（白天→夜晚）")]
        [SerializeField] private float normalCrossfadeDuration = 2f;
        [Tooltip("快速过渡时长（结算确认后 夜晚→白天）")]
        [SerializeField] private float quickCrossfadeDuration = 0.5f;

        private bool isNightBGM = false;  // 当前是否播放夜晚BGM
        private bool hasTransitionedToNight = false; // 当天是否已切换到夜晚BGM（防止重复触发）

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 订阅DayLoopManager事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen += OnStoreOpen;
            }

            // 初始播放白天BGM（游戏从建造阶段开始）
            PlayDayBGM(instant: true);
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
                DayLoopManager.Instance.OnStoreOpen -= OnStoreOpen;
            }
        }

        private void Update()
        {
            // 仅在营业阶段监听时间
            if (DayLoopManager.Instance == null) return;
            if (DayLoopManager.Instance.currentPhase != GamePhase.OpenPhase) return;
            if (!DayLoopManager.Instance.isStoreOpen) return;

            float currentHour = DayLoopManager.Instance.currentHour;
            float nightStartHour = DayLoopManager.Instance.NightBGMStartHour;

            // 检查是否需要切换到夜晚BGM
            if (currentHour >= nightStartHour && !hasTransitionedToNight)
            {
                TransitionToNightBGM();
            }
        }

        /// <summary>
        /// 切换到夜晚BGM（正常过渡）
        /// </summary>
        private void TransitionToNightBGM()
        {
            if (isNightBGM) return;

            isNightBGM = true;
            hasTransitionedToNight = true;
            AudioManager.Instance?.CrossfadeToMusic(nightBGMKey, normalCrossfadeDuration);
            Debug.Log($"[BGMController] 切换到夜晚BGM，过渡时长: {normalCrossfadeDuration}s");
        }

        /// <summary>
        /// 切换到白天BGM（供DailySettlementPanel调用）
        /// </summary>
        public void TransitionToDayBGM()
        {
            isNightBGM = false;
            hasTransitionedToNight = false;
            AudioManager.Instance?.CrossfadeToMusic(dayBGMKey, quickCrossfadeDuration);
            Debug.Log($"[BGMController] 切换到白天BGM，过渡时长: {quickCrossfadeDuration}s");
        }

        /// <summary>
        /// 播放白天BGM
        /// </summary>
        /// <param name="instant">是否立即播放（无过渡）</param>
        private void PlayDayBGM(bool instant = false)
        {
            isNightBGM = false;
            hasTransitionedToNight = false;

            if (instant)
            {
                AudioManager.Instance?.PlayMusic(dayBGMKey);
            }
            else
            {
                AudioManager.Instance?.CrossfadeToMusic(dayBGMKey, normalCrossfadeDuration);
            }
        }

        /// <summary>
        /// 建造阶段开始事件处理
        /// </summary>
        private void OnBuildPhaseStart()
        {
            // 建造阶段开始时，BGM切换由结算确认按钮触发（TransitionToDayBGM）
            // 这里只重置状态标志
            hasTransitionedToNight = false;
        }

        /// <summary>
        /// 开店事件处理
        /// </summary>
        private void OnStoreOpen()
        {
            // 开店时（12:00），确保是白天BGM
            // 正常情况下此时已经是白天BGM，这里做保险处理
            if (isNightBGM)
            {
                PlayDayBGM(instant: true);
            }

            // 重置夜晚切换标志
            hasTransitionedToNight = false;
        }
    }
}
