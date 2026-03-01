using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PopLife.Customers.Runtime;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// Customer Codex 左侧网格中的顾客条目
    /// 4种状态：Unselected / Selected / New / Locked
    /// 只显示顾客肖像（带Mask裁剪），不显示文字
    /// </summary>
    public class CodexCustomerEntry : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// 条目的4种视觉/交互状态
        /// </summary>
        public enum EntryState { Unselected, Selected, New, Locked }

        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject newIndicator;

        [Header("State Sprites")]
        [SerializeField] private Sprite unselectedBg;
        [SerializeField] private Sprite selectedBg;
        [SerializeField] private Sprite newBg;

        [Header("Locked State")]
        [SerializeField] private Color lockedBgColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color lockedPortraitColor = Color.black;

        // 内部数据
        private CustomerRecord record;
        private EntryState currentState = EntryState.Unselected;
        private Action<CodexCustomerEntry> onClicked;

        /// <summary>
        /// 关联的顾客记录
        /// </summary>
        public CustomerRecord Record => record;

        /// <summary>
        /// 当前状态
        /// </summary>
        public EntryState CurrentState => currentState;

        /// <summary>
        /// 初始化条目
        /// </summary>
        /// <param name="recordData">顾客记录</param>
        /// <param name="portrait">肖像精灵</param>
        /// <param name="initialState">初始状态</param>
        /// <param name="clickCallback">点击回调</param>
        public void Initialize(CustomerRecord recordData, Sprite portrait,
            EntryState initialState, Action<CodexCustomerEntry> clickCallback)
        {
            record = recordData;
            onClicked = clickCallback;

            // 设置肖像
            if (portraitImage != null)
            {
                if (portrait != null)
                {
                    portraitImage.sprite = portrait;
                    portraitImage.preserveAspect = true;
                    portraitImage.enabled = true;
                }
                else
                {
                    portraitImage.enabled = false;
                }
            }

            SetState(initialState);
        }

        /// <summary>
        /// 设置条目状态并更新视觉
        /// </summary>
        public void SetState(EntryState state)
        {
            currentState = state;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            switch (currentState)
            {
                case EntryState.Unselected:
                    SetBackground(unselectedBg, Color.white);
                    SetPortraitColor(Color.white);
                    SetNewIndicator(false);
                    break;

                case EntryState.Selected:
                    SetBackground(selectedBg, Color.white);
                    SetPortraitColor(Color.white);
                    SetNewIndicator(false);
                    break;

                case EntryState.New:
                    SetBackground(newBg, Color.white);
                    SetPortraitColor(Color.white);
                    SetNewIndicator(true);
                    break;

                case EntryState.Locked:
                    SetBackground(unselectedBg, lockedBgColor);
                    SetPortraitColor(lockedPortraitColor);
                    SetNewIndicator(false);
                    break;
            }
        }

        private void SetBackground(Sprite sprite, Color color)
        {
            if (backgroundImage == null) return;
            if (sprite != null)
                backgroundImage.sprite = sprite;
            backgroundImage.color = color;
        }

        private void SetPortraitColor(Color color)
        {
            if (portraitImage != null)
                portraitImage.color = color;
        }

        private void SetNewIndicator(bool visible)
        {
            if (newIndicator != null)
                newIndicator.SetActive(visible);
        }

        /// <summary>
        /// IPointerClickHandler：Locked状态下忽略点击
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (currentState == EntryState.Locked) return;
            onClicked?.Invoke(this);
        }
    }
}