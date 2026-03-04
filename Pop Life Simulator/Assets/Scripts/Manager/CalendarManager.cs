using System;
using System.Collections.Generic;
using UnityEngine;
using PopLife.Data;
using PopLife.Runtime;

namespace PopLife
{
    /// <summary>
    /// 日历管理器 — 事件/Buffer 生命周期管理与查询
    /// 订阅 DayLoopManager.OnBuildPhaseStart，每天开始时：
    ///   1. 确定今日活跃的季节事件
    ///   2. Tick 活跃 Buffer（递减/终止概率）
    ///   3. Roll Auto-Buffer（触发概率）
    ///
    /// ⚠️ 本版本不包含经济修正系统，仅提供纯数据查询供日历UI使用
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CalendarManager : MonoBehaviour
    {
        public static CalendarManager Instance { get; private set; }

        [Header("数据源（留空则从 Resources 自动加载）")]
        [SerializeField] private SeasonEventData[] allSeasonEvents;
        [SerializeField] private BufferData[] allBuffers;

        // 运行时状态
        private List<ActiveBuffer> activeBuffers = new();
        private List<SeasonEventData> todaySeasonEvents = new();

        // 事件
        /// <summary>
        /// Buffer 状态变化时广播（日历UI订阅以刷新显示）
        /// </summary>
        public event Action OnCalendarDataChanged;
        public event Action<BufferData> OnBufferStarted;
        public event Action<BufferData> OnBufferEnded;

        #region Unity 生命周期

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 从 Resources 加载（如果 Inspector 未赋值）
            if (allSeasonEvents == null || allSeasonEvents.Length == 0)
                allSeasonEvents = Resources.LoadAll<SeasonEventData>("ScriptableObjects/Calendar/SeasonEvents");

            if (allBuffers == null || allBuffers.Length == 0)
                allBuffers = Resources.LoadAll<BufferData>("ScriptableObjects/Calendar/Buffers");

            Debug.Log($"[CalendarManager] 加载 {allSeasonEvents.Length} 个季节事件, {allBuffers.Length} 个 Buffer 定义");
        }

        private void Start()
        {
            // 加载持久化数据
            LoadBufferState();

            // 订阅每日事件
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart += OnNewDayStart;
            }

