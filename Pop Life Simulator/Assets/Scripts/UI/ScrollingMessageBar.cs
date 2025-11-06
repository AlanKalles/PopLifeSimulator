using System.Collections.Generic;
using UnityEngine;
using TMPro;
using PopLife.Customers.Services;
using PopLife.Customers.Runtime;

namespace PopLife.UI
{
    /// <summary>
    /// 滚动播报条 - 从右向左滚动显示消息队列
    /// 自动监听顾客结账事件并播报
    /// 支持随机插入新闻文本
    /// </summary>
    public class ScrollingMessageBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform messageContainer; // 移动的文字容器
        [SerializeField] private TextMeshProUGUI messageText;    // TMP文字组件
        [SerializeField] private RectTransform maskRect;         // 遮罩区域（用于计算边界）

        [Header("Scroll Settings")]
        [Tooltip("滚动速度（像素/秒）")]
        [SerializeField] private float scrollSpeed = 50f;

        [Tooltip("消息间距（像素）")]
        [SerializeField] private float messageSpacing = 100f;

        [Tooltip("最大队列容量")]
        [SerializeField] private int maxQueueSize = 10;

        [Header("News Settings")]
        [Tooltip("新闻文本库（可选）- 未设置则不播放新闻")]
        [SerializeField] private NewsLibrary newsLibrary;

        [Tooltip("启用自动新闻插入")]
        [SerializeField] private bool enableRandomNews = true;

        [Header("Debug Info")]
        [SerializeField] private int currentQueueCount = 0; // 当前队列消息数（仅显示）
        [SerializeField] private float nextNewsTime = 0f;   // 下次插入新闻的时间（仅显示）

        // 消息队列
        private Queue<string> messageQueue = new Queue<string>();

        // 滚动状态
        private bool isScrolling = false;
        private float messageWidth = 0f;        // 当前消息宽度
        private float maskRightEdge = 0f;       // 遮罩右边缘X坐标
        private float maskLeftEdge = 0f;        // 遮罩左边缘X坐标

        // 新闻插入定时器
        private float newsTimer = 0f;           // 当前计时器
        private float currentNewsInterval = 0f; // 当前随机间隔

        void Awake()
        {
            // 验证必需的引用
            if (!ValidateReferences())
            {
                enabled = false; // 禁用组件避免运行时错误
                return;
            }

            // 计算遮罩边界
            if (maskRect != null)
            {
                maskRightEdge = maskRect.rect.width / 2f;
                maskLeftEdge = -maskRect.rect.width / 2f;
            }

            // 初始化新闻定时器
            ResetNewsTimer();
        }

        void Start()
        {
            // 验证新闻库配置
            if (enableRandomNews && newsLibrary == null)
            {
                Debug.LogWarning("[ScrollingMessageBar] Random news enabled but NewsLibrary not assigned. Disabling random news.");
                enableRandomNews = false;
            }
        }

        /// <summary>
        /// 验证必需的引用是否已设置
        /// </summary>
        private bool ValidateReferences()
        {
            bool isValid = true;

            if (messageContainer == null)
            {
                Debug.LogError("[ScrollingMessageBar] messageContainer 未设置！请在 Inspector 中分配引用。", this);
                isValid = false;
            }

            if (messageText == null)
            {
                Debug.LogError("[ScrollingMessageBar] messageText 未设置！请在 Inspector 中分配 TextMeshProUGUI 组件。", this);
                isValid = false;
            }

            if (maskRect == null)
            {
                Debug.LogWarning("[ScrollingMessageBar] maskRect 未设置，将无法正确计算边界。", this);
                // 不标记为无效，因为可以使用默认值
            }

            return isValid;
        }

        void OnEnable()
        {
            // 监听顾客结账事件
            CustomerEventBus.OnCheckedOut += HandleCustomerCheckout;
        }

        void OnDisable()
        {
            // 取消监听
            CustomerEventBus.OnCheckedOut -= HandleCustomerCheckout;
        }

        void Update()
        {
            // 滚动逻辑
            if (isScrolling && messageContainer != null && messageText != null)
            {
                // 从右向左移动
                messageContainer.localPosition += Vector3.left * scrollSpeed * Time.deltaTime;

                // 检查是否完全移出左侧
                float rightEdge = messageContainer.localPosition.x + messageWidth;
                if (rightEdge < maskLeftEdge)
                {
                    // 当前消息播放完毕
                    isScrolling = false;

                    // 显示下一条消息
                    if (messageQueue.Count > 0)
                    {
                        DisplayNextMessage();
                    }
                    else
                    {
                        // 队列为空，隐藏文字
                        messageText.text = "";
                    }
                }
            }

            // 新闻插入定时器
            if (enableRandomNews && newsLibrary != null)
            {
                newsTimer += Time.deltaTime;

                if (newsTimer >= currentNewsInterval)
                {
                    // 时间到，插入随机新闻
                    InsertRandomNews();
                    // 重置定时器
                    ResetNewsTimer();
                }

                // 更新调试显示
                nextNewsTime = currentNewsInterval - newsTimer;
            }
        }

        /// <summary>
        /// 处理顾客结账事件 - 自动播报
        /// </summary>
        private void HandleCustomerCheckout(CustomerAgent agent)
        {
            if (agent == null || agent.currentSession == null)
                return;

            // 获取顾客名称（从TMP组件）
            var nameText = agent.GetComponentInChildren<TextMeshPro>();
            string customerName = nameText != null ? nameText.text : "Unknown";

            // 获取消费金额
            int moneySpent = agent.currentSession.moneySpent;

            // 格式化消息
            string message = $"{customerName} spent ${moneySpent} in store today";

            // 添加到队列
            EnqueueMessage(message);
        }

