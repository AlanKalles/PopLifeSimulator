using UnityEngine;
using System.Collections.Generic;

namespace PopLife.UI
{
    /// <summary>
    /// 新闻文本库 - 存储随机播报的新闻内容
    /// 在Inspector中可以手动添加/编辑新闻文本
    /// </summary>
    [CreateAssetMenu(fileName = "NewsLibrary", menuName = "PopLife/UI/News Library", order = 100)]
    public class NewsLibrary : ScriptableObject
    {
        [Header("News Settings")]
        [Tooltip("所有新闻文本列表（支持多行文本）")]
        [SerializeField] private List<string> newsTexts = new List<string>();

        [Header("Timing Settings")]
        [Tooltip("最小插入间隔（秒）- 至少等待多久才插入下一条新闻")]
        [SerializeField] private float minInsertInterval = 5f;

        [Tooltip("最大插入间隔（秒）- 最多等待多久就插入下一条新闻")]
        [SerializeField] private float maxInsertInterval = 15f;

        /// <summary>
        /// 获取所有新闻文本（只读）
        /// </summary>
        public IReadOnlyList<string> NewsTexts => newsTexts;

        /// <summary>
        /// 最小插入间隔（秒）
        /// </summary>
        public float MinInsertInterval => minInsertInterval;

        /// <summary>
        /// 最大插入间隔（秒）
        /// </summary>
        public float MaxInsertInterval => maxInsertInterval;

        /// <summary>
        /// 获取随机新闻文本
        /// </summary>
        public string GetRandomNews()
        {
            if (newsTexts == null || newsTexts.Count == 0)
            {
                Debug.LogWarning("[NewsLibrary] No news texts available!");
                return string.Empty;
            }

            int randomIndex = Random.Range(0, newsTexts.Count);
            return newsTexts[randomIndex];
        }

        /// <summary>
        /// 获取随机插入间隔时间（秒）
        /// </summary>
        public float GetRandomInterval()
        {
            return Random.Range(minInsertInterval, maxInsertInterval);
        }

        /// <summary>
        /// 添加新闻文本（运行时）
        /// </summary>
        public void AddNews(string newsText)
        {
            if (string.IsNullOrWhiteSpace(newsText))
            {
                Debug.LogWarning("[NewsLibrary] Cannot add empty news text");
                return;
            }

            newsTexts.Add(newsText);
            Debug.Log($"[NewsLibrary] Added news: {newsText}");
        }

        /// <summary>
        /// 移除指定索引的新闻文本（运行时）
        /// </summary>
        public void RemoveNewsAt(int index)
        {
            if (index < 0 || index >= newsTexts.Count)
            {
                Debug.LogWarning($"[NewsLibrary] Invalid index: {index}");
                return;
            }

            string removedNews = newsTexts[index];
            newsTexts.RemoveAt(index);
            Debug.Log($"[NewsLibrary] Removed news: {removedNews}");
        }

        /// <summary>
        /// 清空所有新闻（运行时）
        /// </summary>
        public void ClearAllNews()
        {
            newsTexts.Clear();
            Debug.Log("[NewsLibrary] All news cleared");
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        void OnValidate()
        {
            // 确保间隔时间合理
            if (minInsertInterval < 1f)
            {
                minInsertInterval = 1f;
                Debug.LogWarning("[NewsLibrary] minInsertInterval cannot be less than 1 second");
            }

            if (maxInsertInterval < minInsertInterval)
            {
                maxInsertInterval = minInsertInterval + 5f;
                Debug.LogWarning("[NewsLibrary] maxInsertInterval adjusted to be greater than minInsertInterval");
            }

            // 移除空白新闻
            newsTexts.RemoveAll(string.IsNullOrWhiteSpace);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 快速添加示例新闻（仅编辑器）
        /// </summary>
        [ContextMenu("Add Sample News (English)")]
        private void AddSampleNewsEnglish()
        {
            newsTexts.Add("Breaking News: Local adult store reports record sales this month");
            newsTexts.Add("City Council approves new regulations for retail businesses");
            newsTexts.Add("Economy Update: Consumer spending reaches all-time high");
            newsTexts.Add("Weather Alert: Clear skies expected for the weekend shopping rush");
            newsTexts.Add("New Study: Customer satisfaction in retail sector improves by 15%");
            newsTexts.Add("Tech News: Innovative POS systems revolutionize retail checkout");
            newsTexts.Add("Market Trend: Online shopping grows but in-store experience remains crucial");
            newsTexts.Add("Health Notice: Remember to maintain social distancing while shopping");
            Debug.Log("[NewsLibrary] Added 8 sample English news items");
        }

        /// <summary>
        /// 清空所有新闻（仅编辑器）
        /// </summary>
        [ContextMenu("Clear All News")]
        private void ClearAllNewsEditor()
        {
            ClearAllNews();
        }
#endif
    }
}
