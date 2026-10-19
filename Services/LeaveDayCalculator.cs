namespace SquadInternal.Services
{
    public static class LeaveDayCalculator
    {
        public static decimal Calculate(
            DateTime fromDate,
            DateTime toDate,
            string duration,
            IReadOnlyDictionary<DateTime, bool> holidays)
        {
            if (toDate.Date < fromDate.Date)
                return 0m;

            if (duration != "Full Day" && fromDate.Date != toDate.Date)
                return 0m;

            decimal total = 0m;
            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                if (holidays.TryGetValue(date, out var isHalfDayHoliday))
                {
                    if (isHalfDayHoliday)
                        total += 0.5m;
                    continue;
                }

                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    continue;

                total += duration == "Full Day" ? 1m : 0.5m;
            }

            return total;
        }
    }
}