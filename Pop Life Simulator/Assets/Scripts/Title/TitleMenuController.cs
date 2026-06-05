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

        [Header("场景过渡（优先）")]
        [Tooltip("品牌化加载页过渡——异步加载 + 黑屏 + Logo。配置后 Start 按钮走过渡流程")]
        [SerializeField] private TitleSceneTransition transition;

        [Header("场景加载（fallback）")]
        [Tooltip("当 transition 未配置时使用，同步加载会导致画面短暂卡顿")]
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
            // 优先走过渡
            if (transition != null)
            {
                transition.BeginTransition();
                return;
            }
            // Fallback：直接同步加载（会感觉卡）
            if (sceneLoader != null)
            {
                sceneLoader.LoadScene();
                return;
            }
            Debug.LogWarning("[TitleMenuController] 没有配置 TitleSceneTransition 或 SceneLoader，Start 按钮无法跳转");
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