        /// <summary>
        /// 添加消息到队列
        /// </summary>
        public void EnqueueMessage(string message)
        {
            // 检查队列容量
            if (messageQueue.Count >= maxQueueSize)
            {
                // 移除最旧的消息
                messageQueue.Dequeue();
                Debug.LogWarning($"[ScrollingMessageBar] Message queue full, oldest message removed");
            }

            // 添加新消息
            messageQueue.Enqueue(message);
            currentQueueCount = messageQueue.Count;

            // 如果当前没有在滚动，立即显示
            if (!isScrolling)
            {
                DisplayNextMessage();
            }
        }

        /// <summary>
        /// 显示下一条消息
        /// </summary>
        private void DisplayNextMessage()
        {
            if (messageQueue.Count == 0)
            {
                isScrolling = false;
                return;
            }

            // 验证必需组件
            if (messageText == null || messageContainer == null)
            {
                Debug.LogError("[ScrollingMessageBar] 无法显示消息：messageText 或 messageContainer 为空！");
                isScrolling = false;
                return;
            }

            // 出队并显示
            string message = messageQueue.Dequeue();
            currentQueueCount = messageQueue.Count;
            messageText.text = message;

            // 强制更新文字布局以获取正确宽度
            messageText.ForceMeshUpdate();
            messageWidth = messageText.preferredWidth;

            // 设置起始位置：从遮罩右边缘外侧开始
            float startX = maskRightEdge + messageSpacing;
            messageContainer.localPosition = new Vector3(startX, 0, 0);

            // 开始滚动
            isScrolling = true;

            Debug.Log($"[ScrollingMessageBar] Displaying message: {message} (width: {messageWidth}px)");
        }

        /// <summary>
        /// 清空消息队列
        /// </summary>
        public void ClearQueue()
        {
            messageQueue.Clear();
            currentQueueCount = 0;
            isScrolling = false;

            if (messageText != null)
            {
                messageText.text = "";
            }

            Debug.Log("[ScrollingMessageBar] Message queue cleared");
        }

        /// <summary>
        /// 测试方法 - 手动添加测试消息
        /// </summary>
        [ContextMenu("Test - Add Sample Messages")]
        private void TestAddSampleMessages()
        {
            EnqueueMessage("Alice spent $150 in store today");
            EnqueueMessage("Bob spent $89 in store today");
            EnqueueMessage("Charlie spent $234 in store today");
            Debug.Log("[ScrollingMessageBar] Added 3 test messages");
        }

        /// <summary>
        /// 测试方法 - 清空队列
        /// </summary>
        [ContextMenu("Test - Clear Queue")]
        private void TestClearQueue()
        {
            ClearQueue();
        }

        /// <summary>
        /// 插入随机新闻到队列
        /// </summary>
        private void InsertRandomNews()
        {
            if (newsLibrary == null)
            {
                Debug.LogWarning("[ScrollingMessageBar] Cannot insert news: NewsLibrary is null");
                return;
            }

            string randomNews = newsLibrary.GetRandomNews();

            if (string.IsNullOrEmpty(randomNews))
            {
                Debug.LogWarning("[ScrollingMessageBar] No news available in library");
                return;
            }

            // 添加到队列
            EnqueueMessage(randomNews);
            Debug.Log($"[ScrollingMessageBar] Random news inserted: {randomNews}");
        }

        /// <summary>
        /// 重置新闻定时器（获取新的随机间隔）
        /// </summary>
        private void ResetNewsTimer()
        {
            newsTimer = 0f;

            if (newsLibrary != null)
            {
                currentNewsInterval = newsLibrary.GetRandomInterval();
            }
            else
            {
                currentNewsInterval = 10f; // 默认间隔
            }

            nextNewsTime = currentNewsInterval;
            Debug.Log($"[ScrollingMessageBar] Next news will be inserted in {currentNewsInterval:F1} seconds");
        }

        /// <summary>
        /// 手动插入新闻（供外部调用）
        /// </summary>
        public void InsertNewsManually(string newsText)
        {
            if (string.IsNullOrWhiteSpace(newsText))
            {
                Debug.LogWarning("[ScrollingMessageBar] Cannot insert empty news");
                return;
            }

            EnqueueMessage(newsText);
            Debug.Log($"[ScrollingMessageBar] Manual news inserted: {newsText}");
        }

        /// <summary>
        /// 启用/禁用随机新闻
        /// </summary>
        public void SetRandomNewsEnabled(bool enabled)
        {
            enableRandomNews = enabled;

            if (enabled)
            {
                ResetNewsTimer();
                Debug.Log("[ScrollingMessageBar] Random news enabled");
            }
            else
            {
                Debug.Log("[ScrollingMessageBar] Random news disabled");
            }
        }

        /// <summary>
        /// 设置新闻库（运行时）
        /// </summary>
        public void SetNewsLibrary(NewsLibrary library)
        {
            newsLibrary = library;

            if (library != null && enableRandomNews)
            {
                ResetNewsTimer();
                Debug.Log($"[ScrollingMessageBar] News library set: {library.name}");
            }
        }

        /// <summary>
        /// 测试方法 - 手动插入一条随机新闻
        /// </summary>
        [ContextMenu("Test - Insert Random News Now")]
        private void TestInsertRandomNews()
        {
            InsertRandomNews();
        }

        /// <summary>
        /// 测试方法 - 切换随机新闻开关
        /// </summary>
        [ContextMenu("Test - Toggle Random News")]
        private void TestToggleRandomNews()
        {
            SetRandomNewsEnabled(!enableRandomNews);
        }
    }
}
