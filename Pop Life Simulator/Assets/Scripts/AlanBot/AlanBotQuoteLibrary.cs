using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PopLife.AlanBot
{
    /// <summary>
    /// AlanBot 随机台词语料库（静态类，懒加载）
    /// 数据源：Resources/Sheets/Alanbot/ 下所有 CSV 文件（自动合并）
    /// 每个 CSV 单列纯文本，每行一句台词；支持 Google Sheets 导出格式（trailing comma + 引号包裹）
    /// 抽取策略：纯随机，最近 3 条索引去重
    /// </summary>
    public static class AlanBotQuoteLibrary
    {
        private const string CsvResourceFolder = "Sheets/Alanbot";
        private const int RecentHistorySize = 3;

        private static string[] quotes;
        private static readonly Queue<int> recentIndices = new Queue<int>();
        private static bool initialized;

        /// <summary>
        /// 第一次调用 GetRandomQuote 时加载文件夹下所有 CSV
        /// </summary>
        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            // 自动加载文件夹下所有 TextAsset，未来设计师可拆多文件维护
            var assets = Resources.LoadAll<TextAsset>(CsvResourceFolder);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[AlanBotQuoteLibrary] 未找到任何 CSV: Resources/{CsvResourceFolder}/");
                quotes = System.Array.Empty<string>();
                return;
            }

            var list = new List<string>();
            foreach (var asset in assets)
            {
                ParseCsv(asset.text, list);
            }
            quotes = list.ToArray();

            if (quotes.Length == 0)
                Debug.LogWarning($"[AlanBotQuoteLibrary] CSV 文件存在但解析后无有效台词");
        }

        /// <summary>
        /// 解析 CSV 文本：每行取第一列字段，支持引号包裹和转义
        /// </summary>
        private static void ParseCsv(string text, List<string> output)
        {
            if (string.IsNullOrEmpty(text)) return;

            var lines = text.Split('\n');
            foreach (var raw in lines)
            {
                // 去 BOM(﻿)、CR、首尾空白
                var line = raw.Trim('\r', '﻿', ' ', '\t');
                if (string.IsNullOrEmpty(line)) continue;

                var quote = ParseFirstField(line).Trim();
                if (!string.IsNullOrEmpty(quote))
                    output.Add(quote);
            }
        }

        /// <summary>
        /// 取 CSV 行的第一列字段
        /// 支持：引号包裹的字段（含逗号）、双引号转义("" → ")
        /// </summary>
        private static string ParseFirstField(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;

            // 引号包裹的字段
            if (line[0] == '"')
            {
                var sb = new StringBuilder(line.Length);
                for (int i = 1; i < line.Length; i++)
                {
                    if (line[i] == '"')
                    {
                        // "" 转义为字面量 "
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            return sb.ToString(); // 字段结束
                        }
                    }
                    else
                    {
                        sb.Append(line[i]);
                    }
                }
                return sb.ToString();
            }

            // 普通字段：取到第一个逗号之前
            int commaIdx = line.IndexOf(',');
            return commaIdx < 0 ? line : line.Substring(0, commaIdx);
        }

        /// <summary>
        /// 抽取一条随机台词，自动跳过最近 3 条已抽过的索引
        /// </summary>
        public static string GetRandomQuote()
        {
            EnsureInitialized();
            if (quotes.Length == 0) return string.Empty;

            // 候选数过少时降级为纯随机，避免无限循环
            if (quotes.Length <= RecentHistorySize)
                return quotes[Random.Range(0, quotes.Length)];

            int idx;
            int safety = 16;
            do
            {
                idx = Random.Range(0, quotes.Length);
                safety--;
            } while (recentIndices.Contains(idx) && safety > 0);

            recentIndices.Enqueue(idx);
            while (recentIndices.Count > RecentHistorySize)
                recentIndices.Dequeue();

            return quotes[idx];
        }
    }
}
