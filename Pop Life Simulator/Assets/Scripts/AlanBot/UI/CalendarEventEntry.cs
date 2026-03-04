namespace PopLife.AlanBot.UI
{
    /// <summary>
    /// 日历事件类型
    /// </summary>
    public enum CalendarEventType
    {
        SeasonEvent,
        Buffer,
        DueDate
    }

    /// <summary>
    /// 日历格子内的统一事件条目（供 DayBlock 和 Tooltip 使用）
    /// </summary>
    public struct CalendarEventEntry
    {
        public CalendarEventType type;
        public string displayName;
        public string description;
        public string questName;    // 仅 DueDate 使用
        public string giverName;    // 仅 DueDate 使用
    }
}
