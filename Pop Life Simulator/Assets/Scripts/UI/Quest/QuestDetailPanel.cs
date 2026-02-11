using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PopLife.Data;

namespace PopLife.UI.Quest
{
    /// <summary>
    /// 任务详情面板 - 点击追踪条目时打开
    /// 复刻 BuildingDetailPanel 的 panelRoot + CanvasGroup + FadeIn/FadeOut + ESC 关闭模式
    /// </summary>
    public class QuestDetailPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("标题区")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image questIcon;
        [SerializeField] private TextMeshProUGUI questTypeLabel;

        [Header("描述区")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("需求条目")]
        [SerializeField] private Transform entriesContainer;
        [SerializeField] private GameObject entryItemPrefab;

        [Header("截止日期")]
        [SerializeField] private GameObject deadlineSection;
        [SerializeField] private TextMeshProUGUI deadlineText;

        [Header("颁布者")]
        [SerializeField] private GameObject giverSection;
        [SerializeField] private Image giverPortrait;
        [SerializeField] private TextMeshProUGUI giverNameText;

        [Header("奖励")]
        [SerializeField] private Transform rewardsContainer;
        [SerializeField] private GameObject rewardItemPrefab;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        private string currentQuestName;
        private readonly List<GameObject> spawnedEntries = new();
        private readonly List<GameObject> spawnedRewards = new();

        private void Awake()
        {
            canvasGroup = canvasGroup ?? GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (panelRoot != null)
                panelRoot.SetActive(false);

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            // ESC 关闭面板
            if (Input.GetKeyDown(KeyCode.Escape) && panelRoot != null && panelRoot.activeSelf)
            {
                Hide();
            }
        }

        /// <summary>
        /// 显示任务详情
        /// </summary>
        public void Show(string questName)
        {
            currentQuestName = questName;
            var detail = QuestDataService.Instance?.GetQuestDetail(questName);
            if (detail == null) return;

            UpdateContent(detail.Value);

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (panelRoot != null) panelRoot.SetActive(true);

            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void Hide()
        {
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        /// <summary>
        /// 检查面板是否正在显示
        /// </summary>
        public bool IsShowing()
        {
            return panelRoot != null && panelRoot.activeSelf;
        }

        #region 内容更新

        private void UpdateContent(QuestDetailViewModel detail)
        {
            // 标题
            if (titleText != null) titleText.text = detail.displayTitle;

            // 任务图标
            if (questIcon != null)
            {
                if (detail.questIcon != null)
                {
                    questIcon.sprite = detail.questIcon;
                    questIcon.enabled = true;
                }
                else
                {
                    questIcon.enabled = false;
                }
            }

            // 任务类型标签
            if (questTypeLabel != null)
            {
                questTypeLabel.text = detail.questType == QuestType.Main ? "MAIN QUEST" : "SIDE QUEST";
                questTypeLabel.color = detail.questType == QuestType.Main
                    ? new Color(1f, 0.84f, 0f) // 金色
                    : Color.white;
            }

            // 描述
            if (descriptionText != null) descriptionText.text = detail.description;

            // 截止日期
            if (deadlineSection != null)
            {
                if (detail.remainingDays < 0)
                {
                    deadlineSection.SetActive(false);
                }
                else
                {
                    deadlineSection.SetActive(true);
                    if (deadlineText != null)
                    {
                        if (detail.remainingDays <= 1)
                            deadlineText.text = $"<color=#FF4444>{detail.remainingDays} day remaining!</color>";
                        else
                            deadlineText.text = $"{detail.remainingDays} days remaining";
                    }
                }
            }

            // 颁布者
            if (giverSection != null)
            {
                if (!string.IsNullOrEmpty(detail.giverName))
                {
                    giverSection.SetActive(true);
                    if (giverNameText != null) giverNameText.text = detail.giverName;
                    if (giverPortrait != null && detail.giverPortrait != null)
                    {
                        giverPortrait.sprite = detail.giverPortrait;
                        giverPortrait.enabled = true;
                    }
                    else if (giverPortrait != null)
                    {
                        giverPortrait.enabled = false;
                    }
                }
                else
                {
                    giverSection.SetActive(false);
                }
            }

            // 条目列表
            PopulateEntries(detail.entries);

            // 奖励列表
            PopulateRewards(detail.rewards);
        }

        private void PopulateEntries(QuestEntryInfo[] entries)
        {
            foreach (var obj in spawnedEntries)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedEntries.Clear();

            if (entries == null || entries.Length == 0 || entriesContainer == null || entryItemPrefab == null)
                return;

            foreach (var entry in entries)
            {
                var obj = Instantiate(entryItemPrefab, entriesContainer);
                var item = obj.GetComponent<QuestTooltipEntryItem>();
                if (item != null)
                {
                    item.SetData(entry);
                }
                spawnedEntries.Add(obj);
            }
        }

        private void PopulateRewards(QuestReward[] rewards)
        {
            foreach (var obj in spawnedRewards)
            {
                if (obj != null) Destroy(obj);
            }
            spawnedRewards.Clear();

            if (rewards == null || rewards.Length == 0 || rewardsContainer == null || rewardItemPrefab == null)
                return;

            foreach (var reward in rewards)
            {
                var obj = Instantiate(rewardItemPrefab, rewardsContainer);
                var item = obj.GetComponent<QuestRewardItem>();
                if (item != null)
                {
                    item.SetData(reward);
                }
                spawnedRewards.Add(obj);
            }
        }

        #endregion

        #region 动画

        private IEnumerator FadeIn()
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut()
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        #endregion
    }
}
