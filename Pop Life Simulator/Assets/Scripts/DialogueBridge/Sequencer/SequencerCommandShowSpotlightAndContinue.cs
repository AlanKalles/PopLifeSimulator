using System.Collections;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// 场景 1 合成命令：隐藏 Dialogue UI → 显示 spotlight → 等关闭 → 推进对话 → 恢复 Dialogue UI。
    ///
    /// ============== 设计师必读 ==============
    /// 1. **不要追加** <c>Continue()</c>：命令内部已自包含 OnContinue 调用。Sequencer 并行执行，
    ///    外部 Continue() 会立即触发抢跑。
    /// 2. **节点必须无 auto-continue**：使用此命令的节点不能配置 "Always Auto Continue Conversation"
    ///    或类似自动推进设置。否则节点 subtitle 渲染完会立刻推进，spotlight 等关闭逻辑失效。
    /// 3. **节点应是空节点**（DialogueText 留空）：命令进入即 Hide UI，节点对话文本会被吞。
    ///    推荐用法：node2 → [空节点放此命令] → node3。
    /// ========================================
    ///
    /// **关键时序**（避免视觉闪烁）：spotlight 关闭 → 先 OnContinue → 等一帧让 dialogue system
    /// 完成 state 切换 + 渲染下一节点 → 再恢复 Dialogue UI alpha。
    /// 否则可能在 dialogue UI 显示旧节点状态时 alpha 已恢复，玩家看到一帧旧内容。
    ///
    /// 协程在 <see cref="DialogueManager"/>.instance 上启动（DontDestroyOnLoad），
    /// 不依赖本命令 GameObject 的生命周期——sequencer 销毁本命令后协程仍能完成。
    ///
    /// 命令本身 fire-and-forget 立即 Stop()，让 sequence 完成；
    /// 节点处于 finished 状态等推进——dialogue UI 隐藏期间玩家无法点 Continue 按钮，
    /// 因此节点不会自动推进，等到 spotlight 关闭由协程的 OnContinue 触发。
    ///
    /// 用法：
    /// <code>ShowSpotlightAndContinue(BuildButton, "Click here", Right, RoundedRectangle, Passthrough)</code>
    /// </summary>
    public class SequencerCommandShowSpotlightAndContinue : SequencerCommand
    {
        private const float MaxWaitForNextLineSeconds = 1f;
        private const int ExtraHiddenFramesAfterNextLine = 2;
        private const float DefaultSpotlightFadeOutSeconds = 0.2f;

        public void Awake()
        {
            string targetSpec = GetParameter(0);
            string text = GetParameter(1);
            string positionStr = GetParameter(2, "Auto");
            string shapeStr = GetParameter(3, "RoundedRectangle");
            string modeStr = GetParameter(4, "Passthrough");

            // 1. 隐藏 dialogue UI 并获取 owner token（防连续 spotlight 节点踩状态）
            int hideToken = DialogueUIVisibility.Hide();

            // 2. 显示 spotlight，注册 onClosed: 启动协程推进对话 + 延迟恢复（仅当 token 仍是 owner）
            bool spotlightShown = SpotlightSequencerHelpers.TryShow(
                targetSpec, text, positionStr, shapeStr, modeStr,
                onClosed: _ => RunFinish(hideToken));

            // 3. spotlight 显示失败 → 走相同的 finish 路径推进对话
            // 一致性：失败也走 coroutine 走相同时序；DialogueManager 不可用则同步降级
            if (!spotlightShown)
            {
                RunFinish(hideToken);
            }

            // 4. 命令立即结束（fire-and-forget）。OnContinue 由协程负责，spotlight 异步存在。
            // 不阻塞 sequence —— 否则当 sequencer 销毁本命令时协程会被打断。
            Stop();
        }

        /// <summary>
        /// 启动 finish 协程；DialogueManager 不可用时降级为同步执行。
        /// </summary>
        private static void RunFinish(int hideToken)
        {
            var runner = DialogueManager.instance;
            if (runner != null)
            {
                runner.StartCoroutine(FinishAfterContinue(hideToken));
            }
            else
            {
                // 降级：同步执行（极少见。此时也无 typewriter/state 切换问题，因为 DialogueManager 已无效）
                AdvanceDialogue();
                DialogueUIVisibility.Show(hideToken);
            }
        }

        /// <summary>
        /// spotlight 关闭后的修复：先推进对话，等下一行准备完成，再额外等几帧让
        /// Dialogue System 的 panel/typewriter/continue button 刷新跑完，最后恢复 dialogue UI alpha。
        /// 避免玩家看到旧节点/空 panel 闪现。
        ///
        /// 用 token 版 Show：若期间下一节点已经 Hide 过新 owner，本协程 Show(旧token) noop，
        /// 不会错误恢复新节点的 spotlight 隐藏状态。
        ///
        /// 协程独立于本命令 GameObject 生命周期（在 DialogueManager 上运行），
        /// 因此 sequencer 销毁本命令后协程仍能正确完成。
        /// </summary>
        private static IEnumerator FinishAfterContinue(int hideToken)
        {
            bool nextLinePrepared = false;
            var manager = DialogueManager.instance;
            float restoreNotBefore = Time.realtimeSinceStartup + GetSpotlightFadeOutDuration();
            if (manager != null)
            {
                manager.conversationLinePrepared += OnConversationLinePrepared;
            }

            AdvanceDialogue();
            DialogueUIVisibility.KeepHidden(hideToken);

            // conversationLinePrepared 发生在 UI 显示 subtitle 之前。先等它，避免只按一帧猜时序。
            float timeout = Time.realtimeSinceStartup + MaxWaitForNextLineSeconds;
            while (!nextLinePrepared && Time.realtimeSinceStartup < timeout)
            {
                DialogueUIVisibility.KeepHidden(hideToken);
                yield return null;
            }

            if (manager != null)
            {
                manager.conversationLinePrepared -= OnConversationLinePrepared;
            }

            // 再给 StandardDialogueUI 的 ShowSubtitleWhenMainPanelOpen / SetSubtitleTextContentAfterOpen /
            // ShowContinueButton end-of-frame 逻辑一个稳定窗口。期间持续压住 CanvasGroup。
            for (int i = 0; i < ExtraHiddenFramesAfterNextLine; i++)
            {
                DialogueUIVisibility.KeepHidden(hideToken);
                yield return null;
            }

            DialogueUIVisibility.KeepHidden(hideToken);
            yield return new WaitForEndOfFrame();

            while (Time.realtimeSinceStartup < restoreNotBefore)
            {
                DialogueUIVisibility.KeepHidden(hideToken);
                yield return null;
            }

            DialogueUIVisibility.Show(hideToken);

            void OnConversationLinePrepared(Subtitle _)
            {
                nextLinePrepared = true;
            }
        }

        private static float GetSpotlightFadeOutDuration()
        {
            return SpotlightManager.Instance != null
                ? SpotlightManager.Instance.FadeOutDuration
                : DefaultSpotlightFadeOutSeconds;
        }

        /// <summary>推进当前对话节点到下一节点。等同于玩家点 Continue 按钮。</summary>
        private static void AdvanceDialogue()
        {
            var ui = DialogueManager.standardDialogueUI;
            if (ui != null) ui.OnContinue();
        }
    }
}
