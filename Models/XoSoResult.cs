using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
   public class XoSoResult
    {
        public long Id { get; set; }

        public string Region { get; set; }

        public string ProvinceCode { get; set; }

        public string ProvinceName { get; set; }

        public DateTime DrawDate { get; set; }

        public List<XoSoPrize> Prizes { get; set; }
            = new List<XoSoPrize>();

        public List<LotoStatisticRow> LotoStatistics { get; set; }
            = new List<LotoStatisticRow>();
    }
}