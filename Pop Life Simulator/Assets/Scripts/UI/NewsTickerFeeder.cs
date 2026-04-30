using UnityEngine;
using PopLife.DialogueBridge;

namespace PopLife.UI
{
    /// <summary>
    /// 新闻播报数据源 - 定时从 NewsLibrary 中随机选取新闻喂入 ScrollingMessageBar
    /// 仅在营业阶段（OpenPhase）工作
    /// </summary>
    public class NewsTickerFeeder : MonoBehaviour
    {
        [SerializeField] private ScrollingMessageBar ticker;
        [SerializeField] private NewsLibrary newsLibrary;
        [SerializeField] private bool enableRandomNews = true;

        // 定时器
        private float newsTimer = 0f;
        private float currentInterval = 10f;

        void Start()
        {
            if (ticker == null)
                ticker = GetComponent<ScrollingMessageBar>();

            if (enableRandomNews && newsLibrary == null)
            {
                Debug.LogWarning("[NewsTickerFeeder] NewsLibrary 未设置，禁用随机新闻。");
                enableRandomNews = false;
            }

            ResetTimer();
        }

        void OnEnable()
        {
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnStoreOpen += HandleStoreOpen;
        }

        void OnDisable()
        {
            if (DayLoopManager.Instance != null)
                DayLoopManager.Instance.OnStoreOpen -= HandleStoreOpen;
        }

        void Update()
        {
            if (!enableRandomNews || newsLibrary == null || ticker == null)
                return;

            if (DialogueGameplayPauseService.IsGameplayPaused)
                return;

            // 仅营业阶段
            if (DayLoopManager.Instance != null && DayLoopManager.Instance.currentPhase != GamePhase.OpenPhase)
                return;

            newsTimer += Time.deltaTime;

            if (newsTimer >= currentInterval)
            {
                InsertRandomNews();
                ResetTimer();
            }
        }

        private void InsertRandomNews()
        {
            string news = newsLibrary.GetRandomNews();
            if (!string.IsNullOrEmpty(news))
                ticker.EnqueueMessage(news);
        }

        private void ResetTimer()
        {
            newsTimer = 0f;
            if (newsLibrary != null)
                currentInterval = newsLibrary.GetRandomInterval();
        }

        private void HandleStoreOpen()
        {
            if (enableRandomNews && newsLibrary != null)
                ResetTimer();
        }

        /// <summary>
        /// 手动插入新闻（供外部调用）
        /// </summary>
        public void InsertNewsManually(string newsText)
        {
            if (!string.IsNullOrWhiteSpace(newsText) && ticker != null)
                ticker.EnqueueMessage(newsText);
        }

        /// <summary>
        /// 启用/禁用随机新闻
        /// </summary>
        public void SetRandomNewsEnabled(bool enabled)
        {
            enableRandomNews = enabled;
            if (enabled) ResetTimer();
        }

        /// <summary>
        /// 运行时更换新闻库
        /// </summary>
        public void SetNewsLibrary(NewsLibrary library)
        {
            newsLibrary = library;
            if (library != null && enableRandomNews)
                ResetTimer();
        }
    }
}
