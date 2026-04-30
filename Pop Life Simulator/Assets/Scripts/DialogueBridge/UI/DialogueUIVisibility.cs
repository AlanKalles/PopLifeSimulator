using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// 唯一接触 PixelCrushers Dialogue UI 隐藏/显示的工具类。
    ///
    /// 行为：CanvasGroup 三件套 — alpha=0 + blocksRaycasts=false + interactable=false。
    /// **不**用 SetActive(false)（避免影响 PixelCrushers UI 组件的 enable/disable 状态机）。
    ///
    /// 原状态保存：保存原 alpha / blocksRaycasts / interactable，Show() 时恢复，
    /// 处理 dialogue UI 原本是半透明等情况，不直接覆盖。
    ///
    /// **Token 机制**（防连续 spotlight 节点踩 UI 状态）：
    /// 每次 Hide() 返回一个递增 token；Show(token) 仅当传入的 token 与当前 owner 一致才恢复。
    /// 这样可避免：node2 协程的 Show(token1) 在 node3 已 Hide(token2) 后错误恢复 node3 的 spotlight 状态。
    /// 无 token 版 Hide()/Show() 仍可用（强制恢复语义），适合独立 Sequencer 命令 / Lua 调用。
    ///
    /// 调用方：
    /// - <see cref="SequencerCommandShowSpotlightAndContinue"/> 用 token 版本（防连续踩状态）
    /// - <see cref="SequencerCommandHideDialogueUI"/> / <see cref="SequencerCommandShowDialogueUI"/> 用无 token 版本
    /// - PopLifeLuaFunctions.HideDialogueUI / ShowDialogueUI 用无 token 版本
    /// </summary>
    public static class DialogueUIVisibility
    {
        private struct SavedState
        {
            public int token;
            public CanvasGroup group;
            public float alpha;
            public bool blocksRaycasts;
            public bool interactable;
            public bool addedByUs;
        }

        private static SavedState? saved;
        private static int nextToken = 1;   // 0 保留为无效 token

        /// <summary>
        /// 视觉隐藏 Dialogue UI 并保存原状态。返回本次隐藏的 owner token。
        ///
        /// 行为：
        /// - 第一次 Hide：保存原状态 + 设 alpha/raycast/interactable + 返回新 token
        /// - 已有 Hide 未恢复时再次 Hide（同一 UI 实例）：仅更新 token 让旧 owner 失效，**不**覆盖原状态
        /// - 失败（dialogueUI 不可用）：返回 0（无效 token）
        /// </summary>
        public static int Hide()
        {
            var ui = DialogueManager.dialogueUI as MonoBehaviour;
            if (ui == null) return 0;

            var cg = ui.GetComponent<CanvasGroup>();

            // 已隐藏同一实例：仅更新 token 让旧 owner 失效，保留原 saved 状态
            if (saved.HasValue && saved.Value.group == cg && cg != null)
            {
                var s = saved.Value;
                s.token = nextToken++;
                saved = s;
                return s.token;
            }

            // 不同 UI 或第一次：保存原状态
            bool added = false;
            if (cg == null)
            {
                cg = ui.gameObject.AddComponent<CanvasGroup>();
                added = true;
            }

            int token = nextToken++;
            saved = new SavedState
            {
                token = token,
                group = cg,
                alpha = cg.alpha,
                blocksRaycasts = cg.blocksRaycasts,
                interactable = cg.interactable,
                addedByUs = added,
            };

            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            return token;
        }

        /// <summary>
        /// 仅当 token 是当前 owner 时恢复。否则 noop。
        /// 用于"我隐藏了我恢复"的精确配对场景，避免旧协程错误恢复新隐藏的 UI 状态。
        /// </summary>
        public static void Show(int token)
        {
            if (!saved.HasValue) return;
            if (saved.Value.token != token) return;   // 已被新 Hide 取代，忽略
            DoRestore();
        }

        /// <summary>
        /// 仅当 token 是当前 owner 时，重新强制隐藏当前 Dialogue UI。
        /// 用于等待 Dialogue System 切节点期间防止第三方 UI 动画/刷新临时改回 alpha。
        /// </summary>
        public static void KeepHidden(int token)
        {
            if (!saved.HasValue) return;
            if (saved.Value.token != token) return;

            var s = saved.Value;
            if (s.group == null) return;

            s.group.alpha = 0f;
            s.group.blocksRaycasts = false;
            s.group.interactable = false;
        }

        /// <summary>
        /// 强制恢复（不区分 token），用于独立 Sequencer/Lua 命令的"全局恢复"语义。
        /// 若 dialogueUI 已被销毁 → 跳过 restore，仅清空 saved。若未 Hide 过 → noop。
        /// </summary>
        public static void Show()
        {
            if (!saved.HasValue) return;
            DoRestore();
        }

        private static void DoRestore()
        {
            var s = saved.Value;
            if (s.group != null)
            {
                if (s.addedByUs)
                {
                    Object.Destroy(s.group);
                }
                else
                {
                    s.group.alpha = s.alpha;
                    s.group.blocksRaycasts = s.blocksRaycasts;
                    s.group.interactable = s.interactable;
                }
            }
            saved = null;
        }

        public static bool IsHidden => saved.HasValue;

        /// <summary>当前 owner token（0 表示未 Hide）</summary>
        public static int CurrentToken => saved.HasValue ? saved.Value.token : 0;
    }
}
