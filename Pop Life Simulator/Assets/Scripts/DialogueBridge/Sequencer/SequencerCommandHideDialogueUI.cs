using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 视觉隐藏 Dialogue UI（CanvasGroup alpha=0 + blocksRaycasts=false + interactable=false），
    /// 保存原状态供 ShowDialogueUI() 恢复。
    ///
    /// 用法：HideDialogueUI()
    ///
    /// 注意：Sequencer 命令是并行执行的，单纯写
    ///   HideDialogueUI(); ShowSpotlightAndWait(...); ShowDialogueUI()
    /// 三个会同时启动，ShowDialogueUI 不会等 spotlight 关。
    /// 如需"隐藏 → spotlight → 关后恢复 → 推进对话"完整时序，请用合成命令
    /// <see cref="SequencerCommandShowSpotlightAndContinue"/>。
    /// </summary>
    public class SequencerCommandHideDialogueUI : SequencerCommand
    {
        public void Awake()
        {
            DialogueUIVisibility.Hide();
            if (DialogueDebug.logInfo)
            {
                Debug.Log("Sequencer: HideDialogueUI()");
            }
            Stop();
        }
    }
}
