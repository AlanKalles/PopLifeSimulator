using UnityEngine;
using System.IO;

namespace PopLife.Utility
{
    /// <summary>
    /// 统一管理跨平台存档路径（编辑器与运行时行为一致）
    /// - StreamingAssets：只读模板（设计师维护，进 git）
    /// - persistentDataPath：运行时存档（玩家产生，可清空）
    /// - 首次读取时自动从 StreamingAssets 复制初始模板到 persistentDataPath
    /// 设计师 Editor 工具如需直接编辑模板，应硬编码 Application.dataPath/StreamingAssets/...，绕开此类。
    /// </summary>
    public static class SavePathManager
    {
        /// <summary>
        /// 获取读取路径：始终返回 persistentDataPath。
        /// 文件不存在时自动从 StreamingAssets 模板复制一份过去（首次运行 / 重置后首次启动）。
        /// </summary>
        public static string GetReadPath(string fileName)
        {
            string runtimePath = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(runtimePath))
            {
                string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
                if (File.Exists(streamingPath))
                {
                    EnsureDirectoryExists(runtimePath);
                    File.Copy(streamingPath, runtimePath);
                    Debug.Log($"[SavePathManager] 首次读取：从 StreamingAssets 模板复制 {fileName} 到 persistentDataPath");
                }
                else
                {
                    Debug.LogWarning($"[SavePathManager] StreamingAssets 中未找到模板 {fileName}");
                }
            }
            return runtimePath;
        }

        /// <summary>
        /// 获取写入路径：始终返回 persistentDataPath。
        /// 不会写到 StreamingAssets，避免污染设计模板。
        /// </summary>
        public static string GetWritePath(string fileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
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
