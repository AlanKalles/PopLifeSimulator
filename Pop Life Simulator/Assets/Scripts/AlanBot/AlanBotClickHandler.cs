using UnityEngine;
using UnityEngine.EventSystems;
using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using PopLife.Runtime;
using PopLife.Services;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot点击检测 — 随时可打断BT + 交互调度
    /// 玩家在任何时刻都可以点击AlanBot打断行为，触发交互
    /// </summary>
    public class AlanBotClickHandler : MonoBehaviour
    {
        [Header("层检测")]
        [SerializeField] private LayerMask alanBotMask;

        [Header("交互延迟")]
        [SerializeField] private float panelShowDelay = 0.3f;

        [Header("引用")]
        [SerializeField] private AlanBotSelectionPanel selectionPanel;

        private AlanBotController controller;
        private AlanBotAnimator animator;
        private AILerp aiLerp;
        private Camera mainCamera;

        private bool pendingInteraction;

        private void Awake()
        {
            controller = GetComponent<AlanBotController>();
            animator = GetComponent<AlanBotAnimator>();
            aiLerp = GetComponent<AILerp>();
            mainCamera = Camera.main;
        }

        private void Update()
        {
            // 检查等待中的交互
            CheckPendingInteraction();

            // 点击检测：使用 InputGateService 判定纯点击（避免拖拽误触）
            if (InputGateService.Instance == null || !InputGateService.Instance.WasClickThisFrame) return;

            // 防止UI穿透
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // 找出是哪个UI元素在拦截
                var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);
                foreach (var r in results)
                {
                    Debug.Log($"[AlanBotClick] 被UI拦截: {r.gameObject.name} (parent={r.gameObject.transform.parent?.name})");
                }
                return;
            }

            // 如果已经在交互中，忽略
            if (controller.isInteracting)
            {
                Debug.Log("[AlanBotClick] 已在交互中，忽略");
                return;
            }

            // 建造模式中禁止触发交互（Place/Move/Destroy）
            // Move模式由PlacementHandler处理移动，此处仅阻止交互
            var cm = FindAnyObjectByType<ConstructionManager>();
            if (cm != null && cm.mode != ConstructionManager.Mode.None)
            {
                Debug.Log($"[AlanBotClick] 建造模式中({cm.mode})，禁止交互");
                return;
            }

            // Raycast检测AlanBot
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.zero, Mathf.Infinity, alanBotMask);

            if (hit.collider == null)
            {
                Debug.Log("[AlanBotClick] Raycast未命中任何物体");
                return;
            }
            if (hit.collider.gameObject != gameObject)
            {
                Debug.Log($"[AlanBotClick] Raycast命中了其他物体: {hit.collider.gameObject.name}");
                return;
            }

            Debug.Log("[AlanBotClick] 点击成功，触发交互");
            // 触发交互
            HandleInteractionRequest();
        }

        /// <summary>
        /// 根据当前状态决定如何过渡到交互
        /// </summary>
        private void HandleInteractionRequest()
        {
            controller.PauseBehavior();
            controller.isInteracting = true;
            pendingInteraction = true;

            if (animator.IsFinishingWalk)
            {
                // 情况A: 正在结束移动动画 → 等动画播回第一帧
                // aiLerp已在PauseBehavior中停止
                // 等 IsFinishingWalk 变 false 后执行交互
            }
            else if (animator.IsBouncing)
            {
                // 情况B: 正在跳跃 → 等跳完再交互
                // 等 IsBouncing 变 false 后执行交互
            }
            else
            {
                // 情况C: 空闲/表情状态 → 直接交互
                pendingInteraction = false;
                ExecuteInteraction();
            }
        }

        /// <summary>
        /// Update中检查等待完成的交互
        /// </summary>
        private void CheckPendingInteraction()
        {
            if (!pendingInteraction) return;
            if (animator.IsBouncing || animator.IsFinishingWalk) return;

            pendingInteraction = false;
            ExecuteInteraction();
        }

        /// <summary>
        /// 执行交互：显示交互表情 → 延迟后打开面板
        /// </summary>
        private void ExecuteInteraction()
        {
            animator.ShowInteractionExpression();
            StartCoroutine(ShowPanelAfterDelay(panelShowDelay));
        }

        private IEnumerator ShowPanelAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (selectionPanel != null)
            {
                selectionPanel.Show(OnSelectionPanelHidden);
            }
        }

        /// <summary>
        /// SelectionPanel关闭时的回调 — 恢复AlanBot状态
        /// </summary>
        private void OnSelectionPanelHidden()
        {
            if (controller == null) return;

            controller.isInteracting = false;

            // 回到BaseModel（从Speechless回来，skipBounce=true所以不跳）
            if (animator != null)
                animator.ReturnToBaseModel();

            // 恢复行为树（仅在营业阶段）
            if (DayLoopManager.Instance != null &&
                DayLoopManager.Instance.currentPhase == GamePhase.OpenPhase)
            {
                controller.ResumeBehavior();
            }
        }
    }
}
