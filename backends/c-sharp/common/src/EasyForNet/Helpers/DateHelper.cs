using System;
using System.Collections.Generic;

namespace EasyForNet.Helpers
{
    public static class DateHelper
    {
        public static IEnumerable<DateTime> EachDate(DateTime from, DateTime to)
        {
            for (var date = from.Date; date.Date <= to.Date; date = date.AddDays(1))
                yield return date;
        }
    }
}