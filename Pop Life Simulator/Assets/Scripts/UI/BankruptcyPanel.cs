using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace PopLife
{
    public class BankruptcyPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI debtText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        [Header("Settings")]
        [SerializeField] private string bankruptcyMessage = "BANKRUPTCY";
        [SerializeField] private string debtMessageFormat = "Debt: ${0}";

        private void Awake()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            // 初始隐藏面板
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBankruptcy += ShowBankruptcyPanel;
            }
        }

        private void OnDisable()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBankruptcy -= ShowBankruptcyPanel;
            }
        }

        private void ShowBankruptcyPanel()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            if (messageText != null)
            {
                messageText.text = bankruptcyMessage;
            }

            if (debtText != null && ResourceManager.Instance != null)
            {
                int debt = Mathf.Abs(ResourceManager.Instance.money);
                debtText.text = string.Format(debtMessageFormat, debt);
            }

            // 暂停游戏
            Time.timeScale = 0f;
        }

        private void OnRestartClicked()
        {
            // 恢复时间倍率
            Time.timeScale = 1f;

            // 重新加载当前场景
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnQuitClicked()
        {
            // 恢复时间倍率
            Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
