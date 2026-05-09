using UnityEngine;
using UnityEngine.UI;

namespace PopLife.Title
{
    /// <summary>
    /// Title 主菜单控制器：Start / Settings / Credits 三按钮
    /// Start 调用 SceneLoader.LoadScene()（无参，目标场景由 SceneLoader.sceneToLoad Inspector 字段决定）
    /// </summary>
    public class TitleMenuController : MonoBehaviour
    {
        [Header("按钮")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;

        [Header("场景加载")]
        [Tooltip("挂在场景某 GameObject 上的 SceneLoader 组件，sceneToLoad 字段需在 Inspector 设置（如 LatestUpdate）")]
        [SerializeField] private SceneLoader sceneLoader;

        [Header("子面板")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject creditsPanel;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (creditsButton != null) creditsButton.onClick.AddListener(OnCreditsClicked);

            // 初始关闭子面板
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (creditsButton != null) creditsButton.onClick.RemoveListener(OnCreditsClicked);
        }

        private void OnStartClicked()
        {
            if (sceneLoader == null)
            {
                Debug.LogWarning("[TitleMenuController] SceneLoader 未配置，Start 按钮无法跳转");
                return;
            }
            sceneLoader.LoadScene();
        }

        private void OnSettingsClicked()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        private void OnCreditsClicked()
        {
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }
    }
}
