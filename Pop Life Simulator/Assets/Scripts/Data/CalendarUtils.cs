using System;

namespace PopLife.Data
{
    /// <summary>
    /// 季节枚举（1年 = 4季节）
    /// </summary>
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    /// <summary>
    /// 游戏日期结构体
    /// </summary>
    public struct GameDate
    {
        public int year;
        public Season season;
        public int dayInSeason; // 1-12

        public override string ToString()
            => $"Year {year}, {CalendarUtils.GetSeasonDisplayName(season)}, Day {dayInSeason}";
    }

    /// <summary>
    /// 日历数学工具类
    /// 1年 = 4季节 × 12天 = 48天
    /// absoluteDay 从1开始（1-based）
    /// </summary>
    public static class CalendarUtils
    {
        public const int DaysPerSeason = 12;
        public const int SeasonsPerYear = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear; // 48

        /// <summary>
        /// absoluteDay (1-based) → GameDate
        /// Day 1 = Year startingYear, Spring, Day 1
        /// ⚠️ 使用 (absoluteDay - 1) 转为0-based后再运算，避免取余陷阱
        /// </summary>
        public static GameDate AbsoluteDayToDate(int absoluteDay, int startingYear)
        {
            int zeroIndexed = absoluteDay - 1; // 转为0-based
            int year = startingYear + zeroIndexed / DaysPerYear;
            int remainder = zeroIndexed % DaysPerYear;
            Season season = (Season)(remainder / DaysPerSeason);
            int dayInSeason = (remainder % DaysPerSeason) + 1; // +1 回到 1-based

            return new GameDate
            {
                year = year,
                season = season,
                dayInSeason = dayInSeason
            };
        }

        /// <summary>
        /// GameDate → absoluteDay (1-based)
        /// </summary>
        public static int DateToAbsoluteDay(int year, Season season, int dayInSeason, int startingYear)
        {
            return (year - startingYear) * DaysPerYear
                 + (int)season * DaysPerSeason
                 + dayInSeason; // dayInSeason 本身 1-based，结果自然 1-based
        }

        /// <summary>
        /// 返回该 absoluteDay 所在季节第一天的 absoluteDay
        /// </summary>
        public static int GetSeasonStartDay(int absoluteDay)
        {
            int zeroIndexed = absoluteDay - 1;
            int seasonIndex = zeroIndexed / DaysPerSeason; // 第几个季节（0-based）
            return seasonIndex * DaysPerSeason + 1; // +1 回到 1-based
        }

        /// <summary>
        /// 获取指定 absoluteDay 所在季节的范围 [start, end]（inclusive）
        /// </summary>
        public static (int start, int end) GetSeasonRange(int absoluteDay)
        {
            int start = GetSeasonStartDay(absoluteDay);
            return (start, start + DaysPerSeason - 1);
        }

        /// <summary>
        /// 季节枚举转英文显示名
        /// </summary>
        public static string GetSeasonDisplayName(Season season) => season switch
        {
            Season.Spring => "SPRING",
            Season.Summer => "SUMMER",
            Season.Autumn => "AUTUMN",
            Season.Winter => "WINTER",
            _ => ""
        };
    }
}
