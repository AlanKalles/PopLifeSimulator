using UnityEngine;

namespace PopLife.UI.Restock
{
    /// <summary>
    /// 未补货提示覆盖对象——AlanBot 形象 + TextMeshPro 文本。
    /// RestockPanel.Open(showNotReadyPrompt=true) 时 SetActive(true)；其他时刻隐藏。
    /// </summary>
    public class NotRestockedPrompt : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        private void Awake()
        {
            if (root == null) root = gameObject;
            Hide();
        }

        public void Show()
        {
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
