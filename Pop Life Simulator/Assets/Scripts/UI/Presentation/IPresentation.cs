using System.Collections;

namespace PopLife.UI
{
    /// <summary>
    /// 一条可被 PresentationChannel 串行播放的演出（对话 / 弹窗 / 横幅等）。
    /// </summary>
    public interface IPresentation
    {
        /// <summary>优先级，小 = 先播；同优先级按入队先后（FIFO）。</summary>
        int Priority { get; }

        /// <summary>播放协程：完成前持续 yield，自带阻塞 / 等待逻辑。</summary>
        IEnumerator Play();
    }
}
