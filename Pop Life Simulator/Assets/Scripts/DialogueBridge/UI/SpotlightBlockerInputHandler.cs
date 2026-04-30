using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 全屏遮罩点击监听器。Blocking 模式下挂在 fullscreen blocker Image 上，
    /// 玩家点击屏幕任意位置时触发 onClicked 回调（SpotlightManager 据此 Hide）。
    /// </summary>
    public class SpotlightBlockerInputHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action onClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            onClicked?.Invoke();
        }
    }
}
