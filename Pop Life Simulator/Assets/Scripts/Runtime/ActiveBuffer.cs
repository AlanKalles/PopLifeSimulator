using System;
using PopLife.Data;

namespace PopLife.Runtime
{
    /// <summary>
    /// 一个正在生效的 Buffer 的运行时状态
    /// 可序列化用于 ES3 持久化
    /// </summary>
    [Serializable]
    public class ActiveBuffer
    {
        /// <summary>
        /// Buffer SO 的 ID（用于持久化后重建 SO 引用）
        /// </summary>
        public string bufferId;

        /// <summary>
        /// 开始生效的 absoluteDay
        /// </summary>
        public int startAbsoluteDay;

        /// <summary>
        /// 剩余天数
        /// >0 = 固定时长，每天递减
        /// -1 = 无限期（靠终止概率结束）
        /// </summary>
        public int remainingDays;

        /// <summary>
        /// 运行时缓存的 SO 引用（不参与序列化）
        /// </summary>
        [NonSerialized]
        public BufferData data;
    }
}
