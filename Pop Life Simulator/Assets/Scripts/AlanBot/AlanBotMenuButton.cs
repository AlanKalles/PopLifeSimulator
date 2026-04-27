using UnityEngine;

namespace PopLife.AlanBot
{
    /// <summary>
    /// 挂在 UI Button 上：在 Button 的 OnClick 中绑定 OpenMenu() 即可打开 AlanBot 选择菜单
    /// 走和点击 AlanBot 一致的流程（暂停行为树 → 表情 → 延迟开面板 → 关闭后恢复状态）
    /// </summary>
    public class AlanBotMenuButton : MonoBehaviour
    {
        [Tooltip("可在 Inspector 拖入；留空时运行时自动 Find")]
        [SerializeField] private AlanBotClickHandler clickHandler;

        /// <summary>
        /// 在 Button 的 OnClick 列表中绑定此方法
        /// </summary>
        public void OpenMenu()
        {
            if (clickHandler == null)
                clickHandler = FindAnyObjectByType<AlanBotClickHandler>();

            if (clickHandler == null)
            {
                Debug.LogWarning("[AlanBotMenuButton] 场景中未找到 AlanBotClickHandler");
                return;
            }

            clickHandler.TryOpenMenu();
        }
    }
}
