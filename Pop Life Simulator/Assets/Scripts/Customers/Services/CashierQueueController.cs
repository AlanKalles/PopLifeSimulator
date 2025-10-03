using System.Collections.Generic;
using UnityEngine;
using PopLife.Runtime;

namespace PopLife.Customers.Services
{
    /// <summary>
    /// 收银台队列控制器 - 管理单个收银台的顾客排队
    /// 每个收银台 FacilityInstance 附加一个此组件
    /// </summary>
    public class CashierQueueController : MonoBehaviour
    {
        [Header("队列配置")]
        [Tooltip("交互点（顾客结账时站立的位置）")]
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
        [Tooltip("每位顾客平均结账时间（秒）")]
        public float secondsPerCustomer = 15f;

        [Header("RVO 兼容性")]
        [Tooltip("队位到达距离（允许 RVO 偏移）")]
        public float arrivalDistance = 0.8f;

        // 运行时数据
        private List<string> queuedCustomers = new();
        private Dictionary<string, Transform> assignedSlots = new();
        private Dictionary<int, Transform> cachedSlotTransforms = new();

        private FacilityInstance facilityInstance;

        void Awake()
        {
            facilityInstance = GetComponent<FacilityInstance>();
            if (facilityInstance == null)
            {
                Debug.LogError($"[CashierQueueController] {gameObject.name} 没有 FacilityInstance 组件");
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
        public Transform AcquireSlot(string customerId)
        {
            if (queuedCustomers.Contains(customerId))
            {
                Debug.LogWarning($"[CashierQueueController] 顾客 {customerId} 已在队列中");
                return assignedSlots[customerId];
            }

            int position = queuedCustomers.Count;
            queuedCustomers.Add(customerId);

            Transform slot = GetSlotAtPosition(position);
            assignedSlots[customerId] = slot;

            Debug.Log($"[CashierQueueController] 收银台 {facilityInstance?.instanceId} " +
                     $"为顾客 {customerId} 分配队位 {position}");

            return slot;
        }

        /// <summary>
        /// 释放队列位置
        /// </summary>
        public void ReleaseSlot(string customerId)
        {
            if (!queuedCustomers.Contains(customerId))
            {
                Debug.LogWarning($"[CashierQueueController] 顾客 {customerId} 不在队列中");
                return;
            }

            queuedCustomers.Remove(customerId);
            assignedSlots.Remove(customerId);

            Debug.Log($"[CashierQueueController] 收银台 {facilityInstance?.instanceId} " +
                     $"顾客 {customerId} 离开队列，队列长度: {queuedCustomers.Count}");

            // 通知后续顾客前移
            NotifyQueueAdvance();
        }

        /// <summary>
        /// 获取队列长度
        /// </summary>
        public int GetQueueLength()
        {
            return queuedCustomers.Count;
        }

        /// <summary>
        /// 获取顾客在队列中的位置
        /// </summary>
        public int GetQueuePosition(string customerId)
        {
            return queuedCustomers.IndexOf(customerId);
        }

        /// <summary>
        /// 预测等待时间
        /// </summary>
        public int PredictWaitTime(int position)
        {
            return Mathf.RoundToInt(position * secondsPerCustomer);
        }

        /// <summary>
        /// 获取交互点
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

        private Transform GetSlotAtPosition(int position)
        {
            if (position == 0)
            {
                return GetInteractionPoint();
            }

            if (queueSlots != null && position <= queueSlots.Length)
            {
                return queueSlots[position - 1];
            }

            if (!cachedSlotTransforms.ContainsKey(position))
            {
                Vector3 slotPosition = CalculateSlotPosition(position);
                Transform slotTransform = CreateSlotTransform(position, slotPosition);
                cachedSlotTransforms[position] = slotTransform;
            }

            return cachedSlotTransforms[position];
        }

        private Vector3 CalculateSlotPosition(int position)
        {
            if (queueAnchor == null)
            {
                Vector3 basePos = interactionAnchor != null ? interactionAnchor.position : transform.position;
                return basePos + queueDirection * (position * slotSpacing);
            }

            return queueAnchor.position + queueDirection * ((position - 1) * slotSpacing);
        }

        private Transform CreateSlotTransform(int position, Vector3 worldPosition)
        {
            GameObject slotObj = new GameObject($"CashierQueueSlot_{position}");
            slotObj.transform.position = worldPosition;
            slotObj.transform.parent = transform;
            slotObj.transform.rotation = Quaternion.identity;

            #if UNITY_EDITOR
            slotObj.AddComponent<QueueSlotGizmo>();
            #endif

            return slotObj.transform;
        }

        private void NotifyQueueAdvance()
        {
            for (int i = 0; i < queuedCustomers.Count; i++)
            {
                string customerId = queuedCustomers[i];
                Transform newSlot = GetSlotAtPosition(i);
                assignedSlots[customerId] = newSlot;

                GameObject customerObj = GameObject.Find(customerId);
                if (customerObj != null)
                {
                    var adapter = customerObj.GetComponent<PopLife.Customers.Runtime.CustomerBlackboardAdapter>();
                    if (adapter != null)
                    {
                        adapter.assignedQueueSlot = newSlot;

                        var destinationSetter = customerObj.GetComponent<Pathfinding.AIDestinationSetter>();
                        if (destinationSetter != null && destinationSetter.target == assignedSlots[customerId])
                        {
                            destinationSetter.target = newSlot;
                            Debug.Log($"[CashierQueueController] 顾客 {customerId} 队位更新到位置 {i}");
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
            if (interactionAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(interactionAnchor.position, 0.3f);
                UnityEditor.Handles.Label(interactionAnchor.position + Vector3.up * 0.5f, "收银点");
            }

            if (queueAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(queueAnchor.position, 0.2f);
                UnityEditor.Handles.Label(queueAnchor.position + Vector3.up * 0.5f, "队列起点");
            }

            if (queueAnchor != null && queueDirection != Vector3.zero)
            {
                Gizmos.color = Color.cyan;
                Vector3 start = queueAnchor.position;
                Vector3 end = start + queueDirection * slotSpacing * 5;
                Gizmos.DrawLine(start, end);
            }

            if (queueSlots != null)
            {
                Gizmos.color = Color.blue;
                for (int i = 0; i < queueSlots.Length; i++)
                {
                    if (queueSlots[i] != null)
                    {
                        Gizmos.DrawWireCube(queueSlots[i].position, Vector3.one * 0.4f);
                        UnityEditor.Handles.Label(queueSlots[i].position + Vector3.up * 0.3f, $"收银队位 {i + 1}");
                    }
                }
            }

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
#endif

        #endregion
    }
}
