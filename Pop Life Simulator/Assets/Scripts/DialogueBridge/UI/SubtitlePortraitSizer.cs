using UnityEngine;
using UnityEngine.UI;
using PixelCrushers.DialogueSystem;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 根据当前 speaker 是否为 generic Customer actor 动态切换对话 portrait Image 的尺寸。
    /// 普通顾客对话 portrait 使用顾客在场景中的像素小人静态图，比例与 Midori/Auditor 立绘
    /// 不一致，铺满 800×800 框会很怪。此组件让 Customer 用 580×580 缩小框 + Preserve Aspect，
    /// 其他角色恢复默认 800×800。
    ///
    /// 监听 <see cref="DialogueSystemController.conversationStarted"/>、
    /// <see cref="DialogueSystemController.conversationLinePrepared"/>、
    /// <see cref="DialogueSystemController.conversationEnded"/> 三个事件。
    /// </summary>
    public class SubtitlePortraitSizer : MonoBehaviour
    {
        [Tooltip("被调整的 portrait Image（Standard Subtitle Panel 中的那个）")]
        [SerializeField] private Image portraitImage;

        [Tooltip("Generic 顾客在 Dialogue Database 里的 actor 名字（默认 Customer）")]
        [SerializeField] private string customerActorName = "Customer";

        [Tooltip("Customer 之外的角色用这个尺寸（与原 prefab 一致）")]
        [SerializeField] private Vector2 defaultSize = new Vector2(800f, 800f);

        [Tooltip("Customer actor 用这个尺寸")]
        [SerializeField] private Vector2 customerSize = new Vector2(580f, 580f);

        [Tooltip("两种尺寸都启用 Preserve Aspect，避免像素图被拉伸")]
        [SerializeField] private bool preserveAspect = true;

        private RectTransform portraitRect;

        private void Awake()
        {
            if (portraitImage != null)
            {
                portraitRect = portraitImage.rectTransform;
            }
        }

        private void OnEnable()
        {
            if (DialogueManager.instance == null) return;
            DialogueManager.instance.conversationStarted += OnConversationStarted;
            DialogueManager.instance.conversationLinePrepared += OnConversationLinePrepared;
            DialogueManager.instance.conversationEnded += OnConversationEnded;
        }

        private void OnDisable()
        {
            if (DialogueManager.instance == null) return;
            DialogueManager.instance.conversationStarted -= OnConversationStarted;
            DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
            DialogueManager.instance.conversationEnded -= OnConversationEnded;
        }

        private void OnConversationStarted(Transform actor)
        {
            // 对话起步时还没有 Subtitle 事件触发，先按 conversation 的 actorInfo 预设一次。
            var conv = DialogueManager.instance != null ? DialogueManager.instance.conversationModel : null;
            if (conv == null) return;
            string speaker = conv.actorInfo != null ? conv.actorInfo.nameInDatabase : null;
            ApplySize(speaker);
        }

        private void OnConversationLinePrepared(Subtitle subtitle)
        {
            if (subtitle == null || subtitle.speakerInfo == null) return;
            ApplySize(subtitle.speakerInfo.nameInDatabase);
        }

        private void OnConversationEnded(Transform actor)
        {
            // 重置为默认，避免下一次开启对话时残留 Customer 尺寸
            ApplySize(null);
        }

        private void ApplySize(string speakerNameInDatabase)
        {
            if (portraitRect == null) return;

            bool isCustomer = !string.IsNullOrEmpty(speakerNameInDatabase)
                              && speakerNameInDatabase == customerActorName;
            portraitRect.sizeDelta = isCustomer ? customerSize : defaultSize;

            if (portraitImage != null)
            {
                portraitImage.preserveAspect = preserveAspect;
            }
        }
    }
}
