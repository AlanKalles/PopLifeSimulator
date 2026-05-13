using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCrushers.DialogueSystem;
using PopLife.Data;
using PopLife.Manager;
using PopLife.Quest;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 任务通知 Toast - 显示新任务/任务完成/任务失败的弹出通知
    /// 挂载在 UI Canvas 上，使用队列管理多条通知
    /// </summary>
    public class QuestNotificationToast : MonoBehaviour
    {
        public static QuestNotificationToast Instance { get; private set; }

        [Header("UI 引用")]
        [Tooltip("Toast 根物体（包含背景、标题、消息文本）")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image questIconImage;
        [SerializeField] private Image accentBar;

        [Header("UI - 单消息布局 (NEW QUEST / QUEST FAILED)")]
        [Tooltip("仅显示一行 message 的容器（如 'new quest message' 节点）")]
        [SerializeField] private GameObject singleLayoutRoot;
        [Tooltip("NEW QUEST / QUEST FAILED 显示的单行消息文本")]
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("UI - 双消息布局 (QUEST COMPLETE)")]
        [Tooltip("含 message + sub message 的容器（如 'success or fail' 节点）")]
        [SerializeField] private GameObject dualLayoutRoot;
        [Tooltip("QUEST COMPLETE 显示的主消息（如 'name message' 节点）")]
        [SerializeField] private TextMeshProUGUI completeMessageText;
        [Tooltip("QUEST COMPLETE 显示的副消息（如 'description' 节点）")]
        [SerializeField] private TextMeshProUGUI subMessageText;

        [Header("标题颜色")]
        [SerializeField] private Color newQuestTitleColor = new(0.4f, 0.8f, 1f);
        [SerializeField] private Color questCompleteTitleColor = new(0.4f, 1f, 0.4f);
        [SerializeField] private Color questFailedTitleColor = new(1f, 0.27f, 0.27f);

        [Header("动画设置")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        // 通知队列
        private Queue<ToastData> pendingToasts = new();
        private bool isShowingToast = false;
        private bool dismissRequested = false;

        private struct ToastData
        {
            public string title;
            public string message;
            public string subMessage;
            // true = QUEST COMPLETE 双布局；false = NEW QUEST / QUEST FAILED 单布局
            public bool useDualLayout;
            public Sprite icon;
            public Color titleColor;
            public string audioKey;
            // Toast消失后触发的Marker（None = 不触发）
            public TutorialMarker afterToastMarker;
            // QUEST COMPLETE 专用：toast 淡出后弹出的奖励详情窗口需要查 reward
            public string questName;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 确保初始隐藏
            if (toastRoot != null)
                toastRoot.SetActive(false);
        }

        private void OnEnable()
        {
            QuestLogicManager.OnQuestActivated += OnQuestActivated;
            QuestLogicManager.OnQuestCompleted += OnQuestCompleted;
            QuestLogicManager.OnQuestFailed += OnQuestFailed;
        }

        private void OnDisable()
        {
            QuestLogicManager.OnQuestActivated -= OnQuestActivated;
            QuestLogicManager.OnQuestCompleted -= OnQuestCompleted;
            QuestLogicManager.OnQuestFailed -= OnQuestFailed;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Toast 显示中，点击任意位置关闭
            if (isShowingToast && Input.GetMouseButtonDown(0))
                dismissRequested = true;
        }

        #region 事件处理

        private void OnQuestActivated(string questName)
        {
            var def = QuestDataService.Instance?.GetDefinition(questName);
            var store = QuestLogicManager.Instance?.StateStore;
            EnqueueToast(new ToastData
            {
                title = "NEW QUEST",
                message = store?.GetTitle(questName) ?? questName,
                icon = def?.QuestIcon,
                titleColor = newQuestTitleColor,
                audioKey = AudioKeys.QUEST_NEW
            });
        }

        private void OnQuestCompleted(string questName)
        {
            var def = QuestDataService.Instance?.GetDefinition(questName);
            var store = QuestLogicManager.Instance?.StateStore;
            string successDesc = store?.GetSuccessDescription(questName) ?? "";

            // 仅 TriggerMarkerAfterToast 模式才把 marker 传给 toast
            var afterMarker = TutorialMarker.None;
            if (def != null && def.TriggerMarkerAfterToast && def.CompletionMarker != TutorialMarker.None)
                afterMarker = def.CompletionMarker;

            EnqueueToast(new ToastData
            {
                title = "QUEST COMPLETE",
                message = store?.GetTitle(questName) ?? questName,
                subMessage = string.IsNullOrEmpty(successDesc) ? null : successDesc,
                useDualLayout = true,
                icon = def?.QuestIcon,
                titleColor = questCompleteTitleColor,
                audioKey = AudioKeys.QUEST_COMPLETE,
                afterToastMarker = afterMarker,
                questName = questName
            });
        }

        private void OnQuestFailed(string questName)
        {
            var store = QuestLogicManager.Instance?.StateStore;
            EnqueueToast(new ToastData
            {
                title = "QUEST FAILED",
                message = store?.GetTitle(questName) ?? questName,
                icon = null,
                titleColor = questFailedTitleColor,
                audioKey = AudioKeys.QUEST_FAILED
            });
        }

        #endregion

        #region Toast 显示

        private void EnqueueToast(ToastData data)
        {
            pendingToasts.Enqueue(data);

            if (DialogueManager.isConversationActive)
            {
                // 对话中只入队，等对话结束后再播放
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
                DialogueManager.instance.conversationEnded += OnConversationEnded;
            }
            else if (!isShowingToast)
            {
                StartCoroutine(ShowToastQueue());
            }
        }

        private void OnConversationEnded(Transform actor)
        {
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
            if (!isShowingToast && pendingToasts.Count > 0)
                StartCoroutine(ShowToastQueue());
        }

        private IEnumerator ShowToastQueue()
        {
            isShowingToast = true;

            while (pendingToasts.Count > 0)
            {
                var data = pendingToasts.Dequeue();
                yield return StartCoroutine(ShowSingleToast(data));

                // 多条通知之间的间隔
                if (pendingToasts.Count > 0)
                    yield return new WaitForSecondsRealtime(0.3f);
            }

            isShowingToast = false;
        }

        private IEnumerator ShowSingleToast(ToastData data)
        {
            // 填充内容
            if (titleText != null) titleText.text = data.title;

            // 切换布局：双布局 (QUEST COMPLETE) vs 单布局 (NEW QUEST / QUEST FAILED)
            if (singleLayoutRoot != null) singleLayoutRoot.SetActive(!data.useDualLayout);
            if (dualLayoutRoot != null) dualLayoutRoot.SetActive(data.useDualLayout);

            if (data.useDualLayout)
            {
                if (completeMessageText != null) completeMessageText.text = data.message ?? "";
                if (subMessageText != null)
                {
                    bool hasSub = !string.IsNullOrEmpty(data.subMessage);
                    subMessageText.gameObject.SetActive(hasSub);
                    if (hasSub) subMessageText.text = data.subMessage;
                }
            }
            else
            {
                if (messageText != null) messageText.text = data.message ?? "";
            }

            if (questIconImage != null)
            {
                if (data.icon != null)
                {
                    questIconImage.sprite = data.icon;
                    questIconImage.gameObject.SetActive(true);
                }
                else
                {
                    questIconImage.gameObject.SetActive(false);
                }
            }

            if (accentBar != null) accentBar.color = data.titleColor;
            if (titleText != null) titleText.color = data.titleColor;

            // 播放音效
            if (!string.IsNullOrEmpty(data.audioKey))
                AudioManager.Instance?.PlaySound(data.audioKey);

            // 显示并淡入
            if (toastRoot != null) toastRoot.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            dismissRequested = false;

            // 淡入
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // 等待玩家点击任意位置
            while (!dismissRequested)
                yield return null;

            // 淡出
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (toastRoot != null) toastRoot.SetActive(false);

            // 首个新任务Toast完全消失时触发marker
            if (data.title == "NEW QUEST")
                GameStateManager.Instance?.NotifyFirstQuestToastDismissed();

            // QUEST COMPLETE 一级 toast 关闭后，弹出二级奖励详情窗口
            // 阻塞 toast 队列，直到玩家关闭二级窗口（点击窗口外背景）
            if (data.useDualLayout
                && !string.IsNullOrEmpty(data.questName)
                && QuestSuccessRewardPanel.Instance != null)
            {
                QuestSuccessRewardPanel.Instance.Show(data.questName);
                // 等一帧让 IsVisible=true 稳定，避免 Show 在 SetActive 之前被读到 false
                yield return null;
                while (QuestSuccessRewardPanel.Instance != null
                       && QuestSuccessRewardPanel.Instance.IsVisible)
                {
                    yield return null;
                }
            }

            // 任务完成Toast消失后触发配置的Marker
            if (data.afterToastMarker != TutorialMarker.None)
                TutorialEventBus.RaiseMarker(data.afterToastMarker);
        }

        #endregion
    }
}
