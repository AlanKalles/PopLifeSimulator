using System;
using System.Collections;

namespace PopLife.UI
{
    /// <summary>
    /// 通用面板演出：构造时传入 show(onComplete) 回调；
    /// Play() 调用 show 并等待 onComplete 被触发后结束。
    /// 适用于"显示一个阻塞面板，等玩家关闭"这类场景（如彩票揭晓）。
    /// </summary>
    public class PanelPresentation : IPresentation
    {
        private readonly int priority;
        private readonly Action<Action> show;
        private bool done;

        /// <param name="priority">优先级，小 = 先播。</param>
        /// <param name="show">显示逻辑；必须在面板关闭时调用传入的 onComplete。</param>
        public PanelPresentation(int priority, Action<Action> show)
        {
            this.priority = priority;
            this.show = show;
        }

        public int Priority => priority;

        public IEnumerator Play()
        {
            done = false;
            if (show == null) yield break;
            show(() => done = true);
            while (!done) yield return null;
        }
    }
}
