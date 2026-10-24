using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public sealed class NorthernQuickStatisticsViewModel
    {
        public DateTime ForDate { get; set; } = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
        public DateTime EndDate => ForDate.AddDays(-1);
        public DateTime? DataStart { get; set; }
        public DateTime? DataEnd { get; set; }
        public int DrawCount { get; set; }
        public int MonthDrawCount { get; set; }
        public List<QuickGap> LotoGaps { get; set; } = new();
        public List<QuickFrequency> Frequent { get; set; } = new();
        public List<QuickGap> Leaders { get; set; } = new();
        public List<QuickReversePair> ReversePairs { get; set; } = new();
        public List<QuickGap> SpecialGaps { get; set; } = new();
        public List<QuickGap> Sums { get; set; } = new();
        public List<QuickGap> Touches { get; set; } = new();
    }
    public sealed class QuickGap
    {
        public int Number { get; set; }
        public string Label => Number.ToString("D2");
        public int Days { get; set; }
        public bool Exact { get; set; }
        public string Display => (Exact ? "" : "≥") + Days;
        public int MaxDays { get; set; }
        public bool MaxExact { get; set; }
        public DateTime? MaxStart { get; set; }
        public DateTime? MaxEnd { get; set; }
    }
    public sealed class QuickFrequency
    {
        public int Number { get; set; }
        public string Label => Number.ToString("D2");
        public int Count { get; set; }
    }
    public sealed class QuickReversePair
    {
        public QuickGap First { get; set; }
        public QuickGap Second { get; set; }
    }
}
