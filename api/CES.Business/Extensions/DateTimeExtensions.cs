using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Extensions
{
    public static class DateTimeExtensions
    {
        public static string ToDateTimeWithFormatString(this DateTime? dateTime, DateTimeFormat format)
        {
            return dateTime.HasValue ? ToDateTimeWithFormatString(dateTime.Value, format) : "";
        }
        public static string ToDateTimeWithFormatString(this DateTime dateTime, DateTimeFormat format)
        {
            return dateTime.ToString(EnumHelper.GetEnumDescription(format), System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToDateOnlyString(this DateTime? dateTime)
        {
            return dateTime.HasValue ? ToDateOnlyString(dateTime.Value) : "";
        }

        public static string ToDateOnlyString(this DateTime dateTime)
        {
            return ToDateTimeWithFormatString(dateTime, DateTimeFormat.DateOnly);
        }

        public static string ToTimeOnlyString(this DateTime? dateTime)
        {
            return dateTime.HasValue ? ToTimeOnlyString(dateTime.Value) : "";
        }

        public static string ToTimeOnlyString(this DateTime dateTime)
        {
            return ToDateTimeWithFormatString(dateTime, DateTimeFormat.TimeOnly);
        }

        public static string ToVerboseDateTimeString(this DateTime? dateTime)
        {
            return dateTime.HasValue ? ToVerboseDateTimeString(dateTime.Value) : "";
        }

        public static string ToVerboseDateTimeString(this DateTime dateTime)
        {
            return ToDateTimeWithFormatString(dateTime, DateTimeFormat.VerboseDateTime);
        }

        public static DateTime GetStartOfWeekDate(this DateTime dateTime, DayOfWeek WeekStartDay)
        {
            int diff = WeekStartDay - dateTime.DayOfWeek;
            return dateTime.AddDays(diff);
        }

        public static DateTime GetEndOfWeekDate(this DateTime dateTime, DayOfWeek WeekStartDay)
        {
            return GetStartOfWeekDate(dateTime, WeekStartDay).AddDays(7);
        }



    }

    public enum DateTimeFormat
    {
        [Description("dddd; dd MMMM yyyy HH:mm tt")]
        VerboseDateTime,

        [Description("yyyy-MM-dd hh:mm")]
        DateAndTime,

        [Description("yyyy-MM-dd")]
        DateOnly,

        [Description("hh:mm tt")]
        TimeOnly,

        [Description("dd/MM/yyyy")]
        PSTFormat,

        [Description("dd/MM/yyyy hh:mm:ss")]
        PSTFormatWithTime,

        [Description("dd MMM yyyy")]
        DateMonthYear,

        [Description("MMM dd")]
        MMMdd,

        [Description("MMM d")]
        MMMd,
    }
}
