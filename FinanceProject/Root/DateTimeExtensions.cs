using System;

namespace FinanceManager.Root
{
    /// <summary>
    /// Extension methods for DateTime to handle common date operations consistently across the application
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Gets the start of the day (midnight)
        /// </summary>
        public static DateTime StartOfDay(this DateTime date)
        {
            return date.Date;
        }

        /// <summary>
        /// Gets the end of the day (23:59:59.9999999)
        /// </summary>
        public static DateTime EndOfDay(this DateTime date)
        {
            return date.Date.AddDays(1).AddTicks(-1);
        }

        /// <summary>
        /// Gets the first day of the month
        /// </summary>
        public static DateTime StartOfMonth(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, 1);
        }

        /// <summary>
        /// Gets the last moment of the last day of the month
        /// </summary>
        public static DateTime EndOfMonth(this DateTime date)
        {
            return date.StartOfMonth().AddMonths(1).AddDays(-1).EndOfDay();
        }

        /// <summary>
        /// Gets the first day of the year
        /// </summary>
        public static DateTime StartOfYear(this DateTime date)
        {
            return new DateTime(date.Year, 1, 1);
        }

        /// <summary>
        /// Gets the last moment of the last day of the year
        /// </summary>
        public static DateTime EndOfYear(this DateTime date)
        {
            return new DateTime(date.Year, 12, 31).EndOfDay();
        }

        /// <summary>
        /// Gets the first day of the quarter containing the specified date
        /// </summary>
        public static DateTime StartOfQuarter(this DateTime date)
        {
            var quarter = (date.Month - 1) / 3;
            return new DateTime(date.Year, quarter * 3 + 1, 1);
        }

        /// <summary>
        /// Gets the last moment of the last day of the quarter
        /// </summary>
        public static DateTime EndOfQuarter(this DateTime date)
        {
            return date.StartOfQuarter().AddMonths(3).AddDays(-1).EndOfDay();
        }

        /// <summary>
        /// Gets the quarter number (1-4) for the specified date
        /// </summary>
        public static int GetQuarter(this DateTime date)
        {
            return (date.Month - 1) / 3 + 1;
        }
    }
}