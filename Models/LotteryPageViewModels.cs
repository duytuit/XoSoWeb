using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public class XoSoRegionViewModel
    {
        public string Region { get; set; }
        public string RegionName { get; set; }
        public int? SelectedDay { get; set; }
        public List<XoSoResult> Results { get; set; } = new List<XoSoResult>();
        public List<LotoStatisticRow> LotoStatistics { get; set; } = new List<LotoStatisticRow>();
    }

    public class LotoStatisticRow
    {
        public int Digit { get; set; }
        public string HeadNumbers { get; set; }
        public string TailNumbers { get; set; }
    }

    public class VietlottPageViewModel
    {
        public List<VietlottResult> Results { get; set; } = new List<VietlottResult>();
        public string SelectedGameCode { get; set; }
        public int SelectedOffset { get; set; }
    }

    public class SpecialPrizeStatisticsViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<SpecialPrizeRegionStatistics> Regions { get; set; } = new List<SpecialPrizeRegionStatistics>();
    }

    public class SpecialPrizeRegionStatistics
    {
        public string RegionCode { get; set; }
        public string RegionName { get; set; }
        public List<SpecialPrizeStatisticsEntry> Entries { get; set; } = new List<SpecialPrizeStatisticsEntry>();
    }

    public class SpecialPrizeStatisticsEntry
    {
        public DateTime DrawDate { get; set; }
        public string ProvinceName { get; set; }
        public List<string> Numbers { get; set; } = new List<string>();
    }

    public class HomePageViewModel
    {
        public XoSoRegionViewModel MienBac { get; set; }
        public XoSoRegionViewModel MienTrung { get; set; }
        public XoSoRegionViewModel MienNam { get; set; }
        public VietlottPageViewModel Vietlott { get; set; }
    }
}
