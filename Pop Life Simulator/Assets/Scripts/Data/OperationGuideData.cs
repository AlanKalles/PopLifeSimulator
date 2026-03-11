using System;
using UnityEngine;
using PopLife.Manager;

namespace PopLife.Data
{
    /// <summary>
    /// 操作指南数据 - 定义一份多页截图+文字指南
    /// </summary>
    [CreateAssetMenu(menuName = "PopLife/Guide/OperationGuideData")]
    public class OperationGuideData : ScriptableObject
    {
        [HideInInspector] public string guideId;

        [Tooltip("面向玩家的显示名称")]
        public string displayName;

        [TextArea]
        [Tooltip("简要描述（集合面板中显示）")]
        public string description;

        [Tooltip("图标（可选，null时用首页截图）")]
        public Sprite icon;

        [Tooltip("排序优先级，数值越小越靠前")]
        public int sortOrder = 100;

        [Header("Marker 触发")]
        [Tooltip("当此 Marker 触发时自动显示此指南（None = 不自动触发）")]
        public TutorialMarker activationMarker = TutorialMarker.None;

        [Tooltip("指南关闭后自动触发此 Marker（None = 不触发）")]
        public TutorialMarker closingMarker = TutorialMarker.None;

        [Tooltip("多页内容")]
        public GuidePage[] pages;

        private void OnValidate()
        {
            guideId = name;
        }
    }

    /// <summary>
    /// 指南单页内容
    /// </summary>
    [Serializable]
    public class GuidePage
    {
        [Tooltip("页面截图")]
        public Sprite screenshot;

        [Tooltip("页面标题")]
        public string title;

        [TextArea]
        [Tooltip("页面正文")]
        public string body;
    }
}
