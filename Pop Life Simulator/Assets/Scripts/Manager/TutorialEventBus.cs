using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Manager
{
    /// <summary>
    /// Event bus for tutorial marker triggers
    /// 教程标记事件总线
    /// </summary>
    public static class TutorialEventBus
    {
        // 存储每个标记的触发状态（防止重复触发）
        private static HashSet<TutorialMarker> triggeredMarkers = new HashSet<TutorialMarker>();

        // 事件：当任意标记被触发时
        public static event Action<TutorialMarker> OnMarkerTriggered;

        /// <summary>
        /// Raise a tutorial marker from anywhere in the code
        /// 在代码任意位置触发教程标记
        /// </summary>
        /// <param name="marker">The marker to raise</param>
        /// <param name="allowRetrigger">If true, allows the same marker to be raised multiple times</param>
        public static void RaiseMarker(TutorialMarker marker, bool allowRetrigger = false)
        {
            // 防止重复触发（除非明确允许）
            if (!allowRetrigger && triggeredMarkers.Contains(marker))
            {
                return;
            }

            triggeredMarkers.Add(marker);

            Debug.Log($"[TutorialEventBus] Marker raised: {marker}");
            OnMarkerTriggered?.Invoke(marker);
        }

        /// <summary>
        /// Check if a marker has been triggered
        /// 检查标记是否已触发
        /// </summary>
        public static bool IsMarkerTriggered(TutorialMarker marker)
        {
            return triggeredMarkers.Contains(marker);
        }

        /// <summary>
        /// Reset a specific marker (for testing)
        /// 重置特定标记（测试用）
        /// </summary>
        public static void ResetMarker(TutorialMarker marker)
        {
            triggeredMarkers.Remove(marker);
            Debug.Log($"[TutorialEventBus] Marker reset: {marker}");
        }

        /// <summary>
        /// Reset all markers (for testing)
        /// 重置所有标记（测试用）
        /// </summary>
        public static void ResetAllMarkers()
        {
            triggeredMarkers.Clear();
            Debug.Log("[TutorialEventBus] All markers reset");
        }

        /// <summary>
        /// Get all triggered markers (for debugging)
        /// 获取所有已触发标记（调试用）
        /// </summary>
        public static IEnumerable<TutorialMarker> GetTriggeredMarkers()
        {
            return triggeredMarkers;
        }

        /// <summary>
        /// Get count of triggered markers
        /// </summary>
        public static int GetTriggeredCount()
        {
            return triggeredMarkers.Count;
        }
    }
}
