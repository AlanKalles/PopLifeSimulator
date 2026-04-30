using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 显示 spotlight + tooltip 并**等待关闭**。Show 成功后命令保持 isPlaying=true，
    /// 直到 spotlight 被关闭（玩家点击目标 / 屏幕 / Lua HideSpotlight()）才 Stop()。
    ///
    /// 配合对话节点的 auto-continue（在 Sequence 末尾加 <c>Continue()</c> 或对话节点 Inspector 勾选自动推进），
    /// spotlight 关闭瞬间对话自动跳到下一节点。
    ///
    /// 用法：<c>ShowSpotlightAndWait(targetSpec, text, [position], [shape], [mode]); Continue()</c>
    ///
    /// 适合"必须完成动作才能继续教程"的强制场景。
    /// 普通信息展示用 <see cref="SequencerCommandShowSpotlight"/>（fire-and-forget）。
    /// </summary>
    public class SequencerCommandShowSpotlightAndWait : SequencerCommand
    {
        private bool spotlightClosed;
        private bool spotlightShown;

        public void Awake()
        {
            string targetSpec = GetParameter(0);
            string text = GetParameter(1);
            string positionStr = GetParameter(2, "Auto");
            string shapeStr = GetParameter(3, "RoundedRectangle");
            string modeStr = GetParameter(4, "Passthrough");

            spotlightShown = SpotlightSequencerHelpers.TryShow(
                targetSpec, text, positionStr, shapeStr, modeStr,
                onClosed: _ => spotlightClosed = true);

            // Show 失败时立即 Stop，避免对话卡死
            if (!spotlightShown) Stop();
        }

        public void Update()
        {
            if (spotlightShown && spotlightClosed) Stop();
        }
    }
}
