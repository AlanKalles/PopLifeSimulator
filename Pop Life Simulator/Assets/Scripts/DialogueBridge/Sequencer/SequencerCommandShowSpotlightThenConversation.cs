using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 场景 2 合成命令：spotlight 异步显示；当前对话照常由玩家 Continue 推进直至结束；
    /// spotlight 关闭后启动 nextConversation。
    ///
    /// 用法（在最后 node 的 Sequence 字段）：
    /// <code>
    /// ShowSpotlightThenConversation(BuildButton, "Click here", Right, RoundedRectangle, Passthrough, Tutorial/NextConversation)
    /// </code>
    ///
    /// 流程时序：
    /// 1. 命令立即调 spotlight Show + 立即 Stop()
    /// 2. 节点 sequence 完成 → 玩家点 Continue 推进对话
    /// 3. 当前 conversation 走到最后 → 对话结束 (isConversationActive=false)
    /// 4. 玩家关 spotlight → onClosed 触发 → QueueConversation 启动 nextConv
    /// 5. 边界：玩家先关 spotlight 再点 Continue → QueueConversation 协程等当前对话结束后才启动新对话（不会抢启动）
    ///
    /// 此命令不隐藏 dialogue UI。如需"隐藏 UI + spotlight + 关后启动新对话"，
    /// 请在最后 node 之前加一个空节点用 <see cref="SequencerCommandShowSpotlightAndContinue"/> 衔接。
    /// </summary>
    public class SequencerCommandShowSpotlightThenConversation : SequencerCommand
    {
        public void Awake()
        {
            string targetSpec = GetParameter(0);
            string text = GetParameter(1);
            string positionStr = GetParameter(2, "Auto");
            string shapeStr = GetParameter(3, "RoundedRectangle");
            string modeStr = GetParameter(4, "Passthrough");
            string nextConv = GetParameter(5);

            if (string.IsNullOrEmpty(nextConv))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: ShowSpotlightThenConversation - nextConversation 必填。" +
                        "用法：ShowSpotlightThenConversation(target, text, position, shape, mode, NextConversation)");
                }
                Stop();
                return;
            }

            SpotlightSequencerHelpers.TryShow(
                targetSpec, text, positionStr, shapeStr, modeStr,
                onClosed: _ => DialogueTriggerHelper.QueueConversation(nextConv));

            if (DialogueDebug.logInfo)
            {
                Debug.Log($"Sequencer: ShowSpotlightThenConversation({targetSpec}, ..., {nextConv})");
            }

            // 命令立即 Stop，节点正常完成 → 玩家点 Continue → 当前对话结束 → spotlight 异步存在
            Stop();
        }
    }
}
