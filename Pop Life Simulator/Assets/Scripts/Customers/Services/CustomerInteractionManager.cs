using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Manager;
using PopLife.NarrativeSystem;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 全局顾客交互管理器
    /// 管理玩家点击检测、对话ID公共池、时间暂停控制
    /// Global customer interaction manager
    /// Manages player click detection, narrative ID pool, and time pause control
    /// </summary>
    public class CustomerInteractionManager : MonoBehaviour
    {
        public static CustomerInteractionManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private LayerMask customerInteractionLayer;  // CustomerInteractionLayer
        [SerializeField] private float triggerProbability = 0.15f;    // 触发概率 (15%)

        // 公共池：当日已使用的对话ID
        private HashSet<string> usedNarrativeIds = new HashSet<string>();

        // 当前对话状态
        private bool isDialogueActive = false;
        private CustomerInteractionState currentDialogueCustomer = null;

        // 属性
        public bool IsDialogueActive => isDialogueActive;
        public float TriggerProbability => triggerProbability;

        private void Awake()
        {
            // 单例设置
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 订阅事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
            }

            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnNarrativeCompleted += OnNarrativeCompleted;
            }
        }

        private void OnDisable()
        {
            // 取消订阅事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
            }

            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnNarrativeCompleted -= OnNarrativeCompleted;
            }
        }

        private void Start()
        {
            // 延迟订阅（确保 DayLoopManager 已初始化）
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnBuildPhaseStart;
                DayLoopManager.Instance.OnBuildPhaseStart += OnBuildPhaseStart;
            }

            if (NarrativeManager.Instance != null)
            {
                NarrativeManager.Instance.OnNarrativeCompleted -= OnNarrativeCompleted;
                NarrativeManager.Instance.OnNarrativeCompleted += OnNarrativeCompleted;
            }
        }

        private void Update()
        {
            // 检测玩家点击
            if (Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }
        }

        /// <summary>
        /// 处理鼠标点击
        /// Handle mouse click
        /// </summary>
        private void HandleMouseClick()
        {
            // 如果已有对话进行中，忽略点击
            if (isDialogueActive)
            {
                Debug.Log("[CustomerInteractionManager] 已有对话进行中，忽略点击");
                return;
            }

            // 获取鼠标世界坐标
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Raycast 检测 CustomerInteractionLayer
            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero, 0f, customerInteractionLayer);

            if (hit.collider != null)
            {
                // 找到顾客
                var customerAgent = hit.collider.GetComponentInParent<CustomerAgent>();
                if (customerAgent != null)
                {
                    var interactionState = customerAgent.GetComponent<CustomerInteractionState>();
                    if (interactionState != null && interactionState.IsInDialogueState)
                    {
                        TriggerDialogue(customerAgent, interactionState);
                    }
                }
            }
        }

        /// <summary>
        /// 触发对话
        /// Trigger dialogue
        /// </summary>
        private void TriggerDialogue(CustomerAgent customerAgent, CustomerInteractionState interactionState)
        {
            // 获取 CustomerRecord
            var record = customerAgent.GetCustomerRecord();
            if (record == null || record.availableNarrativeIds == null || record.availableNarrativeIds.Length == 0)
            {
                Debug.LogWarning($"[CustomerInteractionManager] {customerAgent.name} 没有可用的对话ID");
                return;
            }

            // 从公共池筛选对话ID
            string selectedNarrativeId = SelectNarrativeId(record.availableNarrativeIds);
            if (string.IsNullOrEmpty(selectedNarrativeId))
            {
                Debug.LogWarning($"[CustomerInteractionManager] {customerAgent.name} 无法选择对话ID");
                return;
            }

            // 通知顾客对话已触发
            interactionState.OnDialogueTriggered();

            // 记录当前对话顾客
            isDialogueActive = true;
            currentDialogueCustomer = interactionState;

            // 开始对话
            bool started = NarrativeManager.Instance.StartNarrative(selectedNarrativeId);
            if (!started)
            {
                Debug.LogError($"[CustomerInteractionManager] 无法开始对话: {selectedNarrativeId}");
                // 恢复状态
                isDialogueActive = false;
                currentDialogueCustomer = null;
                interactionState.ExitDialogueState();
            }
            else
            {
                Debug.Log($"[CustomerInteractionManager] {customerAgent.name} 开始对话: {selectedNarrativeId}");
            }
        }

        /// <summary>
        /// 从公共池筛选对话ID
        /// Select narrative ID from pool
        /// </summary>
        /// <param name="customerAvailableIds">顾客可用的对话ID列表</param>
        /// <returns>选中的对话ID</returns>
        public string SelectNarrativeId(string[] customerAvailableIds)
        {
            if (customerAvailableIds == null || customerAvailableIds.Length == 0)
                return null;

            // 排除已使用的ID
            var availableIds = customerAvailableIds.Where(id => !usedNarrativeIds.Contains(id)).ToList();

            // 如果全部被占用，允许从原始列表任意选择
            if (availableIds.Count == 0)
            {
                Debug.Log("[CustomerInteractionManager] 所有可用ID都已被使用，从原始列表选择");
                availableIds = customerAvailableIds.ToList();
            }

            // 随机选择
            int index = Random.Range(0, availableIds.Count);
            string selectedId = availableIds[index];

            // 添加到已使用池
            usedNarrativeIds.Add(selectedId);

            Debug.Log($"[CustomerInteractionManager] 选择对话ID: {selectedId}，当前已用: {usedNarrativeIds.Count}");
            return selectedId;
        }

        /// <summary>
        /// 对话完成事件处理
        /// Narrative completed event handler
        /// </summary>
        private void OnNarrativeCompleted(NarrativeSO narrative)
        {
            if (!isDialogueActive)
                return;

            // 通知顾客对话结束
            if (currentDialogueCustomer != null)
            {
                currentDialogueCustomer.OnDialogueEnded();
            }

            // 重置状态
            isDialogueActive = false;
            currentDialogueCustomer = null;

            Debug.Log($"[CustomerInteractionManager] 对话结束: {narrative.NarrativeName}");
        }

        /// <summary>
        /// 建造阶段开始事件处理（清空公共池）
        /// Build phase start event handler (clear pool)
        /// </summary>
        private void OnBuildPhaseStart()
        {
            ClearUsedNarrativeIds();
        }

        /// <summary>
        /// 清空已使用的对话ID池
        /// Clear used narrative IDs pool
        /// </summary>
        public void ClearUsedNarrativeIds()
        {
            int count = usedNarrativeIds.Count;
            usedNarrativeIds.Clear();
            Debug.Log($"[CustomerInteractionManager] 清空对话ID池，已清除 {count} 个ID");
        }

        /// <summary>
        /// 检查对话ID是否已被使用
        /// Check if narrative ID is already used
        /// </summary>
        public bool IsNarrativeIdUsed(string narrativeId)
        {
            return usedNarrativeIds.Contains(narrativeId);
        }

        /// <summary>
        /// 获取已使用的对话ID数量
        /// Get count of used narrative IDs
        /// </summary>
        public int GetUsedNarrativeIdCount()
        {
            return usedNarrativeIds.Count;
        }
    }
}
