using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Extensions
{
    public static class StringExtensions
    {
        public static decimal ConvertToDecimalTime(this string input)
        {
            if (decimal.TryParse(input, out decimal decimalHours))
            {
                return decimalHours;
            }

            if (TimeSpan.TryParse(input, out TimeSpan timeSpan))
            {
                return (decimal)timeSpan.TotalHours;
            }

            return 0;
        }

        private static string GetFormattedTime(decimal hours, StringFormats stringFormat)
        {
            int fullHours = (int)hours;
            int minutes = (int)((hours - fullHours) * 60);
            string formattedTime = "";

            switch (stringFormat)
            {
                case StringFormats.FullTimeString:
                    formattedTime = $"{fullHours} hours";
                    if (minutes > 0)
                    {
                        formattedTime += $"{fullHours} hours";
                    }
                    break;
                case StringFormats.TimeFormat:
                    formattedTime = $"{fullHours}:{minutes}";
                    break;

            }
            return formattedTime;
        }

        public static string ConvertHoursToStringFormat(this decimal hours, StringFormats stringformat = StringFormats.FullTimeString)
        {
            return GetFormattedTime(hours, stringformat);
        }

        public static string ConvertHoursToStringFormat(this string time, StringFormats stringFormat = StringFormats.FullTimeString)
        {
            decimal hours = ConvertToDecimalTime(time);
            return GetFormattedTime(hours, stringFormat);

        }

    }

    public enum StringFormats
    {
        [Description("HH:mm")]
        TimeFormat,

        [Description("FullTimeString")]
        FullTimeString

    }
}
