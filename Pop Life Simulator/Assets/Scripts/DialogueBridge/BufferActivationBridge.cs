using UnityEngine;
using PopLife.Data;

namespace PopLife.DialogueBridge
{
    /// <summary>
    /// 监听 Buffer 激活事件，按 BufferData.activationConversationTitle 启动对话
    /// 用 DialogueTriggerHelper.QueueConversation：若已有对话进行中（如教程对话），等其结束后再启动，避免冲突或丢失
    /// </summary>
    public class BufferActivationBridge : MonoBehaviour
    {
        private void OnEnable()
        {
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnBufferStarted += HandleBufferStarted;
        }

        private void OnDisable()
        {
            if (CalendarManager.Instance != null)
                CalendarManager.Instance.OnBufferStarted -= HandleBufferStarted;
        }

        private void HandleBufferStarted(BufferData buf)
        {
            if (buf == null || string.IsNullOrEmpty(buf.activationConversationTitle))
                return;

            DialogueTriggerHelper.QueueConversation(buf.activationConversationTitle);
        }
    }
}
