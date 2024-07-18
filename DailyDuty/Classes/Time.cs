using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace DailyDuty.Classes;

public static class Time {
    private static DateTime GetNextDateTimeForHour(int hours)
        => System.Cache.UtcNow.Hour < hours ? System.Cache.UtcNow.Date.AddHours(hours) : System.Cache.UtcNow.Date.AddDays(1).AddHours(hours);

    public static DateTime NextDailyReset()
        => GetNextDateTimeForHour(15);

    public static DateTime NextWeeklyReset()
        => NextDayOfWeek(DayOfWeek.Tuesday, 8);

    public static DateTime NextFashionReportReset()
        => NextWeeklyReset().AddDays(3);

    public static DateTime NextGrandCompanyReset()
        => GetNextDateTimeForHour(20);

    public static DateTime NextLeveAllowanceReset() {
        var now = System.Cache.UtcNow;

        return now.Hour < 12 ? now.Date.AddHours(12) : now.Date.AddDays(1);
    }

    private static DateTime NextDayOfWeek(DayOfWeek weekday, int hour) {
        var today = System.Cache.UtcNow;

        if (today.Hour < hour && today.DayOfWeek == weekday) {
            return today.Date.AddHours(hour);
        }
        var nextReset = today.AddDays(1);

        while (nextReset.DayOfWeek != weekday) {
            nextReset = nextReset.AddDays(1);
        }

        return nextReset.Date.AddHours(hour);
    }

    public class DatacenterException : Exception;
    
    public static DateTime NextJumboCactpotReset() {
        return System.Cache.DataCenterRegion switch {
            // Japan
            1 => NextDayOfWeek(DayOfWeek.Saturday, 12),

            // North America
            2 => NextDayOfWeek(DayOfWeek.Sunday, 2),

            // Europe
            3 => NextDayOfWeek(DayOfWeek.Saturday, 19),

            // Australia
            4 => NextDayOfWeek(DayOfWeek.Saturday, 9),
            
            // Cloud
            7 => NextDayOfWeek(DayOfWeek.Sunday, 2),

            // Unknown Region
            _ => throw new DatacenterException(),
        };
    }

    public static string FormatTimespan(this TimeSpan timeSpan, bool hideSeconds = false)
        => hideSeconds ? 
               $"{timeSpan.Days:0}.{timeSpan.Hours:00}:{timeSpan.Minutes:00}" : 
               $"{timeSpan.Days:0}.{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
}