using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// Sequencer command to play a character-specific emote/reaction sound.
    ///
    /// Usage in Dialogue Editor Sequence field:
    ///   Emote(emoteType)
    ///
    /// Parameters:
    ///   emoteType: 情感类型字符串，如 Laugh, Sigh, Surprise, Gasp
    ///
    /// Examples:
    ///   Emote(Laugh)               立即触发
    ///   Emote(Sigh)@0.5            延迟 0.5 秒触发
    ///   required Emote(Gasp)@1.0   延迟 1 秒 + 对话打断时仍触发
    ///
    /// 具体 AudioClip 由 AudioConfigSO.actorEmotes 按 "当前说话人 + emoteType" 查表决定，
    /// 未配置则回退到 defaultEmotes。
    /// </summary>
    public class SequencerCommandEmote : SequencerCommand
    {
        public void Awake()
        {
            string emoteType = GetParameter(0);

            if (string.IsNullOrEmpty(emoteType))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: Emote() - missing emoteType parameter");
                }
                Stop();
                return;
            }

            // 读取当前说话人（与 TypewriterAudioPerActor 使用相同的取名方式）
            string actorName = null;
            var state = DialogueManager.currentConversationState;
            if (state?.subtitle?.speakerInfo != null)
            {
                actorName = state.subtitle.speakerInfo.nameInDatabase;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEmote(actorName, emoteType);

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: Emote({emoteType}) for actor '{actorName}'");
                }
            }
            else
            {
                Debug.LogWarning("Sequencer: Emote() - AudioManager.Instance is null");
            }

            Stop();
        }
    }
}
