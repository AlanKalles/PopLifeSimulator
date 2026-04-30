using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 恢复 Dialogue UI 到 HideDialogueUI() 之前的原状态。
    /// 若未 Hide 过 → noop；若 dialogueUI 已被销毁 → 跳过 restore。
    ///
    /// 用法：ShowDialogueUI()
    /// </summary>
    public class SequencerCommandShowDialogueUI : SequencerCommand
    {
        public void Awake()
        {
            DialogueUIVisibility.Show();
            if (DialogueDebug.logInfo)
            {
                Debug.Log("Sequencer: ShowDialogueUI()");
            }
            Stop();
        }
    }
}
