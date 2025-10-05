using UnityEngine;
using System.IO;

namespace PopLife.Utility
{
    /// <summary>
    /// 统一管理跨平台存档路径
    /// - 编辑器模式: 读写 StreamingAssets
    /// - 运行时模式: 读写 persistentDataPath
    /// - 首次运行自动复制初始数据
    /// </summary>
    public static class SavePathManager
    {
        /// <summary>
        /// 获取读取路径
        /// - 编辑器: StreamingAssets
        /// - 运行时: persistentDataPath (首次从StreamingAssets复制)
        /// </summary>
        public static string GetReadPath(string fileName)
        {
#if UNITY_EDITOR
            return Path.Combine(Application.streamingAssetsPath, fileName);
#else
            string runtimePath = Path.Combine(Application.persistentDataPath, fileName);
            // 首次运行从StreamingAssets复制
            if (!File.Exists(runtimePath))
            {
                string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
                if (File.Exists(streamingPath))
                {
                    File.Copy(streamingPath, runtimePath);
                    Debug.Log($"[SavePathManager] 首次运行: 复制 {fileName} 到 persistentDataPath");
                }
                else
                {
                    Debug.LogWarning($"[SavePathManager] StreamingAssets 中未找到 {fileName}");
                }
            }
            return runtimePath;
#endif
        }

        /// <summary>
        /// 获取写入路径
        /// - 编辑器: StreamingAssets
        /// - 运行时: persistentDataPath
        /// </summary>
        public static string GetWritePath(string fileName)
        {
#if UNITY_EDITOR
            return Path.Combine(Application.streamingAssetsPath, fileName);
#else
            return Path.Combine(Application.persistentDataPath, fileName);
#endif
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        public static void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
