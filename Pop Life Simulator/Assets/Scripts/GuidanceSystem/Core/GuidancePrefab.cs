using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using PopLife.Manager;

namespace PopLife.GuidanceSystem.Core
{
    /// <summary>
    /// Guidance预制体核心脚本
    /// 每个Guidance作为独立预制体，包含完整的节点序列、UI组件和交互配置
    /// </summary>
    public class GuidancePrefab : MonoBehaviour
    {
        [Header("基础配置")]
        [Tooltip("Guidance唯一标识符")]
        public string guidanceID;

        [Header("节点序列")]
        [Tooltip("线性节点序列，按顺序播放")]
        public List<GuidanceNode> nodes = new List<GuidanceNode>();

        [Header("结束效果")]
        [Tooltip("Guidance结束时触发的效果")]
        public GuidanceEndEffect endEffect = new GuidanceEndEffect();

        /// <summary>
        /// 当前节点索引
        /// </summary>
        private int currentNodeIndex = -1;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        private bool isRunning = false;


        /// <summary>
        /// Guidance开始事件
        /// </summary>
        public event Action OnGuidanceStarted;

        /// <summary>
        /// Guidance结束事件
        /// </summary>
        public event Action OnGuidanceEnded;

        /// <summary>
        /// 节点切换事件
        /// </summary>
        public event Action<int> OnNodeChanged;

        /// <summary>
        /// 当前是否正在运行
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// 当前节点索引
        /// </summary>
        public int CurrentNodeIndex => currentNodeIndex;

        /// <summary>
        /// 节点总数
        /// </summary>
        public int NodeCount => nodes.Count;

        private void Awake()
        {
            // 初始化时隐藏所有节点UI
            HideAllNodes();
        }

        private void OnEnable()
        {
            // 订阅Marker事件
            TutorialEventBus.OnMarkerTriggered += HandleMarkerTriggered;
        }

        private void OnDisable()
        {
            // 取消订阅Marker事件
            TutorialEventBus.OnMarkerTriggered -= HandleMarkerTriggered;
        }

        /// <summary>
        /// 处理Marker触发事件
        /// </summary>
        private void HandleMarkerTriggered(TutorialMarker marker)
        {
            if (!isRunning || currentNodeIndex < 0 || currentNodeIndex >= nodes.Count)
                return;

            var currentNode = nodes[currentNodeIndex];

            // 只有MaskAreaClickWithMarker类型才需要检查Marker
            if (currentNode.interactionType == InteractionType.MaskAreaClickWithMarker)
            {
                if (marker == currentNode.requiredMarker)
                {
                    Debug.Log($"[GuidancePrefab] Received required marker: {marker}, advancing to next");
                    // 收到marker后立即推进
                    AdvanceToNext();
                }
            }
        }

        private void Update()
        {
            if (!isRunning || currentNodeIndex < 0 || currentNodeIndex >= nodes.Count)
                return;

            // 检测交互
            CheckInteraction();
        }

        /// <summary>
        /// 开始Guidance
        /// </summary>
        public void StartGuidance()
        {
            if (nodes.Count == 0)
            {
                Debug.LogWarning($"[GuidancePrefab] Guidance '{guidanceID}' has no nodes!");
                EndGuidance();
                return;
            }

            isRunning = true;
            currentNodeIndex = -1;

            Debug.Log($"[GuidancePrefab] Starting guidance: {guidanceID}");
            OnGuidanceStarted?.Invoke();

            // 显示第一个节点
            AdvanceToNext();
        }

        /// <summary>
        /// 前进到下一个节点
        /// </summary>
        public void AdvanceToNext()
        {
            if (!isRunning)
                return;

            // 隐藏当前节点
            if (currentNodeIndex >= 0 && currentNodeIndex < nodes.Count)
            {
                HideNode(currentNodeIndex);
            }

            // 前进
            currentNodeIndex++;

            // 检查是否结束
            if (currentNodeIndex >= nodes.Count)
            {
                EndGuidance();
                return;
            }

            // 显示新节点
            ShowNode(currentNodeIndex);
            OnNodeChanged?.Invoke(currentNodeIndex);

            Debug.Log($"[GuidancePrefab] {guidanceID} - Node {currentNodeIndex + 1}/{nodes.Count}");
        }

