using UnityEngine;
using TMPro;
using PopLife.Manager;

namespace PopLife.GuidanceSystem.Core
{
    /// <summary>
    /// 交互方式枚举
    /// </summary>
    public enum InteractionType
    {
        /// <summary>
        /// 仅遮罩镂空区域检测点击
        /// </summary>
        MaskAreaClick,

        /// <summary>
        /// 任意位置点击即可
        /// </summary>
        AnywhereClick,

        /// <summary>
        /// 遮罩区域点击 + 等待特定Marker触发才能推进
        /// </summary>
        MaskAreaClickWithMarker
    }

    /// <summary>
    /// 单个Guidance节点数据
    /// 包含该节点使用的UI组件引用、文本内容和交互方式
    /// </summary>
    [System.Serializable]
    public class GuidanceNode
    {
        [Header("UI引用 - 从Prefab子对象拖入")]
        [Tooltip("该节点使用的文本框UI对象")]
        public GameObject textBoxUI;

        [Tooltip("该节点使用的遮罩组合对象")]
        public GameObject maskGroup;

        [Tooltip("TextMeshPro文本组件")]
        public TMP_Text textComponent;

        [Header("内容配置")]
        [Tooltip("该节点显示的文本内容")]
        [TextArea(3, 6)]
        public string textContent;

        [Header("交互方式")]
        [Tooltip("玩家如何完成此节点")]
        public InteractionType interactionType = InteractionType.AnywhereClick;

        [Header("Marker等待配置 (仅MaskAreaClickWithMarker时)")]
        [Tooltip("需要等待的Marker才能推进")]
        public TutorialMarker requiredMarker;
    }
}
