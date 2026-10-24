using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public sealed class NumberRhythmViewModel
    {
        public DateTime EndDate { get; set; }
        public int Days { get; set; } = 30;
        public DateTime StartDate => EndDate.AddDays(1 - Days);
        public string Target { get; set; } = "loto";
        public string Sort { get; set; } = "number";
        public string Q { get; set; } = "";
        public List<NumberRhythmDay> Rows { get; set; } = new();
        public List<NumberRhythmColumn> Columns { get; set; } = new();
        public int DrawCount { get; set; }
    }

    public sealed class NumberRhythmDay
    {
        public DateTime Date { get; set; }
        public bool HasData { get; set; }
        public int[] Counts { get; set; } = new int[100];
        public int Special { get; set; } = -1;
    }

    public sealed class NumberRhythmColumn
    {
        public int Number { get; set; }
        public string Label => Number.ToString("D2");
        public int Total { get; set; }
        public int HitDays { get; set; }
        public int Gap { get; set; }
        public bool GapExact { get; set; }
        public DateTime? LastSeen { get; set; }
        public List<NumberRhythmCycle> Cycles { get; set; } = new();
    }

    public sealed class NumberRhythmCycle
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int MissDays => (To - From).Days - 1;
    }
}