        /// <summary>
        /// 结束Guidance
        /// </summary>
        public void EndGuidance()
        {
            if (!isRunning)
                return;

            isRunning = false;

            // 隐藏所有节点
            HideAllNodes();

            // 执行结束效果
            endEffect?.Execute();

            Debug.Log($"[GuidancePrefab] Guidance '{guidanceID}' ended");
            OnGuidanceEnded?.Invoke();

            // 销毁自身
            Destroy(gameObject);
        }

        /// <summary>
        /// 强制跳过Guidance
        /// </summary>
        public void Skip()
        {
            Debug.Log($"[GuidancePrefab] Guidance '{guidanceID}' skipped");
            EndGuidance();
        }

        /// <summary>
        /// 检测玩家交互
        /// </summary>
        private void CheckInteraction()
        {
            if (!Input.GetMouseButtonDown(0))
                return;

            var currentNode = nodes[currentNodeIndex];

            switch (currentNode.interactionType)
            {
                case InteractionType.AnywhereClick:
                    // 任意位置点击即可前进
                    AdvanceToNext();
                    break;

                case InteractionType.MaskAreaClick:
                    // 检查点击是否在遮罩镂空区域内
                    if (IsClickInMaskArea(currentNode))
                    {
                        AdvanceToNext();
                    }
                    break;

                case InteractionType.MaskAreaClickWithMarker:
                    // 此类型由HandleMarkerTriggered处理，不需要点击检测
                    break;
            }
        }

        /// <summary>
        /// 检查点击是否在遮罩镂空区域内
        /// 通过检测点击是否命中遮罩区域来判断（命中遮罩=不在洞内，未命中=在洞内）
        /// </summary>
        private bool IsClickInMaskArea(GuidanceNode node)
        {
            // 如果没有设置maskGroup，默认允许
            if (node.maskGroup == null)
                return true;

            // 使用EventSystem进行Raycast检测
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // 检查是否点击到了遮罩区域（maskGroup或其子对象）
            foreach (var result in results)
            {
                // 如果点击命中了maskGroup或其子对象，说明点在遮罩上，不在洞内
                if (result.gameObject.transform.IsChildOf(node.maskGroup.transform) ||
                    result.gameObject == node.maskGroup)
                {
                    return false;
                }
            }

            // 没有命中遮罩，说明点击在洞内（穿透到了底层UI）
            return true;
        }

        /// <summary>
        /// 显示指定节点
        /// </summary>
        private void ShowNode(int index)
        {
            if (index < 0 || index >= nodes.Count)
                return;

            var node = nodes[index];

            // 激活UI
            if (node.textBoxUI != null)
                node.textBoxUI.SetActive(true);

            if (node.maskGroup != null)
                node.maskGroup.SetActive(true);

            // 设置文本内容
            if (node.textComponent != null)
                node.textComponent.text = node.textContent;
        }

        /// <summary>
        /// 隐藏指定节点
        /// </summary>
        private void HideNode(int index)
        {
            if (index < 0 || index >= nodes.Count)
                return;

            var node = nodes[index];

            if (node.textBoxUI != null)
                node.textBoxUI.SetActive(false);

            if (node.maskGroup != null)
                node.maskGroup.SetActive(false);
        }

        /// <summary>
        /// 隐藏所有节点
        /// </summary>
        private void HideAllNodes()
        {
            foreach (var node in nodes)
            {
                if (node.textBoxUI != null)
                    node.textBoxUI.SetActive(false);

                if (node.maskGroup != null)
                    node.maskGroup.SetActive(false);
            }
        }

        /// <summary>
        /// 编辑器验证
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(guidanceID))
            {
                guidanceID = gameObject.name;
            }
        }
    }

    /// <summary>
    /// 标记遮罩镂空可点击区域的组件
    /// 挂载在遮罩组合中的透明可点击区域上
    /// </summary>
    public class GuidanceCutoutArea : MonoBehaviour
    {
        // 仅作为标记组件使用
    }
}
