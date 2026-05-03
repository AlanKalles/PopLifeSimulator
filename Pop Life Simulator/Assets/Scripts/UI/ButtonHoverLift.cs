using UnityEngine;
using UnityEngine.EventSystems;

namespace PopLife.UI
{
    /// <summary>
    /// 通用按钮悬停抬起组件
    /// 鼠标悬停时按钮瞬间向上抬升 liftAmount 像素，离开时立即回到原位
    /// 修改的是 RectTransform.anchoredPosition，与 Animator 的 Image.sprite swap 互不干扰
    /// </summary>
    public class ButtonHoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Lift Settings")]
        [Tooltip("悬停时向上抬升的像素数")]
        [SerializeField] private float liftAmount = 4f;

        private RectTransform rectTransform;
        private Vector2 originalAnchoredPos;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalAnchoredPos = rectTransform.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            rectTransform.anchoredPosition = originalAnchoredPos + new Vector2(0f, liftAmount);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            rectTransform.anchoredPosition = originalAnchoredPos;
        }

        private void OnDisable()
        {
            // 禁用时强制复位，避免按钮卡在抬起状态
            if (rectTransform != null)
                rectTransform.anchoredPosition = originalAnchoredPos;
        }
    }
}
