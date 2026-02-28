using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace PopLife.UI
{
    /// <summary>
    /// 通用滚动播报条引擎 - 从右向左滚动显示消息队列
    /// 不绑定任何数据源，由外部 Feeder 组件喂入消息
    /// 仅在营业阶段（OpenPhase）滚动，建造阶段（BuildPhase）清空队列
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

        [Header("Auto Hide")]
        [Tooltip("队列为空时自动隐藏播报条")]
        [SerializeField] private bool autoHide = false;

        [Tooltip("用于控制显隐的 CanvasGroup（autoHide 开启时必须设置）")]
        [SerializeField] private CanvasGroup barCanvasGroup;

        [Header("Debug Info")]
        [SerializeField] private int currentQueueCount = 0;

        // 消息队列
        private Queue<string> messageQueue = new Queue<string>();

        // 滚动状态
        private bool isScrolling = false;
        private float messageWidth = 0f;
        private float maskRightEdge = 0f;
        private float maskLeftEdge = 0f;

        void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            if (maskRect != null)
            {
                maskRightEdge = maskRect.rect.width / 2f;
                maskLeftEdge = -maskRect.rect.width / 2f;
            }

            // 初始隐藏（如果启用了 autoHide）
            if (autoHide)
                SetBarVisible(false);
        }

        void OnEnable()
        {
            // 监听游戏阶段切换事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += HandleBuildPhaseStart;
            }
        }

        void OnDisable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= HandleBuildPhaseStart;
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = true;

            if (messageContainer == null)
            {
                Debug.LogError("[ScrollingMessageBar] messageContainer 未设置！", this);
                isValid = false;
            }

            if (messageText == null)
            {
                Debug.LogError("[ScrollingMessageBar] messageText 未设置！", this);
                isValid = false;
            }

            if (maskRect == null)
            {
                Debug.LogWarning("[ScrollingMessageBar] maskRect 未设置，将无法正确计算边界。", this);
            }

            return isValid;
        }

        void Update()
        {
            // 仅营业阶段滚动
            if (DayLoopManager.Instance != null && DayLoopManager.Instance.currentPhase != GamePhase.OpenPhase)
                return;

            if (!isScrolling || messageContainer == null || messageText == null)
                return;

            // 从右向左移动
            messageContainer.localPosition += Vector3.left * scrollSpeed * Time.deltaTime;

            // 检查是否完全移出左侧
            float rightEdge = messageContainer.localPosition.x + messageWidth;
            if (rightEdge < maskLeftEdge)
            {
                isScrolling = false;

                if (messageQueue.Count > 0)
                {
                    DisplayNextMessage();
                }
                else
                {
                    messageText.text = "";
                    if (autoHide)
                        SetBarVisible(false);
                }
            }
        }

        /// <summary>
        /// 添加消息到队列
        /// </summary>
        public void EnqueueMessage(string message)
        {
            if (messageQueue.Count >= maxQueueSize)
            {
                messageQueue.Dequeue();
            }

            messageQueue.Enqueue(message);
            currentQueueCount = messageQueue.Count;

            // 有新消息进来，确保播报条可见
            if (autoHide)
                SetBarVisible(true);

            if (!isScrolling)
                DisplayNextMessage();
        }

        private void DisplayNextMessage()
        {
            if (messageQueue.Count == 0)
            {
                isScrolling = false;
                return;
            }

            if (messageText == null || messageContainer == null)
            {
                isScrolling = false;
                return;
            }

            string message = messageQueue.Dequeue();
            currentQueueCount = messageQueue.Count;
            messageText.text = message;

            messageText.ForceMeshUpdate();
            messageWidth = messageText.preferredWidth;

            float startX = maskRightEdge + messageSpacing;
            messageContainer.localPosition = new Vector3(startX, 0, 0);

            isScrolling = true;
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
                messageText.text = "";

            if (autoHide)
                SetBarVisible(false);
        }

        /// <summary>
        /// 控制播报条显隐（通过 CanvasGroup alpha）
        /// </summary>
        private void SetBarVisible(bool visible)
        {
            if (barCanvasGroup == null) return;
            barCanvasGroup.alpha = visible ? 1f : 0f;
            barCanvasGroup.blocksRaycasts = visible;
        }

        private void HandleBuildPhaseStart()
        {
            ClearQueue();
        }

        [ContextMenu("Test - Add Sample Messages")]
        private void TestAddSampleMessages()
        {
            EnqueueMessage("Alice spent $150 in store today");
            EnqueueMessage("Bob picked Luxury Vibrator");
            EnqueueMessage("Charlie entered the store");
        }

        [ContextMenu("Test - Clear Queue")]
        private void TestClearQueue()
        {
            ClearQueue();
        }
    }
}
