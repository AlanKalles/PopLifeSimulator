using PixelCrushers.DialogueSystem.SequencerCommands;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 显示 spotlight + tooltip。**Fire-and-forget**：Show 后立即 Stop()，对话照常推进；
    /// spotlight 异步存在，由后续 SpotlightOff() 或 HideSpotlight() 关闭。
    ///
    /// 如需对话停在节点等待 spotlight 关闭再推进，使用 <c>ShowSpotlightAndWait(...)</c>。
    ///
    /// 用法：<c>ShowSpotlight(targetSpec, text, [position=Auto], [shape=RoundedRectangle], [mode=passthrough])</c>
    ///
    /// targetSpec 智能解析：
    /// <list type="bullet">
    /// <item>"BuildButton" / "id:BuildButton" → 注册表查找</item>
    /// <item>"rect:100,200,300,150" → 屏幕像素坐标</item>
    /// <item>"norm:0.4,0.4,0.2,0.2" → normalized 坐标</item>
    /// </list>
    /// </summary>
    public class SequencerCommandShowSpotlight : SequencerCommand
    {
        public void Awake()
        {
            string targetSpec = GetParameter(0);
            string text = GetParameter(1);
            string positionStr = GetParameter(2, "Auto");
            string shapeStr = GetParameter(3, "RoundedRectangle");
            string modeStr = GetParameter(4, "Passthrough");

            SpotlightSequencerHelpers.TryShow(targetSpec, text, positionStr, shapeStr, modeStr,
                onClosed: null);

            // Fire-and-forget：立即 Stop，对话推进与 spotlight 解耦
            Stop();
        }
    }
}
