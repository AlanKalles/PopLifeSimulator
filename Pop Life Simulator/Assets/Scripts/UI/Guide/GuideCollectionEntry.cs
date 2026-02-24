using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

namespace PopLife.UI.Guide
{
    /// <summary>
    /// 指南集合列表中的单个条目
    /// </summary>
    public class GuideCollectionEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private Button entryButton;

        private OperationGuideData guideData;
        private Action<OperationGuideData> onClick;

        /// <summary>
        /// 初始化条目显示内容
        /// </summary>
        public void Setup(OperationGuideData data, bool isNew, Action<OperationGuideData> onClickCallback)
        {
            guideData = data;
            onClick = onClickCallback;

            if (nameText != null)
                nameText.text = data.displayName;

            if (descriptionText != null)
                descriptionText.text = data.description;

            // 图标：优先使用icon，fallback到首页截图
            if (iconImage != null)
            {
                Sprite displayIcon = data.icon;
                if (displayIcon == null && data.pages != null && data.pages.Length > 0)
                    displayIcon = data.pages[0].screenshot;

                if (displayIcon != null)
                {
                    iconImage.sprite = displayIcon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            // 未读标记
            if (newBadge != null)
                newBadge.SetActive(isNew);

            // 点击事件
            if (entryButton != null)
            {
                entryButton.onClick.RemoveAllListeners();
                entryButton.onClick.AddListener(OnEntryClicked);
            }
        }

        private void OnEntryClicked()
        {
            onClick?.Invoke(guideData);
        }
    }
}
