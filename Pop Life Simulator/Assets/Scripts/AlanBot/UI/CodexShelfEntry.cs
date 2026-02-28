using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using PopLife.Data;

namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// Item Codex 左侧列表中的货架条目
    /// 4种状态：Unselected / Selected / New / Locked
    /// 不使用Unity原生Selectable状态，手动管理视觉表现
    /// </summary>
    public class CodexShelfEntry : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// 条目的4种视觉/交互状态
        /// </summary>
        public enum EntryState { Unselected, Selected, New, Locked }

        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("State Sprites")]
        [SerializeField] private Sprite unselectedSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite newSprite;

        [Header("State Colors")]
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color selectedTextColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color lockedTextColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        [SerializeField] private Color lockedTintColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        // 内部数据
        private ShelfArchetype shelf;
        private EntryState currentState = EntryState.Unselected;
        private Action<CodexShelfEntry> onClicked;

        /// <summary>
        /// 关联的货架原型
        /// </summary>
        public ShelfArchetype Shelf => shelf;

        /// <summary>
        /// 当前状态
        /// </summary>
        public EntryState CurrentState => currentState;

        /// <summary>
        /// 初始化条目
        /// </summary>
        /// <param name="shelfData">货架原型数据</param>
        /// <param name="initialState">初始状态</param>
        /// <param name="clickCallback">点击回调</param>
        public void Initialize(ShelfArchetype shelfData, EntryState initialState,
            Action<CodexShelfEntry> clickCallback)
        {
            shelf = shelfData;
            onClicked = clickCallback;
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
            if (shelf == null) return;

            switch (currentState)
            {
                case EntryState.Unselected:
                    SetBackground(unselectedSprite, Color.white);
                    SetText(shelf.displayName, normalTextColor);
                    SetIcon(true);
                    break;

                case EntryState.Selected:
                    SetBackground(selectedSprite, Color.white);
                    SetText(shelf.displayName, selectedTextColor);
                    SetIcon(true);
                    break;

                case EntryState.New:
                    SetBackground(newSprite, Color.white);
                    SetText(shelf.displayName, normalTextColor);
                    SetIcon(true);
                    break;

                case EntryState.Locked:
                    SetBackground(selectedSprite, lockedTintColor);
                    SetText("???", lockedTextColor);
                    SetIcon(false);
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

        private void SetText(string text, Color color)
        {
            if (nameText == null) return;
            nameText.text = text;
            nameText.color = color;
        }

        private void SetIcon(bool visible)
        {
            if (iconImage == null) return;

            if (visible && shelf != null && shelf.icon != null)
            {
                iconImage.sprite = shelf.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
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
