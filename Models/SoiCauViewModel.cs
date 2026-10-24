using System;
using System.Collections.Generic;
namespace XoSoWeb.Models
{
    public sealed class SoiCauViewModel
    {
        public DateTime Date { get; set; }
        public int Length { get; set; } = 5;
        public string Target { get; set; } = "loto";
        public bool Reverse { get; set; } = true;
        public string Q { get; set; } = "";
        public List<SoiCauDraw> Draws { get; set; } = new();
        public List<SoiCauPattern> Patterns { get; set; } = new();
    }
    public sealed class SoiCauDraw
    {
        public DateTime Date { get; set; }
        public string Digits { get; set; }
        public List<string> Labels { get; set; } = new();
        public HashSet<string> Loto { get; set; } = new();
        public string Special { get; set; }
    }
    public sealed class SoiCauPattern
    {
        public int First { get; set; }
        public int Second { get; set; }
        public string Number { get; set; }
        public List<SoiCauStep> Steps { get; set; } = new();
    }
    public sealed class SoiCauStep
    {
        public DateTime SourceDate { get; set; }
        public DateTime ResultDate { get; set; }
        public string Number { get; set; }
        public string Matched { get; set; }
    }
}
