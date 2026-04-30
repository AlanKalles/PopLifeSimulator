using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Passthrough 模式下，若目标不是 Button，由 SpotlightManager 临时挂载此组件捕获点击。
    /// SpotlightManager 在 Show 时 AddComponent，Hide 时 Destroy，确保不污染目标。
    /// </summary>
    public class SpotlightTargetClickProxy : MonoBehaviour, IPointerClickHandler
    {
        public Action onClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            onClicked?.Invoke();
        }
    }
}
