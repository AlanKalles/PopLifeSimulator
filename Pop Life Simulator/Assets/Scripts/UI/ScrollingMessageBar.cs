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

        [Header("Debug Info")]
        [SerializeField] private int currentQueueCount = 0; // 当前队列消息数（仅显示）

        // 消息队列
        private Queue<string> messageQueue = new Queue<string>();

        // 滚动状态
        private bool isScrolling = false;
        private float messageWidth = 0f;        // 当前消息宽度
        private float maskRightEdge = 0f;       // 遮罩右边缘X坐标
        private float maskLeftEdge = 0f;        // 遮罩左边缘X坐标

        void Awake()
        {
            // 计算遮罩边界
            if (maskRect != null)
            {
                maskRightEdge = maskRect.rect.width / 2f;
                maskLeftEdge = -maskRect.rect.width / 2f;
            }
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
            if (isScrolling)
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
            messageText.text = "";
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
    }
}
