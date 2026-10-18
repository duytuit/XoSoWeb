using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public class LotoFrequencyViewModel
    {
        public DateTime WeekStart { get; set; }
        public string Region { get; set; }
        public int DrawCount { get; set; }
        public int[,] Counts { get; } = new int[100, 7];

        public int Total(int number)
        {
            var total = 0;
            for (var day = 0; day < 7; day++) total += Counts[number, day];
            return total;
        }

        public void Add(DateTime date, IEnumerable<XoSoPrize> prizes)
        {
            var day = (date.Date - WeekStart.Date).Days;
            if (day < 0 || day > 6) return;
            DrawCount++;
            foreach (var prize in prizes)
            {
                foreach (var number in prize?.Numbers ?? new List<string>())
                {
                    var text = number?.Trim();
                    if (text == null || text.Length < 2) continue;
                    var tens = text[text.Length - 2] - '0';
                    var units = text[text.Length - 1] - '0';
                    if (tens >= 0 && tens <= 9 && units >= 0 && units <= 9)
                        Counts[tens * 10 + units, day]++;
                }
            }
        }
    }
}
