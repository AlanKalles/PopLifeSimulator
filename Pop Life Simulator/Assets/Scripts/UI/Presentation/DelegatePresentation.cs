using System;
using System.Collections;

namespace PopLife.UI
{
    /// <summary>用 Func&lt;IEnumerator&gt; 适配的通用演出。</summary>
    public class DelegatePresentation : IPresentation
    {
        private readonly int priority;
        private readonly Func<IEnumerator> play;

        public DelegatePresentation(int priority, Func<IEnumerator> play)
        {
            this.priority = priority;
            this.play = play;
        }

        public int Priority => priority;

        public IEnumerator Play() => play != null ? play() : Empty();

        private static IEnumerator Empty() { yield break; }
    }
}
