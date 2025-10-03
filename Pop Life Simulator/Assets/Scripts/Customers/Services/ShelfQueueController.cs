using System.Collections.Generic;
using UnityEngine;
using PopLife.Runtime;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 货架队列控制器 - 管理单个货架的顾客排队
    /// 每个 ShelfInstance 附加一个此组件
    /// </summary>
    public class ShelfQueueController : MonoBehaviour
    {
        [Header("队列配置")]
        [Tooltip("交互点（顾客购买时站立的位置）")]
        public Transform interactionAnchor;

        [Tooltip("队列起点（第一个排队位置）")]
        public Transform queueAnchor;

        [Tooltip("队列方向（如果未设置会自动计算）")]
        public Vector3 queueDirection = Vector3.down;

        [Tooltip("队位间距（米）")]
        public float slotSpacing = 1.0f;

        [Tooltip("预设队位（可选，优先使用）")]
        public Transform[] queueSlots;

        [Header("等待时间配置")]
        [Tooltip("每位顾客平均服务时间（秒）")]
        public float secondsPerCustomer = 10f;

        [Header("RVO 兼容性")]
        [Tooltip("队位到达距离（允许 RVO 偏移）")]
        public float arrivalDistance = 0.8f;

        // 运行时数据
        private List<string> queuedCustomers = new();
        private Dictionary<string, Transform> assignedSlots = new();
        private Dictionary<int, Transform> cachedSlotTransforms = new();

        private ShelfInstance shelfInstance;

        void Awake()
        {
            shelfInstance = GetComponent<ShelfInstance>();
            if (shelfInstance == null)
            {
                Debug.LogError($"[ShelfQueueController] {gameObject.name} 没有 ShelfInstance 组件");
            }

            // 自动计算队列方向
            if (queueDirection == Vector3.zero && interactionAnchor != null && queueAnchor != null)
            {
                queueDirection = (queueAnchor.position - interactionAnchor.position).normalized;
            }
        }

        #region 公共接口

        /// <summary>
        /// 申请队列位置
        /// </summary>
        /// <param name="customerId">顾客ID</param>
        /// <returns>分配的队位Transform</returns>
        public Transform AcquireSlot(string customerId)
        {
            if (queuedCustomers.Contains(customerId))
            {
                Debug.LogWarning($"[ShelfQueueController] 顾客 {customerId} 已在队列中");
                return assignedSlots[customerId];
            }

            int position = queuedCustomers.Count;
            queuedCustomers.Add(customerId);

            Transform slot = GetSlotAtPosition(position);
            assignedSlots[customerId] = slot;

            Debug.Log($"[ShelfQueueController] 货架 {shelfInstance?.instanceId} " +
                     $"为顾客 {customerId} 分配队位 {position}");

            return slot;
        }

        /// <summary>
        /// 释放队列位置（顾客离开时调用）
        /// </summary>
        /// <param name="customerId">顾客ID</param>
        public void ReleaseSlot(string customerId)
        {
            if (!queuedCustomers.Contains(customerId))
            {
                Debug.LogWarning($"[ShelfQueueController] 顾客 {customerId} 不在队列中");
                return;
            }

            queuedCustomers.Remove(customerId);
            assignedSlots.Remove(customerId);

            Debug.Log($"[ShelfQueueController] 货架 {shelfInstance?.instanceId} " +
                     $"顾客 {customerId} 离开队列，队列长度: {queuedCustomers.Count}");

            // 通知后续顾客前移
            NotifyQueueAdvance();
        }

        /// <summary>
        /// 获取队列长度（供策略系统查询）
        /// </summary>
        public int GetQueueLength()
        {
            return queuedCustomers.Count;
        }

        /// <summary>
        /// 获取顾客在队列中的位置
        /// </summary>
        /// <param name="customerId">顾客ID</param>
        /// <returns>位置索引（0 = 队首），-1 表示不在队列中</returns>
        public int GetQueuePosition(string customerId)
        {
            return queuedCustomers.IndexOf(customerId);
        }

        /// <summary>
        /// 预测等待时间（供 QueuePolicy 使用）
        /// </summary>
        /// <param name="position">队列位置</param>
        /// <returns>预计等待秒数</returns>
        public int PredictWaitTime(int position)
        {
            return Mathf.RoundToInt(position * secondsPerCustomer);
        }

        /// <summary>
        /// 获取交互点（队首顾客的目标）
        /// </summary>
        public Transform GetInteractionPoint()
        {
            return interactionAnchor != null ? interactionAnchor : transform;
        }

        /// <summary>
        /// 检查顾客是否在队列中
        /// </summary>
        public bool IsInQueue(string customerId)
        {
            return queuedCustomers.Contains(customerId);
        }

        #endregion

        #region 内部实现

        /// <summary>
        /// 获取指定位置的队位 Transform
        /// </summary>
        private Transform GetSlotAtPosition(int position)
        {
            // 位置 0 = 队首，使用交互点
            if (position == 0)
            {
                return GetInteractionPoint();
            }

            // 优先使用预设队位
            if (queueSlots != null && position <= queueSlots.Length)
            {
                return queueSlots[position - 1]; // position 1 对应 queueSlots[0]
            }

            // 动态计算队位（使用缓存优化）
            if (!cachedSlotTransforms.ContainsKey(position))
            {
                Vector3 slotPosition = CalculateSlotPosition(position);
                Transform slotTransform = CreateSlotTransform(position, slotPosition);
                cachedSlotTransforms[position] = slotTransform;
            }

            return cachedSlotTransforms[position];
        }

        /// <summary>
        /// 计算队位的世界坐标
        /// </summary>
        private Vector3 CalculateSlotPosition(int position)
        {
            if (queueAnchor == null)
            {
                // 降级：基于交互点向后计算
                Vector3 basePos = interactionAnchor != null ? interactionAnchor.position : transform.position;
                return basePos + queueDirection * (position * slotSpacing);
            }

            // 标准计算：从队列起点开始
            return queueAnchor.position + queueDirection * ((position - 1) * slotSpacing);
        }

        /// <summary>
        /// 创建虚拟队位 Transform
        /// </summary>
        private Transform CreateSlotTransform(int position, Vector3 worldPosition)
        {
            GameObject slotObj = new GameObject($"QueueSlot_{position}");
            slotObj.transform.position = worldPosition;
            slotObj.transform.parent = transform; // 父对象设为货架
            slotObj.transform.rotation = Quaternion.identity;

            // 可选：添加 Gizmo 帮助调试
            #if UNITY_EDITOR
            slotObj.AddComponent<QueueSlotGizmo>();
            #endif

            return slotObj.transform;
        }

        /// <summary>
        /// 通知所有在队列中的顾客前移
        /// </summary>
        private void NotifyQueueAdvance()
        {
            // 重新分配所有队位
            for (int i = 0; i < queuedCustomers.Count; i++)
            {
                string customerId = queuedCustomers[i];
                Transform newSlot = GetSlotAtPosition(i);
                assignedSlots[customerId] = newSlot;

                // 查找顾客 GameObject 并更新目标
                GameObject customerObj = GameObject.Find(customerId);
                if (customerObj != null)
                {
                    var adapter = customerObj.GetComponent<PopLife.Customers.Runtime.CustomerBlackboardAdapter>();
                    if (adapter != null)
                    {
                        // 更新黑板中的队位引用
                        adapter.assignedQueueSlot = newSlot;

                        // 触发寻路更新
                        var destinationSetter = customerObj.GetComponent<Pathfinding.AIDestinationSetter>();
                        if (destinationSetter != null && destinationSetter.target == assignedSlots[customerId])
                        {
                            destinationSetter.target = newSlot;
                            Debug.Log($"[ShelfQueueController] 顾客 {customerId} 队位更新到位置 {i}");
                        }
                    }
                }
            }
        }

        #endregion

        #region 调试工具

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // 绘制交互点
            if (interactionAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(interactionAnchor.position, 0.3f);
                UnityEditor.Handles.Label(interactionAnchor.position + Vector3.up * 0.5f, "交互点");
            }

            // 绘制队列起点
            if (queueAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(queueAnchor.position, 0.2f);
                UnityEditor.Handles.Label(queueAnchor.position + Vector3.up * 0.5f, "队列起点");
            }

            // 绘制队列方向
            if (queueAnchor != null && queueDirection != Vector3.zero)
            {
                Gizmos.color = Color.cyan;
                Vector3 start = queueAnchor.position;
                Vector3 end = start + queueDirection * slotSpacing * 5;
                Gizmos.DrawLine(start, end);
                DrawCone(end, queueDirection, 0.3f);
            }

            // 绘制预设队位
            if (queueSlots != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < queueSlots.Length; i++)
                {
                    if (queueSlots[i] != null)
                    {
                        Gizmos.DrawWireCube(queueSlots[i].position, Vector3.one * 0.4f);
                        UnityEditor.Handles.Label(queueSlots[i].position + Vector3.up * 0.3f, $"队位 {i + 1}");
                    }
                }
            }

            // 运行时绘制当前队列
            if (Application.isPlaying && queuedCustomers.Count > 0)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < queuedCustomers.Count; i++)
                {
                    if (assignedSlots.TryGetValue(queuedCustomers[i], out Transform slot) && slot != null)
                    {
                        Gizmos.DrawWireSphere(slot.position, arrivalDistance);
                        UnityEditor.Handles.Label(slot.position, $"{queuedCustomers[i]}\n位置:{i}");
                    }
                }
            }
        }

        // 辅助方法：绘制圆锥
        private static void DrawCone(Vector3 position, Vector3 direction, float size)
        {
            Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized * size;
            Vector3 tip = position;
            Vector3 base1 = position - direction * size + right;
            Vector3 base2 = position - direction * size - right;

            Gizmos.DrawLine(tip, base1);
            Gizmos.DrawLine(tip, base2);
            Gizmos.DrawLine(base1, base2);
        }
#endif

        #endregion
    }

#if UNITY_EDITOR
    /// <summary>
    /// 队列位置 Gizmo 辅助组件
    /// </summary>
    public class QueueSlotGizmo : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
        }
    }
#endif
}
