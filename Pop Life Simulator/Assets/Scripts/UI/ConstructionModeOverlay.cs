using UnityEngine;
using UnityEngine.UI;
using PopLife.Runtime;

namespace PopLife.UI
{
    /// <summary>
    /// 建造模式UI遮罩管理器
    /// 功能：
    /// 1. 进入建造/移动/拆除模式时显示全屏遮罩（不遮挡UI）
    /// 2. 根据不同模式显示不同颜色（Place=浅蓝色, Move=浅黄色, Destroy=浅红色）
    /// 3. 隐藏指定的UI面板（如建筑列表）
    /// 4. 退出模式时恢复所有UI
    /// </summary>
    public class ConstructionModeOverlay : MonoBehaviour
    {
        [Header("遮罩设置")]
        [SerializeField] private GameObject overlayPanel; // 全屏遮罩Panel
        [SerializeField] private Image overlayImage; // Panel的Image组件（用于改变颜色）

        [Header("模式颜色配置")]
        [SerializeField] private Color placeColor = new Color(0.5f, 0.7f, 1f, 0.3f); // 浅蓝色（建造）
        [SerializeField] private Color moveColor = new Color(1f, 0.9f, 0.5f, 0.3f); // 浅黄色（移动）
        [SerializeField] private Color destroyColor = new Color(1f, 0.5f, 0.5f, 0.3f); // 浅红色（拆除）

        [Header("需要隐藏的UI")]
        [SerializeField] private GameObject[] panelsToHide; // 建造时需要隐藏的UI列表（如建筑列表面板）

        [Header("引用")]
        [SerializeField] private ConstructionManager constructionManager;

        private ConstructionManager.Mode lastMode = ConstructionManager.Mode.None;

        void Start()
        {
            // 初始状态：隐藏遮罩
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(false);
            }

            // 自动获取Image组件（如果未手动赋值）
            if (overlayImage == null && overlayPanel != null)
            {
                overlayImage = overlayPanel.GetComponent<Image>();
                if (overlayImage == null)
                {
                    Debug.LogWarning("ConstructionModeOverlay: overlayPanel上没有Image组件，无法改变颜色！");
                }
            }

            // 查找ConstructionManager（如果未手动赋值）
            if (constructionManager == null)
            {
                constructionManager = FindFirstObjectByType<ConstructionManager>();
                if (constructionManager == null)
                {
                    Debug.LogError("ConstructionModeOverlay: 找不到ConstructionManager组件！");
                }
            }
        }

        void Update()
        {
            if (constructionManager == null) return;

            // 检测建造模式变化
            ConstructionManager.Mode currentMode = constructionManager.mode;

            if (currentMode != lastMode)
            {
                OnModeChanged(currentMode);
                lastMode = currentMode;
            }
        }

        /// <summary>
        /// 建造模式切换时的回调
        /// </summary>
        private void OnModeChanged(ConstructionManager.Mode newMode)
        {
            bool isConstructionMode = (newMode == ConstructionManager.Mode.Place ||
                                      newMode == ConstructionManager.Mode.Move ||
                                      newMode == ConstructionManager.Mode.Destroy);

            if (isConstructionMode)
            {
                EnterConstructionMode(newMode);
            }
            else
            {
                ExitConstructionMode();
            }
        }

        /// <summary>
        /// 进入建造模式：显示遮罩，设置颜色，隐藏指定UI
        /// </summary>
        private void EnterConstructionMode(ConstructionManager.Mode mode)
        {
            // 显示遮罩
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(true);
            }

            // 根据模式设置颜色
            if (overlayImage != null)
            {
                overlayImage.color = mode switch
                {
                    ConstructionManager.Mode.Place => placeColor,
                    ConstructionManager.Mode.Move => moveColor,
                    ConstructionManager.Mode.Destroy => destroyColor,
                    _ => placeColor
                };
            }

            // 隐藏指定UI面板
            foreach (GameObject panel in panelsToHide)
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 退出建造模式：隐藏遮罩，恢复所有UI
        /// </summary>
        private void ExitConstructionMode()
        {
            // 隐藏遮罩
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(false);
            }

            // 恢复所有UI面板
            foreach (GameObject panel in panelsToHide)
            {
                if (panel != null)
                {
                    panel.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 手动强制退出建造模式（可由外部调用，如ESC键）
        /// </summary>
        public void ForceExitConstructionMode()
        {
            if (constructionManager != null)
            {
                constructionManager.Cancel();
            }
            ExitConstructionMode();
        }
    }
}
