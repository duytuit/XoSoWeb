using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public class VietlottStatisticsIndexViewModel
    {
        public List<VietlottStatisticsGameViewModel> Games { get; set; } = new List<VietlottStatisticsGameViewModel>();
    }

    public class VietlottStatisticsGameViewModel
    {
        public string GameCode { get; set; }
        public string Slug { get; set; }
        public string Name { get; set; }
        public List<VietlottStatisticsToolViewModel> Tools { get; set; } = new List<VietlottStatisticsToolViewModel>();
    }

    public class VietlottStatisticsToolViewModel
    {
        public string Slug { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class VietlottStatisticsPageViewModel
    {
        public string GameCode { get; set; }
        public string GameSlug { get; set; }
        public string GameName { get; set; }
        public string ToolSlug { get; set; }
        public string ToolName { get; set; }
        public string Description { get; set; }
        public string EndDraw { get; set; }
        public string PrizeName { get; set; }
        public int RequestedPeriods { get; set; }
        public int UsedPeriods { get; set; }
        public int ExcludedPeriods { get; set; }
        public DateTime? OldestDrawDate { get; set; }
        public DateTime? NewestDrawDate { get; set; }
        public List<int> PeriodPresets { get; set; } = new List<int>();
        public List<VietlottStatisticsDrawOption> DrawOptions { get; set; } = new List<VietlottStatisticsDrawOption>();
        public List<string> PrizeOptions { get; set; } = new List<string>();
        public List<VietlottStatisticsToolViewModel> Tools { get; set; } = new List<VietlottStatisticsToolViewModel>();
        public List<VietlottStatisticsRow> Rows { get; set; } = new List<VietlottStatisticsRow>();
        public List<VietlottStatisticsRow> PageRows { get; set; } = new List<VietlottStatisticsRow>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalRows { get; set; }
        public string Sort { get; set; } = "number";
        public string Search { get; set; }
        public bool IsJackpotEstimate { get; set; }
        public bool IsMax3d { get; set; }
        public bool IsDrawHistory { get; set; }
        public bool HasDrawReferences { get; set; }
    }

    public class VietlottStatisticsRow
    {
        public string Value { get; set; }
        public string Detail { get; set; }
        public long Count { get; set; }
        public int? DrawCount { get; set; }
        public int? Gap { get; set; }
        public string DrawNo { get; set; }
        public DateTime? DrawDate { get; set; }
        public string DrawUrl { get; set; }
        public long SortValue { get; set; }
    }

    public class VietlottStatisticsDrawOption
    {
        public string DrawNo { get; set; }
        public DateTime DrawDate { get; set; }
    }
}
