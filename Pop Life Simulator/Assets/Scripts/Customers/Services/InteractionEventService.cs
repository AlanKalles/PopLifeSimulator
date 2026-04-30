using System.Collections.Generic;
using UnityEngine;
using PopLife.Customers.Runtime;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife.Customers.Services
{
    public class InteractionEventService : MonoBehaviour
    {
        public static InteractionEventService Instance { get; private set; }

        [Header("交互事件配置")]
        [Tooltip("触发交互事件的概率 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float interactionProbability = 0.2f;

        [Header("调试")]
        [SerializeField] private int loadedEventCount = 0;

        private InteractionEventData[] allEvents;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 从 Resources 加载所有交互事件 SO
            allEvents = Resources.LoadAll<InteractionEventData>(
                "ScriptableObjects/InteractionEvents");
            loadedEventCount = allEvents?.Length ?? 0;
            Debug.Log($"[InteractionEventService] 加载了 {loadedEventCount} 个交互事件");
        }

        /// <summary>
        /// 在顾客到达货架队首后调用。掷骰概率 → 筛选匹配当前货架的事件 → 随机返回一个。
        /// 返回 null 表示本次不触发交互。
        /// </summary>
        public InteractionEventData TryRollInteraction(CustomerAgent agent, ShelfInstance currentShelf)
        {
            var tutorialController = Day1InteractionTutorialController.Instance;
            if (tutorialController != null
                && tutorialController.TryResolveInteraction(agent, currentShelf, out var forcedEvent, out var handled)
                && handled)
            {
                return forcedEvent;
            }

            return TryRollInteraction(currentShelf);
        }

        /// <summary>
        /// 旧入口保留给非顾客上下文调用。不会应用 Day 1 专用 override。
        /// </summary>
        public InteractionEventData TryRollInteraction(ShelfInstance currentShelf)
        {
            if (allEvents == null || allEvents.Length == 0) return null;
            if (currentShelf == null) return null;

            // 概率检查
            if (Random.value > interactionProbability) return null;

            var sa = currentShelf.archetype as ShelfArchetype;
            if (sa == null) return null;

            // 筛选匹配当前货架的事件
            var eligible = new List<InteractionEventData>();
            foreach (var evt in allEvents)
            {
                if (DoesShelfMatchEvent(sa, evt))
                    eligible.Add(evt);
            }

            if (eligible.Count == 0) return null;
            return eligible[Random.Range(0, eligible.Count)];
        }

        /// <summary>
        /// 检查某个具体货架是否满足事件的过滤条件。
        /// </summary>
        private bool DoesShelfMatchEvent(ShelfArchetype sa, InteractionEventData evt)
        {
            // 无过滤条件 = 通用事件，任何货架都匹配
            if (!evt.UseFilterCategory && !evt.UseFilterShelf)
                return true;

            if (evt.UseFilterCategory && sa.category != evt.RequiredCategory)
                return false;

            if (evt.UseFilterShelf
                && evt.RequiredShelves != null
                && evt.RequiredShelves.Length > 0
                && !System.Array.Exists(evt.RequiredShelves, s => s == sa))
                return false;

            return true;
        }
    }
}
