using System;
using UnityEngine;
using PixelCrushers.DialogueSystem;
using PopLife.DialogueBridge.UI;

namespace PopLife.DialogueBridge.Sequencer
{
    /// <summary>
    /// SequencerCommandShowSpotlight 与 SequencerCommandShowSpotlightAndWait 的共享参数解析与
    /// SpotlightManager.Show 调用逻辑。
    /// </summary>
    internal static class SpotlightSequencerHelpers
    {
        /// <summary>
        /// 尝试调用 SpotlightManager.Show。返回是否真正显示（参数非法或 Manager 不可用时返回 false）。
        /// </summary>
        public static bool TryShow(string targetSpec, string text, string positionStr,
            string shapeStr, string modeStr, Action<CloseReason> onClosed)
        {
            if (string.IsNullOrEmpty(targetSpec) || string.IsNullOrEmpty(text))
            {
                if (DialogueDebug.logWarnings)
                {
                    Debug.LogWarning("Sequencer: ShowSpotlight() - targetSpec 与 text 必填。" +
                        "用法：ShowSpotlight(BuildButton, \"提示文字\", Right)");
                }
                return false;
            }

            if (SpotlightManager.Instance == null)
            {
                Debug.LogWarning("Sequencer: ShowSpotlight() - SpotlightManager.Instance is null");
                return false;
            }

            try
            {
                SpotlightManager.Instance.Show(new SpotlightRequest
                {
                    target = SpotlightTargetSpec.ParseSmart(targetSpec),
                    text = text,
                    position = ParseEnum(positionStr, TooltipPosition.Auto),
                    shape = ParseEnum(shapeStr, SpotlightShape.RoundedRectangle),
                    mode = ParseEnum(modeStr, InteractionMode.Passthrough),
                    onClosed = onClosed,
                });

                if (DialogueDebug.logInfo)
                {
                    Debug.Log($"Sequencer: ShowSpotlight({targetSpec}, \"{text}\", {positionStr})");
                }
                return true;
            }
            catch (ArgumentException ex)
            {
                Debug.LogWarning($"Sequencer: ShowSpotlight() - 参数无效: {ex.Message}");
                return false;
            }
        }

        private static T ParseEnum<T>(string s, T fallback) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return Enum.TryParse<T>(s, true, out var v) ? v : fallback;
        }
    }
}
