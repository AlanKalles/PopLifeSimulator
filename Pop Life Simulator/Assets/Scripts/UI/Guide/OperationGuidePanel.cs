using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;
using PopLife.Manager;

namespace PopLife.UI.Guide
{
    /// <summary>
    /// 操作指南面板 - 模态多页面板，显示截图+文字指南
    /// 使用 CanvasGroup 控制显隐，root 始终 active
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class OperationGuidePanel : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private Image screenshotImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI pageIndicatorText;

        [Header("Navigation")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeButton;

        private CanvasGroup canvasGroup;
        private OperationGuideData currentGuide;
        private int currentPageIndex;
        private Action onCloseCallback;
        private bool isShowing;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            // 绑定按钮事件
            if (prevButton != null)
                prevButton.onClick.AddListener(PrevPage);
            if (nextButton != null)
                nextButton.onClick.AddListener(NextPage);
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            // 初始隐藏（CanvasGroup方式，GameObject保持active）
            SetCanvasGroupVisible(false);
        }

        private void Update()
        {
            if (!isShowing) return;

            // 键盘操作
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PrevPage();
            }
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                NextPage();
            }
        }

        /// <summary>
        /// 显示指南面板
        /// </summary>
        public void Show(OperationGuideData data, Action onClose = null)
        {
            if (data == null || data.pages == null || data.pages.Length == 0)
            {
                Debug.LogWarning("[OperationGuidePanel] Guide data is null or has no pages");
                return;
            }

            currentGuide = data;
            currentPageIndex = 0;
            onCloseCallback = onClose;
            isShowing = true;

            // 标记为已查看
            if (OperationGuideManager.Instance != null)
                OperationGuideManager.Instance.MarkAsViewed(data.guideId);

            SetCanvasGroupVisible(true);
            DisplayPage(0);
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void Close()
        {
            if (!isShowing) return;

            isShowing = false;
            SetCanvasGroupVisible(false);

            var callback = onCloseCallback;
            onCloseCallback = null;
            currentGuide = null;
            callback?.Invoke();
        }

        /// <summary>
        /// 当前是否正在显示
        /// </summary>
        public bool IsShowing() => isShowing;

        private void PrevPage()
        {
            if (currentGuide == null) return;
            if (currentPageIndex <= 0) return;

            currentPageIndex--;
            DisplayPage(currentPageIndex);
            PlayPageSound();
        }

        private void NextPage()
        {
            if (currentGuide == null) return;
            if (currentPageIndex >= currentGuide.pages.Length - 1) return;

            currentPageIndex++;
            DisplayPage(currentPageIndex);
            PlayPageSound();
        }

        private void DisplayPage(int index)
        {
            if (currentGuide == null || currentGuide.pages == null) return;
            if (index < 0 || index >= currentGuide.pages.Length) return;

            var page = currentGuide.pages[index];

            // 截图
            if (screenshotImage != null)
            {
                if (page.screenshot != null)
                {
                    screenshotImage.sprite = page.screenshot;
                    screenshotImage.enabled = true;
                }
                else
                {
                    screenshotImage.enabled = false;
                }
            }

            // 文本
            if (titleText != null)
                titleText.text = page.title ?? "";

            if (bodyText != null)
                bodyText.text = page.body ?? "";

            // 页码指示
            if (pageIndicatorText != null)
                pageIndicatorText.text = $"{index + 1} / {currentGuide.pages.Length}";

            // 导航按钮可见性
            if (prevButton != null)
                prevButton.gameObject.SetActive(index > 0);

            if (nextButton != null)
                nextButton.gameObject.SetActive(index < currentGuide.pages.Length - 1);
        }

        private void PlayPageSound()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(AudioKeys.UI_GUIDE_PAGE);
        }

        private void SetCanvasGroupVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
