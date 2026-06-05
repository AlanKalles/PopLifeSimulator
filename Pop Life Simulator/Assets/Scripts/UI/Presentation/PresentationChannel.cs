using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PopLife.UI
{
    /// <summary>
    /// 通用演出协调通道：对话与弹窗按优先级（小 = 先）单条串行播放，互不重叠。
    /// 懒加载自举单例（DontDestroyOnLoad），<see cref="Instance"/> 永不为 null，
    /// 消除"忘挂场景物体 → 演出静默丢失"的单点故障。
    /// 未来系统（季节横幅 / 结算面板等）可注册自己的 <see cref="IPresentation"/>。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class PresentationChannel : MonoBehaviour
    {
        // ===== 优先级常量（小 = 先播；同优先级 FIFO；留间隔便于未来插入）=====
        public const int PRIORITY_DAY_STORY_DIALOGUE = 100; // 当天剧情 / 教程对话
        public const int PRIORITY_LOTTERY_REVEAL     = 200; // 彩票开奖输赢面板
        // 以下为未来 adopter 预留（本次不实装，仅占位）
        public const int PRIORITY_SEASON_BANNER      = 300; // 季节切换横幅（日初）
        public const int PRIORITY_BANKRUPTCY         = 350; // 破产对话 / 面板（日末）
        public const int PRIORITY_SETTLEMENT         = 400; // 每日结算面板（日末）

        private static PresentationChannel instance;
        private static bool isQuitting;

        /// <summary>
        /// 懒加载自举：首次访问且无实例时自动创建，保证永不为 null。
        /// 退出播放时返回 null，避免在销毁阶段重建。
        /// </summary>
        public static PresentationChannel Instance
        {
            get
            {
                if (isQuitting) return null;
                if (instance == null)
                {
                    var go = new GameObject("[PresentationChannel]");
                    instance = go.AddComponent<PresentationChannel>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private struct Entry
        {
            public int priority;
            public long seq;
            public IPresentation item;
        }

        private readonly List<Entry> queue = new();
        private long seqCounter;
        private bool isRunning;

        private void Awake()
        {
            // 支持手动放置或自举：第一个实例成为单例，其余销毁
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit() => isQuitting = true;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>入队一条演出。runner 空闲时自动启动。</summary>
        public void Enqueue(IPresentation presentation)
        {
            if (presentation == null) return;
            queue.Add(new Entry { priority = presentation.Priority, seq = seqCounter++, item = presentation });
            if (!isRunning) StartCoroutine(RunLoop());
        }

        /// <summary>便捷重载：直接入队一段协程。</summary>
        public void Enqueue(int priority, Func<IEnumerator> play)
            => Enqueue(new DelegatePresentation(priority, play));

        private IEnumerator RunLoop()
        {
            isRunning = true;
            // try/finally 保证即便演出抛异常，isRunning 也会复位，后续 Enqueue 可重启 runner
            try
            {
                while (queue.Count > 0)
                {
                    // 帧闸：让同帧内后续 Enqueue 先入队（保证按优先级排序而非按入队顺序）
                    yield return null;
                    // 尊重通道外启动的对话（含 1 帧让 isConversationActive 翻 true 的窗口）
                    while (DialogueManager.isConversationActive)
                        yield return null;

                    var item = DequeueMin();
                    if (item == null) continue;

                    // 异常隔离：手动 MoveNext 驱动（C# 禁止 yield return 在含 catch 的 try 内）。
                    // 某条演出抛异常时记日志并跳到下一条，绝不卡死整条队列。
                    var e = item.Play();
                    while (true)
                    {
                        bool more;
                        object current = null;
                        try
                        {
                            more = e.MoveNext();
                            if (more) current = e.Current;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[PresentationChannel] presentation threw, skipping: {ex}");
                            break;
                        }
                        if (!more) break;
                        yield return current;
                    }
                }
            }
            finally
            {
                isRunning = false;
            }
        }

        /// <summary>取出最高优先级（min priority，同优先级取最小 seq）的项。</summary>
        private IPresentation DequeueMin()
        {
            if (queue.Count == 0) return null;
            int best = 0;
            for (int i = 1; i < queue.Count; i++)
            {
                if (queue[i].priority < queue[best].priority ||
                    (queue[i].priority == queue[best].priority && queue[i].seq < queue[best].seq))
                    best = i;
            }
            var item = queue[best].item;
            queue.RemoveAt(best);
            return item;
        }
    }
}
