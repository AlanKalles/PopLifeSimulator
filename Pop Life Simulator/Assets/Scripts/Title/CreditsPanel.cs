using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

namespace PopLife.Title
{
    /// <summary>
    /// Credits 面板：文字从底部缓慢上滚到顶部
    /// OnEnable 启动 PrimeTween，OnDisable 停止并复位；Close 按钮关闭
    /// </summary>
    public class CreditsPanel : MonoBehaviour
    {
        [Header("滚动")]
        [SerializeField] private RectTransform scrollContent;
        [Tooltip("起始 anchoredPosition.y（一般为负值，让文字位于视口下方）")]
        [SerializeField] private float startY = -800f;
        [Tooltip("终止 anchoredPosition.y（一般为正值，让文字滚出视口顶部）")]
        [SerializeField] private float endY = 800f;
        [SerializeField] private float scrollDuration = 20f;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        private Tween currentTween;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnEnable()
        {
            if (scrollContent == null) return;

            // 复位到起始位置
            Vector2 pos = scrollContent.anchoredPosition;
            pos.y = startY;
            scrollContent.anchoredPosition = pos;

            // 启动滚动
            if (currentTween.isAlive) currentTween.Stop();
            currentTween = Tween.UIAnchoredPositionY(scrollContent, endY, scrollDuration, Ease.Linear);
        }

        private void OnDisable()
        {
            if (currentTween.isAlive) currentTween.Stop();
        }

        private void OnCloseClicked()
        {
            gameObject.SetActive(false);
        }
    }
}