            // 第一天安全网：手动触发一次日初逻辑
            OnNewDayStart();
        }

        private void OnDestroy()
        {
            if (DayLoopManager.Instance != null)
            {
                DayLoopManager.Instance.OnBuildPhaseStart -= OnNewDayStart;
            }

            if (Instance == this)
                Instance = null;
        }

        #endregion

        #region 每日逻辑

        /// <summary>
        /// 每天建造阶段开始时调用
        /// </summary>
        private void OnNewDayStart()
        {
            int today = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
            int startingYear = DayLoopManager.Instance != null ? DayLoopManager.Instance.StartingYear : 2126;
            var date = CalendarUtils.AbsoluteDayToDate(today, startingYear);

            // 1. 确定今日活跃的季节事件
            RefreshTodaySeasonEvents(date);

            // 2. Tick 活跃 Buffer
            TickActiveBuffers();

            // 3. Roll Auto-Buffer
            RollAutoBuffers(today);

            // 4. 持久化 Buffer 状态
            SaveBufferState();

            // 5. 通知UI刷新
            OnCalendarDataChanged?.Invoke();

            Debug.Log($"[CalendarManager] Day {today} ({date}): " +
                      $"{todaySeasonEvents.Count} 个季节事件, {activeBuffers.Count} 个活跃 Buffer");
        }

        /// <summary>
        /// 刷新今日活跃的季节事件
        /// </summary>
        private void RefreshTodaySeasonEvents(GameDate date)
        {
            todaySeasonEvents.Clear();

            foreach (var evt in allSeasonEvents)
            {
                if (evt == null) continue;
                if (evt.season != date.season) continue;

                // 检查当前天是否在事件持续范围内
                int eventStart = evt.dayInSeason;
                int eventEnd = evt.dayInSeason + evt.durationDays - 1;
                if (date.dayInSeason >= eventStart && date.dayInSeason <= eventEnd)
                {
                    todaySeasonEvents.Add(evt);
                }
            }
        }

        /// <summary>
        /// Tick 所有活跃 Buffer：递减持续时间或检查终止概率
        /// </summary>
        private void TickActiveBuffers()
        {
            for (int i = activeBuffers.Count - 1; i >= 0; i--)
            {
                var buf = activeBuffers[i];

                if (buf.remainingDays > 0)
                {
                    // 固定时长：递减
                    buf.remainingDays--;
                    if (buf.remainingDays <= 0)
                    {
                        Debug.Log($"[CalendarManager] Buffer '{buf.bufferId}' 时长到期，移除");
                        OnBufferEnded?.Invoke(buf.data);
                        activeBuffers.RemoveAt(i);
                        continue;
                    }
                }
                else if (buf.remainingDays < 0 && buf.data != null)
                {
                    // 无限期：检查终止概率
                    if (UnityEngine.Random.value < buf.data.terminateProbability)
                    {
                        Debug.Log($"[CalendarManager] Buffer '{buf.bufferId}' 终止概率命中，移除");
                        OnBufferEnded?.Invoke(buf.data);
                        activeBuffers.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// 检查所有 Auto-Buffer 的触发概率
        /// </summary>
        private void RollAutoBuffers(int today)
        {
            foreach (var bufData in allBuffers)
            {
                if (bufData == null) continue;
                if (bufData.triggerType != BufferTriggerType.Auto) continue;

                // 检查是否已激活（同一 Buffer 不叠加）
                if (activeBuffers.Exists(b => b.bufferId == bufData.bufferId))
                    continue;

                if (UnityEngine.Random.value < bufData.occurProbability)
                {
                    Debug.Log($"[CalendarManager] Auto-Buffer '{bufData.bufferId}' 触发概率命中，激活");
                    ActivateBuffer(bufData, today);
                }
            }
        }

        #endregion

        #region Buffer 激活

        /// <summary>
        /// 激活一个 Buffer（Auto 或 Manual 共用）
        /// </summary>
        private void ActivateBuffer(BufferData bufData, int startDay)
        {
            var active = new ActiveBuffer
            {
                bufferId = bufData.bufferId,
                startAbsoluteDay = startDay,
                remainingDays = bufData.durationDays > 0 ? bufData.durationDays : -1,
                data = bufData
            };

            activeBuffers.Add(active);
            OnBufferStarted?.Invoke(bufData);
            SaveBufferState();
            OnCalendarDataChanged?.Invoke();
        }

        /// <summary>
        /// 玩家手动触发 Manual-Buffer
        /// </summary>
        public bool TryActivateManualBuffer(BufferData bufData)
        {
            if (bufData == null) return false;
            if (bufData.triggerType != BufferTriggerType.Manual) return false;

            // 检查是否已激活
            if (activeBuffers.Exists(b => b.bufferId == bufData.bufferId))
            {
                Debug.Log($"[CalendarManager] Manual-Buffer '{bufData.bufferId}' 已在激活中，跳过");
                return false;
            }

            // 检查费用
            if (ResourceManager.Instance == null || !ResourceManager.Instance.CanAfford(bufData.hostCost, 0))
            {
                Debug.Log($"[CalendarManager] 资金不足，无法触发 Manual-Buffer '{bufData.bufferId}'");
                return false;
            }

            ResourceManager.Instance.SpendMoney(bufData.hostCost);

            int today = DayLoopManager.Instance != null ? DayLoopManager.Instance.currentDay : 1;
            ActivateBuffer(bufData, today);
            Debug.Log($"[CalendarManager] 手动触发 Buffer '{bufData.bufferId}'，花费 {bufData.hostCost}");
            return true;
        }

        #endregion

        #region 公共查询 API

        /// <summary>
        /// 获取今日活跃的季节事件
        /// </summary>
        public List<SeasonEventData> GetTodaySeasonEvents() => new(todaySeasonEvents);

        /// <summary>
        /// 获取当前所有活跃 Buffer
        /// </summary>
        public List<ActiveBuffer> GetActiveBuffers() => new(activeBuffers);

        /// <summary>
        /// 获取指定 absoluteDay 上的季节事件（供日历格子显示）
        /// 季节事件每年重复，只看 season + dayInSeason
        /// </summary>
        public List<SeasonEventData> GetEventsForDay(int absoluteDay)
        {
            int startingYear = DayLoopManager.Instance != null ? DayLoopManager.Instance.StartingYear : 2126;
            var date = CalendarUtils.AbsoluteDayToDate(absoluteDay, startingYear);
            var result = new List<SeasonEventData>();

            foreach (var evt in allSeasonEvents)
            {
                if (evt == null) continue;
                if (evt.season != date.season) continue;

                int eventStart = evt.dayInSeason;
                int eventEnd = evt.dayInSeason + evt.durationDays - 1;
                if (date.dayInSeason >= eventStart && date.dayInSeason <= eventEnd)
                {
                    result.Add(evt);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取指定 absoluteDay 上活跃的 Buffer（按 start + duration 范围判断）
        /// 用于日历格子显示：在 Buffer 活跃的每一天都显示
        /// </summary>
        public List<ActiveBuffer> GetBuffersForDay(int absoluteDay)
        {
            var result = new List<ActiveBuffer>();

            foreach (var buf in activeBuffers)
            {
                if (buf.data == null) continue;

                int bufStart = buf.startAbsoluteDay;

                if (buf.remainingDays < 0)
                {
                    // 无限期 Buffer：只要 absoluteDay >= startDay 就显示
                    // 但限制在当前季节范围内，避免无限延伸
                    var (seasonStart, seasonEnd) = CalendarUtils.GetSeasonRange(absoluteDay);
                    if (absoluteDay >= bufStart && absoluteDay <= seasonEnd)
                    {
                        result.Add(buf);
                    }
                }
                else
                {
                    // 固定时长 Buffer：检查是否在 [start, start+originalDuration-1] 范围内
                    // 注意 remainingDays 已经在递减，需要通过 startDay + originalDuration 计算
                    int originalDuration = buf.data.durationDays;
                    int bufEnd = bufStart + originalDuration - 1;
                    if (absoluteDay >= bufStart && absoluteDay <= bufEnd)
                    {
                        result.Add(buf);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取所有季节事件 SO（供编辑器或特殊查询使用）
        /// </summary>
        public SeasonEventData[] GetAllSeasonEvents() => allSeasonEvents;

        /// <summary>
        /// 获取所有 Buffer SO（供编辑器或特殊查询使用）
        /// </summary>
        public BufferData[] GetAllBufferDefinitions() => allBuffers;

        #endregion

        #region 持久化（ES3）

        private const string ES3_KEY_ACTIVE_BUFFERS = "CalendarActiveBuffers";

        private void SaveBufferState()
        {
            ES3.Save(ES3_KEY_ACTIVE_BUFFERS, activeBuffers);
        }

        private void LoadBufferState()
        {
            if (!ES3.KeyExists(ES3_KEY_ACTIVE_BUFFERS))
            {
                activeBuffers = new List<ActiveBuffer>();
                return;
            }

            activeBuffers = ES3.Load<List<ActiveBuffer>>(ES3_KEY_ACTIVE_BUFFERS);

            // 重建 SO 引用
            for (int i = activeBuffers.Count - 1; i >= 0; i--)
            {
                var buf = activeBuffers[i];
                buf.data = FindBufferDataById(buf.bufferId);

                if (buf.data == null)
                {
                    Debug.LogWarning($"[CalendarManager] 加载时找不到 Buffer SO '{buf.bufferId}'，移除该记录");
                    activeBuffers.RemoveAt(i);
                }
            }

            Debug.Log($"[CalendarManager] 从持久化加载 {activeBuffers.Count} 个活跃 Buffer");
        }

        private BufferData FindBufferDataById(string bufferId)
        {
            if (string.IsNullOrEmpty(bufferId)) return null;

            foreach (var buf in allBuffers)
            {
                if (buf != null && buf.bufferId == bufferId)
                    return buf;
            }

            return null;
        }

        #endregion
    }
}
