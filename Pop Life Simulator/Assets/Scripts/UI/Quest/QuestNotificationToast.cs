using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCrushers.DialogueSystem;
using PopLife.Data;
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
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private TextMeshProUGUI subMessageText;
        [SerializeField] private Image questIconImage;
        [SerializeField] private Image accentBar;

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
            public Sprite icon;
            public Color titleColor;
            public string audioKey;
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
            EnqueueToast(new ToastData
            {
                title = "NEW QUEST",
                message = QuestLog.GetQuestTitle(questName),
                icon = def?.QuestIcon,
                titleColor = newQuestTitleColor,
                audioKey = AudioKeys.QUEST_NEW
            });
        }

        private void OnQuestCompleted(string questName)
        {
            var def = QuestDataService.Instance?.GetDefinition(questName);
            string successDesc = DialogueLua.GetQuestField(questName, "Success Description").asString;

            EnqueueToast(new ToastData
            {
                title = "QUEST COMPLETE",
                message = QuestLog.GetQuestTitle(questName),
                subMessage = string.IsNullOrEmpty(successDesc) ? null : successDesc,
                icon = def?.QuestIcon,
                titleColor = questCompleteTitleColor,
                audioKey = AudioKeys.QUEST_COMPLETE
            });
        }

        private void OnQuestFailed(string questName)
        {
            EnqueueToast(new ToastData
            {
                title = "QUEST FAILED",
                message = QuestLog.GetQuestTitle(questName),
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
            if (messageText != null) messageText.text = data.message;
            if (subMessageText != null)
            {
                bool hasSubMessage = !string.IsNullOrEmpty(data.subMessage);
                subMessageText.gameObject.SetActive(hasSubMessage);
                if (hasSubMessage) subMessageText.text = data.subMessage;
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
        }

        #endregion
    }
}
