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

        // 记录进入建造模式前每个 panel 的原始 active 状态，退出时按原状还原
        // 用 bool[] 与 panelsToHide 等长一一对应，避免 GameObject 作 Dictionary key 的开销
        private bool[] panelsOriginalActive;
        private bool isOverlaying;

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

            // 仅在首次进入建造态时记录原始状态——避免 Place→Move→Destroy 等模式互切时把已经隐藏的状态当成"原始状态"覆盖
            if (!isOverlaying)
            {
                CaptureOriginalActiveStates();
                isOverlaying = true;
            }

            // 隐藏指定UI面板
            if (panelsToHide != null)
            {
                foreach (GameObject panel in panelsToHide)
                {
                    if (panel != null)
                        panel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 退出建造模式：隐藏遮罩，按记录的原始状态恢复UI
        /// </summary>
        private void ExitConstructionMode()
        {
            // 隐藏遮罩
            if (overlayPanel != null)
            {
                overlayPanel.SetActive(false);
            }

            // 恢复 panel 到进入建造模式前的状态（marker 锁定的面板会保持隐藏）
            if (isOverlaying && panelsToHide != null && panelsOriginalActive != null)
            {
                int count = Mathf.Min(panelsToHide.Length, panelsOriginalActive.Length);
                for (int i = 0; i < count; i++)
                {
                    GameObject panel = panelsToHide[i];
                    if (panel != null)
                        panel.SetActive(panelsOriginalActive[i]);
                }
            }

            isOverlaying = false;
        }

        /// <summary>
        /// 抓拍当前 panelsToHide 中每个对象的 active 状态，用于退出时还原
        /// </summary>
        private void CaptureOriginalActiveStates()
        {
            if (panelsToHide == null)
            {
                panelsOriginalActive = null;
                return;
            }

            if (panelsOriginalActive == null || panelsOriginalActive.Length != panelsToHide.Length)
                panelsOriginalActive = new bool[panelsToHide.Length];

            for (int i = 0; i < panelsToHide.Length; i++)
            {
                GameObject panel = panelsToHide[i];
                panelsOriginalActive[i] = panel != null && panel.activeSelf;
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
