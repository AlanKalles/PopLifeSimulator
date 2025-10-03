using System;
using UnityEngine;
using PopLife.Data;
using PopLife.Customers.Services;

namespace PopLife.Runtime
{
    // 设施实例
    public class FacilityInstance : BuildingInstance
    {
        private FacilityArchetype FA => (FacilityArchetype)archetype;
        private CashierQueueController queueController;

        protected override void Awake()
        {
            base.Awake();
            queueController = GetComponent<CashierQueueController>();
        }

        protected override void OnInitialized() => ApplyEffects();
        private void OnDestroy() => EffectManager.Instance.UnregisterEffect(this);

        public void ApplyEffects()
        {
            foreach (var e in FA.effects)
                EffectManager.Instance.RegisterEffect(this, e);
        }

        /// <summary>
        /// 获取交互锚点（供顾客寻路使用）
        /// </summary>
        public Transform GetInteractionPoint()
        {
            // 如果是收银台且有队列控制器
            if (FA != null && FA.facilityType == FacilityType.Cashier && queueController != null)
            {
                return queueController.GetInteractionPoint();
            }
            // 降级：返回设施自身Transform
            return transform;
        }

        /// <summary>
        /// 获取队列长度（供策略系统查询）
        /// </summary>
        public int GetQueueLength()
        {
            if (queueController != null)
            {
                return queueController.GetQueueLength();
            }
            return 0;
        }
    }
}