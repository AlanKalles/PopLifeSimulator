using System.Collections.Generic;


namespace PopLife.Customers.Services
{
// 极简队列占位：统计长度与预测等待
    public class QueueService
    {
        private readonly Dictionary<string, int> _len = new(); // key: pointId
        public int GetLength(string pointId) => _len.TryGetValue(pointId, out var v) ? v : 0;
        public void Enter(string pointId){ _len[pointId] = GetLength(pointId) + 1; }
        public void Leave(string pointId){ var v = GetLength(pointId); _len[pointId] = (v>0)? v-1 : 0; }
        public int PredictWaitSeconds(string pointId, int unitSeconds) => GetLength(pointId) * unitSeconds;
    }
}